using System.Collections.Generic;
using System.Numerics;
using Game.Data;

namespace Game.Core
{
    /// <summary>물질이 대상에 닿았을 때의 규칙을 처리한다. 반응하거나, 덮어쓰거나, 아무 일도 없다</summary>
    public sealed class ReactionResolver
    {
        readonly GameData data;
        readonly RunState state;
        readonly SpatialGrid grid;
        readonly List<RunEvent> events;
        readonly int maxZones;
        readonly int depthLimit;
        readonly int maxReactionsPerTick;
        readonly List<EnemyState> scratch = new List<EnemyState>();
        readonly HashSet<int> visited = new HashSet<int>();

        public ReactionResolver(GameData data, RunState state, SpatialGrid grid, List<RunEvent> events)
        {
            this.data = data;
            this.state = state;
            this.grid = grid;
            this.events = events;
            maxZones = (int)data.Balance("max_zones");
            depthLimit = (int)data.Balance("chain_depth_limit");
            maxReactionsPerTick = (int)data.Balance("max_reactions_per_tick");
        }

        /// <summary>두 물질이 반응할 수 있으면 반응과 새 연쇄 단계를 돌려준다. 단계 한도를 넘으면 반응하지 않는다</summary>
        ReactionDef Match(Held target, Held incoming, out int depth)
        {
            depth = 0;
            if (target.IsEmpty || incoming.IsEmpty) return null;
            ReactionDef reaction = data.FindReaction(target.Id, incoming.Id);
            if (reaction == null) return null;
            depth = (target.Depth > incoming.Depth ? target.Depth : incoming.Depth) + 1;
            return depth <= depthLimit ? reaction : null;
        }

        /// <summary>적에게 물질이 닿았다. 반응했으면 true. 반응하지 않으면 들어온 물질로 덮어쓴다</summary>
        public bool ContactEnemy(EnemyState enemy, Held incoming)
        {
            if (incoming.IsEmpty) return false;
            ReactionDef reaction = Match(enemy.Substance, incoming, out int depth);
            if (reaction != null)
            {
                // 반응하면 두 물질 모두 사라진다. 결과는 큐에 넣어 틱 끝에 처리한다
                enemy.Substance = new Held();
                state.ReactionQueue.Add(new PendingReaction
                {
                    ReactionId = reaction.Id, Pos = enemy.Pos, Depth = depth, TargetEnemyId = enemy.Id
                });
                return true;
            }
            enemy.Substance = new Held
            {
                Id = incoming.Id,
                TimeLeft = data.Substance(incoming.Id).Duration,
                Depth = incoming.Depth
            };
            return false;
        }

        /// <summary>영역에 물질이 닿았다. 반응했으면 true. 반응하지 않으면 영역은 바뀌지 않는다</summary>
        public bool ContactZone(ZoneState zone, Held incoming, Vector2 at)
        {
            if (zone.Removed) return false;
            ReactionDef reaction = Match(zone.Substance, incoming, out int depth);
            if (reaction == null) return false;
            zone.Removed = true;
            state.ReactionQueue.Add(new PendingReaction
            {
                ReactionId = reaction.Id, Pos = at, Depth = depth, TargetEnemyId = -1
            });
            return true;
        }

        /// <summary>쌓인 반응을 앞에서부터 처리한다. 한 틱 상한을 넘는 것은 다음 틱으로 넘긴다</summary>
        public void ProcessQueue()
        {
            int processed = 0;
            // 처리 중에 새 반응이 큐 끝에 붙을 수 있다. 그것도 같은 상한에 포함된다
            while (processed < state.ReactionQueue.Count && processed < maxReactionsPerTick)
            {
                PendingReaction pending = state.ReactionQueue[processed];
                processed++;
                Apply(pending);
            }
            state.ReactionQueue.RemoveRange(0, processed);
        }

        void Apply(PendingReaction pending)
        {
            ReactionDef reaction = FindReactionById(pending.ReactionId);
            if (reaction == null) return;
            state.ReactionCount++;
            events.Add(new RunEvent { Type = RunEventType.Reaction, Id = reaction.Id, Pos = pending.Pos });

            foreach (EffectStep step in data.Effect(reaction.EffectId).Steps)
            {
                switch (step.Piece)
                {
                    case EffectPiece.Damage:
                    {
                        EnemyState target = FindEnemy(pending.TargetEnemyId);
                        if (target != null) target.Hp -= step.Amount;
                        break;
                    }
                    case EffectPiece.ApplySubstance:
                    {
                        EnemyState target = FindEnemy(pending.TargetEnemyId);
                        if (target != null) ContactEnemy(target, Carried(step.Substance, pending.Depth));
                        break;
                    }
                    case EffectPiece.SpawnZone:
                        SpawnZone(pending.Pos, step.Radius, step.Duration, 0f,
                            new Held { Id = step.Substance, TimeLeft = -1f, Depth = pending.Depth });
                        break;
                    case EffectPiece.Explode:
                        Explode(pending, step);
                        break;
                    case EffectPiece.Chain:
                        Chain(pending, step);
                        break;
                }
            }
        }

        Held Carried(string substanceId, int depth)
        {
            return new Held { Id = substanceId, TimeLeft = data.Substance(substanceId).Duration, Depth = depth };
        }

        /// <summary>반경 안 모든 적에게 피해를 준다. 물질이 있으면 반경 안 적과 영역에 그 물질이 닿은 것으로 처리해 연쇄가 이어진다</summary>
        void Explode(PendingReaction pending, EffectStep step)
        {
            events.Add(new RunEvent { Type = RunEventType.Explosion, Id = pending.ReactionId, Pos = pending.Pos, Radius = step.Radius });
            grid.Query(pending.Pos, step.Radius, scratch);
            foreach (EnemyState e in scratch) e.Hp -= step.Amount;
            if (step.Substance == null) return;

            Held carried = Carried(step.Substance, pending.Depth);
            foreach (EnemyState e in scratch)
                if (e.Hp > 0f) ContactEnemy(e, carried);
            foreach (ZoneState z in state.Zones)
            {
                if (z.Removed) continue;
                float limit = step.Radius + z.Radius;
                if (Vector2.DistanceSquared(z.Pos, pending.Pos) <= limit * limit) ContactZone(z, carried, z.Pos);
            }
        }

        /// <summary>가까운 적으로 차례로 옮겨 가며 피해를 준다. 같은 적을 두 번 맞히지 않는다</summary>
        void Chain(PendingReaction pending, EffectStep step)
        {
            visited.Clear();
            if (pending.TargetEnemyId >= 0) visited.Add(pending.TargetEnemyId);
            Vector2 current = pending.Pos;
            int hops = 0;
            while (hops < step.Count)
            {
                EnemyState next = grid.Nearest(current, step.Radius, visited);
                if (next == null) break;
                visited.Add(next.Id);
                // 이미 죽은 적은 건너뛴다. 횟수에는 넣지 않는다
                if (next.Hp <= 0f) continue;
                next.Hp -= step.Amount;
                events.Add(new RunEvent { Type = RunEventType.ChainLink, Id = pending.ReactionId, Pos = current, Pos2 = next.Pos });
                current = next.Pos;
                hops++;
            }
        }

        ReactionDef FindReactionById(string id)
        {
            foreach (ReactionDef r in data.Reactions)
                if (r.Id == id) return r;
            return null;
        }

        EnemyState FindEnemy(int id)
        {
            if (id < 0) return null;
            foreach (EnemyState e in state.Enemies)
                if (e.Id == id) return e.Hp > 0f ? e : null;
            return null;
        }

        /// <summary>영역을 만든다. 상한에 닿았으면 가장 오래된 지형 아닌 영역을 지우고 만든다</summary>
        public ZoneState SpawnZone(Vector2 pos, float radius, float duration, float damage, Held substance)
        {
            int alive = 0;
            foreach (ZoneState z in state.Zones) if (!z.Removed) alive++;
            if (alive >= maxZones)
            {
                for (int i = 0; i < state.Zones.Count; i++)
                {
                    ZoneState old = state.Zones[i];
                    if (old.Removed || old.TimeLeft < 0f) continue;
                    old.Removed = true;
                    break;
                }
            }
            var zone = new ZoneState
            {
                Id = state.NextId++,
                Pos = pos,
                Radius = radius,
                TimeLeft = duration,
                Damage = damage,
                ContactTimer = 0f,
                Substance = substance
            };
            state.Zones.Add(zone);
            events.Add(new RunEvent { Type = RunEventType.ZoneSpawned, Id = substance.Id, Pos = pos, Radius = radius });
            SweepZones();
            return zone;
        }

        /// <summary>사라지기로 표시한 영역을 목록에서 뺀다. 영역 목록을 순회하는 중에는 부르지 않는다</summary>
        public void SweepZones()
        {
            state.Zones.RemoveAll(z => z.Removed);
        }
    }
}
