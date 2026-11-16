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
        readonly StageDef stage;
        readonly float upgradeDamage;
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
            stage = data.Stage(state.StageId);
            upgradeDamage = data.Balance("upgrade_damage");
            depthLimit = (int)data.Balance("chain_depth_limit");
            maxReactionsPerTick = (int)data.Balance("max_reactions_per_tick");
        }

        /// <summary>두 물질이 반응할 수 있으면 반응과 새 연쇄 단계를 돌려준다. 단계 한도를 넘으면 반응하지 않는다</summary>
        ReactionDef Match(Held target, Held incoming, out int depth, out int level)
        {
            depth = 0;
            level = target.Level > incoming.Level ? target.Level : incoming.Level;
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
            ReactionDef reaction = Match(enemy.Substance, incoming, out int depth, out int level);
            if (reaction != null)
            {
                // 반응하면 두 물질 모두 사라진다. 결과는 큐에 넣어 틱 끝에 처리한다
                enemy.Substance = new Held();
                state.ReactionQueue.Add(new PendingReaction
                {
                    ReactionId = reaction.Id, Pos = enemy.Pos, Depth = depth, Level = level, TargetEnemyId = enemy.Id
                });
                return true;
            }
            enemy.Substance = new Held
            {
                Id = incoming.Id,
                TimeLeft = data.Substance(incoming.Id).Duration,
                Depth = incoming.Depth,
                Level = incoming.Level
            };
            return false;
        }

        /// <summary>영역에 물질이 닿았다. 반응했으면 true. 반응하지 않으면 영역은 바뀌지 않는다</summary>
        public bool ContactZone(ZoneState zone, Held incoming, Vector2 at)
        {
            if (zone.Removed) return false;
            ReactionDef reaction = Match(zone.Substance, incoming, out int depth, out int level);
            if (reaction == null) return false;
            zone.Removed = true;
            state.ReactionQueue.Add(new PendingReaction
            {
                ReactionId = reaction.Id, Pos = at, Depth = depth, Level = level, TargetEnemyId = -1
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

            // 물질을 강화했으면 반응의 피해가 그만큼 세진다
            float power = 1f + upgradeDamage * System.Math.Max(0, pending.Level - 1);
            foreach (EffectStep step in data.Effect(reaction.EffectId).Steps)
            {
                switch (step.Piece)
                {
                    case EffectPiece.Damage:
                    {
                        EnemyState target = FindEnemy(pending.TargetEnemyId);
                        if (target != null) Combat.Damage(target, step.Amount * power);
                        break;
                    }
                    case EffectPiece.ApplySubstance:
                    {
                        EnemyState target = FindEnemy(pending.TargetEnemyId);
                        if (target != null) ContactEnemy(target, Carried(step.Substance, pending));
                        break;
                    }
                    case EffectPiece.SpawnZone:
                        SpawnZone(pending.Pos, step.Radius, step.Duration, step.Amount * power,
                            new Held { Id = step.Substance, TimeLeft = -1f, Depth = pending.Depth, Level = pending.Level });
                        break;
                    case EffectPiece.Explode:
                        Explode(pending, step, power);
                        break;
                    case EffectPiece.Chain:
                        Chain(pending, step, power);
                        break;
                    case EffectPiece.Status:
                        if (step.Radius > 0f)
                        {
                            grid.Query(pending.Pos, step.Radius, scratch);
                            foreach (EnemyState e in scratch) ApplyStatus(e, step, power);
                        }
                        else
                        {
                            // 반경이 없으면 반응이 일어난 적 하나에게만 건다
                            EnemyState target = FindEnemy(pending.TargetEnemyId);
                            if (target != null) ApplyStatus(target, step, power);
                        }
                        break;
                    case EffectPiece.Push:
                        Push(pending, step);
                        break;
                }
            }
        }

        Held Carried(string substanceId, PendingReaction pending)
        {
            return new Held { Id = substanceId, TimeLeft = data.Substance(substanceId).Duration, Depth = pending.Depth, Level = pending.Level };
        }

        /// <summary>반경 안 모든 적에게 피해를 준다. 물질이 있으면 반경 안 적과 영역에 그 물질이 닿은 것으로 처리해 연쇄가 이어진다</summary>
        void Explode(PendingReaction pending, EffectStep step, float power)
        {
            events.Add(new RunEvent { Type = RunEventType.Explosion, Id = pending.ReactionId, Pos = pending.Pos, Radius = step.Radius });
            grid.Query(pending.Pos, step.Radius, scratch);
            foreach (EnemyState e in scratch) Combat.Damage(e, step.Amount * power);
            if (step.Substance == null) return;

            Held carried = Carried(step.Substance, pending);
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
        void Chain(PendingReaction pending, EffectStep step, float power)
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
                Combat.Damage(next, step.Amount * power);
                events.Add(new RunEvent { Type = RunEventType.ChainLink, Id = pending.ReactionId, Pos = current, Pos2 = next.Pos });
                current = next.Pos;
                hops++;
            }
        }

        /// <summary>상태를 건다. 이미 걸려 있으면 남은 시간은 긴 쪽, 세기는 센 쪽을 따른다</summary>
        static void ApplyStatus(EnemyState e, EffectStep step, float power)
        {
            switch (step.Status)
            {
                case "freeze":
                    if (step.Duration > e.FreezeTime) e.FreezeTime = step.Duration;
                    break;
                case "slow":
                    e.SlowFactor = e.SlowTime > 0f && e.SlowFactor < step.Amount ? e.SlowFactor : step.Amount;
                    if (step.Duration > e.SlowTime) e.SlowTime = step.Duration;
                    break;
                case "dot":
                    float dps = step.Amount * power;
                    e.DotDps = e.DotTime > 0f && e.DotDps > dps ? e.DotDps : dps;
                    if (step.Duration > e.DotTime) e.DotTime = step.Duration;
                    break;
                case "vulnerable":
                    e.VulnMul = e.VulnTime > 0f && e.VulnMul > step.Amount ? e.VulnMul : step.Amount;
                    if (step.Duration > e.VulnTime) e.VulnTime = step.Duration;
                    break;
            }
        }

        /// <summary>반경 안의 적을 밀어내거나 (양수) 끌어당긴다 (음수). 끌어당길 때 중심을 지나치지 않고 맵 밖으로 나가지 않는다</summary>
        void Push(PendingReaction pending, EffectStep step)
        {
            const float MinDistanceFromCenter = 0.3f;
            grid.Query(pending.Pos, step.Radius, scratch);
            foreach (EnemyState e in scratch)
            {
                Vector2 away = e.Pos - pending.Pos;
                float dist = away.Length();
                if (dist < 0.0001f) continue;
                float newDist = dist + step.Amount;
                if (step.Amount < 0f && newDist < MinDistanceFromCenter) newDist = System.Math.Min(dist, MinDistanceFromCenter);
                Vector2 pos = pending.Pos + away / dist * newDist;
                pos.X = System.Math.Min(stage.Width, System.Math.Max(0f, pos.X));
                pos.Y = System.Math.Min(stage.Height, System.Math.Max(0f, pos.Y));
                e.Pos = pos;
            }
            // 적이 옮겨졌으니 격자를 다시 만든다. 안 그러면 뒤따르는 조각이 옛 자리에서 찾는다
            grid.Rebuild(state.Enemies);
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
