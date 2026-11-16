using System;
using System.Collections.Generic;
using System.Numerics;
using Game.Data;

namespace Game.Core
{
    /// <summary>판을 한 틱씩 진행시킨다. 규칙은 전부 여기와 여기서 부르는 클래스에 있다</summary>
    public sealed class Simulation
    {
        readonly GameData data;
        readonly StageDef stage;
        readonly float dt;
        readonly float playerSpeed;
        readonly float playerRadius;
        readonly int maxEnemies;
        readonly float contactInterval;
        readonly int trailLength;
        readonly float absorbGrowth;
        readonly int upgradeMax;
        readonly SpellCaster caster;
        readonly List<EnemyState> scratch = new List<EnemyState>();

        public RunState State { get; }
        public SpatialGrid Grid { get; }
        /// <summary>이번 틱에 일어난 일. Tick 을 부를 때마다 비운다</summary>
        public List<RunEvent> Events { get; } = new List<RunEvent>();
        /// <summary>물질 접촉과 반응 규칙</summary>
        public ReactionResolver Resolver { get; }

        public Simulation(GameData data, RunState state)
        {
            this.data = data;
            State = state;
            stage = data.Stage(state.StageId);
            dt = 1f / data.Balance("tick_rate");
            playerSpeed = data.Balance("player_speed");
            playerRadius = data.Balance("player_radius");
            maxEnemies = (int)data.Balance("max_enemies");
            Grid = new SpatialGrid(stage.Width, stage.Height, data.Balance("grid_cell_size"));
            Grid.Rebuild(state.Enemies);
            contactInterval = data.Balance("contact_interval");
            trailLength = Math.Max(1, (int)Math.Round(data.Balance("tail_delay") * data.Balance("tick_rate")));
            absorbGrowth = data.Balance("absorb_growth");
            upgradeMax = (int)data.Balance("upgrade_max");
            Resolver = new ReactionResolver(data, state, Grid, Events);
            caster = new SpellCaster(data, state, Grid, Events, Resolver);
        }

        /// <summary>한 틱 진행한다. 진행 중이 아니면 시간이 흐르지 않는다</summary>
        public void Tick(TickInput input)
        {
            Events.Clear();
            if (State.Phase == RunPhase.Dead) return;
            if (State.Phase == RunPhase.ChoosingPart)
            {
                // 붙일 부위를 고르는 동안 시간은 흐르지 않는다. 선택만 받는다
                HandlePartChoice(input.PartChoice);
                return;
            }

            MovePlayer(input.Move);
            SpawnEnemies();
            MoveEnemies();
            Grid.Rebuild(State.Enemies);
            caster.Update(dt);
            UpdateProjectiles();
            UpdateZones();
            UpdateEnemySubstances();
            Resolver.ProcessQueue();
            Resolver.SweepZones();
            RemoveDeadEnemies();
            Grid.Rebuild(State.Enemies);

            if (State.Player.Hp <= 0f)
            {
                State.Phase = RunPhase.Dead;
                Events.Add(new RunEvent { Type = RunEventType.PlayerDied, Pos = State.Player.Pos });
            }
            else if (State.PendingTraits.Count > 0)
            {
                State.Phase = RunPhase.ChoosingPart;
                Events.Add(new RunEvent { Type = RunEventType.TraitPending, Id = State.PendingTraits[0], Pos = State.Player.Pos });
            }
            State.Tick++;
        }

        /// <summary>그 특징을 다음에 얻으려면 몇 마리를 잡아야 하는지. 얻을수록 늘어난다</summary>
        public int AbsorbNeed(int traitIndex)
        {
            double need = stage.Absorb[traitIndex].Value * Math.Pow(absorbGrowth, State.AbsorbAcquired[traitIndex]);
            return Math.Max(1, (int)Math.Round(need));
        }

        /// <summary>잡은 적의 특징 하나를 포식 게이지에 더한다. 다 차면 그 특징을 얻는다</summary>
        void Absorb(string trait)
        {
            int index = stage.AbsorbIndex(trait);
            if (index < 0) return;
            State.AbsorbCount[index]++;
            if (State.AbsorbCount[index] < AbsorbNeed(index)) return;
            State.AbsorbCount[index] = 0;
            State.AbsorbAcquired[index]++;
            State.PendingTraits.Add(trait);
        }

        /// <summary>맨 앞 특징을 버리거나 부위에 붙인다. 같은 특징이 이미 붙어 있으면 강화한다. 남은 특징이 없으면 판을 다시 진행한다</summary>
        void HandlePartChoice(int choice)
        {
            if (State.PendingTraits.Count == 0) { State.Phase = RunPhase.Playing; return; }
            if (choice == TickInput.NoChoice) return;
            if (choice != TickInput.Discard && (choice < 0 || choice >= State.Slots.Length)) return;

            string trait = State.PendingTraits[0];
            State.PendingTraits.RemoveAt(0);
            if (choice != TickInput.Discard)
            {
                SpellSlot slot = State.Slots[choice];
                if (data.IsDelivery(trait))
                {
                    if (slot.DeliveryId == trait) slot.DeliveryLevel = Math.Min(upgradeMax, Math.Max(1, slot.DeliveryLevel) + 1);
                    else { slot.DeliveryId = trait; slot.DeliveryLevel = 1; }
                }
                else
                {
                    if (slot.SubstanceId == trait) slot.SubstanceLevel = Math.Min(upgradeMax, Math.Max(1, slot.SubstanceLevel) + 1);
                    else { slot.SubstanceId = trait; slot.SubstanceLevel = 1; }
                }
                slot.Cooldown = 0f;
                Events.Add(new RunEvent { Type = RunEventType.TraitEquipped, Id = trait, Pos = State.Player.Pos });
            }

            if (State.PendingTraits.Count > 0)
                Events.Add(new RunEvent { Type = RunEventType.TraitPending, Id = State.PendingTraits[0], Pos = State.Player.Pos });
            else
                State.Phase = RunPhase.Playing;
        }

        void MovePlayer(Vector2 move)
        {
            float len = move.Length();
            if (len > 1f) move /= len;
            Vector2 p = State.Player.Pos + move * playerSpeed * dt;
            p.X = Math.Min(stage.Width, Math.Max(0f, p.X));
            p.Y = Math.Min(stage.Height, Math.Max(0f, p.Y));
            State.Player.Pos = p;
            // 움직인 쪽이 앞이다. 가만히 있으면 마지막 방향을 유지한다
            if (len > 0.01f) State.Player.Facing = Vector2.Normalize(move);
            State.Player.Trail.Add(p);
            while (State.Player.Trail.Count > trailLength) State.Player.Trail.RemoveAt(0);
        }

        void SpawnEnemies()
        {
            State.SpawnTimer -= dt;
            if (State.SpawnTimer > 0f) return;

            float seconds = State.Seconds(data);
            float minutes = seconds / 60f;
            State.SpawnTimer = Math.Max(data.Balance("spawn_interval_min"),
                data.Balance("spawn_interval_start") - data.Balance("spawn_interval_decay_per_sec") * seconds);

            int count = 1 + (int)Math.Floor(minutes / data.Balance("spawn_count_step_min"));
            // 시간이 갈수록 적 체력이 가파르게 오른다. 잘하는 사람도 결국 쓰러지게 하기 위한 것
            float hpMul = 1f + data.Balance("enemy_hp_growth") * minutes * minutes;
            float distance = data.Balance("spawn_distance");

            for (int i = 0; i < count && State.Enemies.Count < maxEnemies; i++)
            {
                StageEnemy picked = PickEnemy();
                EnemyDef def = data.Enemy(picked.Body);
                Vector2 pos = PickSpawnPosition(distance);
                var e = new EnemyState
                {
                    Id = State.NextId++,
                    DefId = def.Id,
                    Pos = pos,
                    Hp = def.Hp * hpMul,
                    Radius = def.Radius,
                    InnateSubstance = picked.Substance
                };
                if (picked.Substance != null) e.Substance = new Held { Id = picked.Substance, TimeLeft = -1f, Depth = 0 };
                State.Enemies.Add(e);
            }
        }

        /// <summary>플레이어에서 떨어진 생성 위치를 고른다. 벽에 눌려 플레이어 가까이 붙은 자리는 다시 뽑는다</summary>
        Vector2 PickSpawnPosition(float distance)
        {
            const int Attempts = 8;
            // 맵 가장자리에서는 일부 방향이 벽으로 잘린다. 이 비율보다 가까우면 버린다
            float minDistance = distance * 0.6f;
            Vector2 best = State.Player.Pos;
            float bestDistance = -1f;
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                float angle = State.Rng.Range(0f, (float)(Math.PI * 2.0));
                Vector2 pos = State.Player.Pos + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * distance;
                pos.X = Math.Min(stage.Width, Math.Max(0f, pos.X));
                pos.Y = Math.Min(stage.Height, Math.Max(0f, pos.Y));
                float d = Vector2.Distance(pos, State.Player.Pos);
                if (d > bestDistance) { best = pos; bestDistance = d; }
                if (d >= minDistance) break;
            }
            return best;
        }

        StageEnemy PickEnemy()
        {
            float total = 0f;
            for (int i = 0; i < stage.Enemies.Count; i++) total += stage.Enemies[i].Weight;
            float roll = State.Rng.NextFloat() * total;
            for (int i = 0; i < stage.Enemies.Count; i++)
            {
                roll -= stage.Enemies[i].Weight;
                if (roll < 0f) return stage.Enemies[i];
            }
            return stage.Enemies[stage.Enemies.Count - 1];
        }

        void MoveEnemies()
        {
            foreach (EnemyState e in State.Enemies)
            {
                EnemyDef def = data.Enemy(e.DefId);
                Vector2 to = State.Player.Pos - e.Pos;
                float dist = to.Length();
                if (dist > 0.01f) e.Pos += to / dist * Math.Min(dist, def.Speed * dt);
                if (Vector2.Distance(e.Pos, State.Player.Pos) < playerRadius + e.Radius)
                    State.Player.Hp -= def.ContactDps * dt;
            }
        }

        void UpdateProjectiles()
        {
            for (int i = 0; i < State.Projectiles.Count; i++)
            {
                ProjectileState p = State.Projectiles[i];
                Vector2 step = p.Vel * dt;
                p.Pos += step;
                p.RangeLeft -= step.Length();

                bool gone = false;
                Grid.Query(p.Pos, p.Radius, scratch);
                if (scratch.Count > 0)
                {
                    // 여러 적에 걸쳤으면 Id 가 가장 작은 적 하나만 맞힌다
                    EnemyState hit = scratch[0];
                    hit.Hp -= p.Damage;
                    Resolver.ContactEnemy(hit, p.Substance);
                    gone = true;
                }
                else if (!p.Substance.IsEmpty)
                {
                    foreach (ZoneState z in State.Zones)
                    {
                        if (z.Removed) continue;
                        float limit = z.Radius + p.Radius;
                        if (Vector2.DistanceSquared(z.Pos, p.Pos) > limit * limit) continue;
                        if (Resolver.ContactZone(z, p.Substance, p.Pos)) { gone = true; break; }
                    }
                }

                bool outside = p.Pos.X < 0f || p.Pos.Y < 0f || p.Pos.X > stage.Width || p.Pos.Y > stage.Height;
                if (gone || outside || p.RangeLeft <= 0f)
                {
                    State.Projectiles.RemoveAt(i);
                    i--;
                }
            }
        }

        void UpdateZones()
        {
            // 순회 중에는 목록을 바꾸지 않는다. 사라질 영역은 표시만 하고 끝에서 치운다
            int count = State.Zones.Count;
            for (int i = 0; i < count; i++)
            {
                ZoneState z = State.Zones[i];
                if (z.Removed) continue;
                if (z.TimeLeft >= 0f)
                {
                    z.TimeLeft -= dt;
                    if (z.TimeLeft <= 0f) { z.Removed = true; continue; }
                }

                z.ContactTimer -= dt;
                if (z.ContactTimer > 0f) continue;
                z.ContactTimer = contactInterval;

                Grid.Query(z.Pos, z.Radius, scratch);
                foreach (EnemyState e in scratch)
                {
                    e.Hp -= z.Damage;
                    if (Resolver.ContactEnemy(e, z.Substance)) { z.Removed = true; break; }
                }
                if (z.Removed || z.Substance.IsEmpty) continue;

                for (int j = 0; j < count; j++)
                {
                    ZoneState other = State.Zones[j];
                    if (other.Removed || other.Id <= z.Id) continue;
                    float limit = z.Radius + other.Radius;
                    if (Vector2.DistanceSquared(z.Pos, other.Pos) > limit * limit) continue;
                    if (Resolver.ContactZone(other, z.Substance, (z.Pos + other.Pos) / 2f)) { z.Removed = true; break; }
                }
            }
            Resolver.SweepZones();
        }

        void UpdateEnemySubstances()
        {
            foreach (EnemyState e in State.Enemies)
            {
                // 음수로 시작한 물질은 적이 원래 지닌 것이라 줄이지 않는다
                if (e.Substance.IsEmpty || e.Substance.TimeLeft < 0f) continue;
                e.Substance.TimeLeft -= dt;
                if (e.Substance.TimeLeft <= 0f) e.Substance = new Held();
            }
        }

        void RemoveDeadEnemies()
        {
            for (int i = 0; i < State.Enemies.Count; i++)
            {
                EnemyState e = State.Enemies[i];
                if (e.Hp > 0f) continue;
                State.Enemies.RemoveAt(i);
                i--;
                State.Kills++;
                Events.Add(new RunEvent { Type = RunEventType.EnemyDied, Id = e.DefId, Pos = e.Pos });
                // 먹어치운다. 몸 종류가 지닌 방식과, 처음부터 지녔던 물질이 게이지에 쌓인다
                Absorb(data.Enemy(e.DefId).Delivery);
                Absorb(e.InnateSubstance);
            }
        }
    }
}
