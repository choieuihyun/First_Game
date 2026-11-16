using System;
using System.Collections.Generic;
using System.Numerics;
using Game.Data;

namespace Game.Core
{
    /// <summary>신체 부위마다 쿨타임을 돌리고 자동으로 발사한다</summary>
    public sealed class SpellCaster
    {
        readonly GameData data;
        readonly RunState state;
        readonly SpatialGrid grid;
        readonly List<RunEvent> events;
        readonly ReactionResolver resolver;
        readonly int maxProjectiles;
        readonly float upgradeDamage;
        readonly float upgradeSize;
        readonly List<EnemyState> scratch = new List<EnemyState>();
        readonly List<EnemyState> hits = new List<EnemyState>();
        readonly int upgradeStep;
        readonly float boltSpread;
        readonly float cooldownCut;

        public SpellCaster(GameData data, RunState state, SpatialGrid grid, List<RunEvent> events, ReactionResolver resolver)
        {
            this.data = data;
            this.state = state;
            this.grid = grid;
            this.events = events;
            this.resolver = resolver;
            maxProjectiles = (int)data.Balance("max_projectiles");
            upgradeDamage = data.Balance("upgrade_damage");
            upgradeSize = data.Balance("upgrade_size");
            upgradeStep = Math.Max(1, (int)data.Balance("upgrade_step"));
            boltSpread = data.Balance("upgrade_bolt_spread");
            cooldownCut = data.Balance("upgrade_cooldown_cut");
        }

        /// <summary>모든 부위를 한 틱 진행한다. 팔은 자기 쪽의 적을 노리고 꼬리는 뒤에 남긴다</summary>
        public void Update(float dt)
        {
            for (int part = 0; part < state.Slots.Length; part++)
            {
                SpellSlot slot = state.Slots[part];
                if (slot.Cooldown > 0f) slot.Cooldown -= dt;
                if (slot.Cooldown > 0f || slot.DeliveryId == null) continue;

                DeliveryDef def = data.Delivery(slot.DeliveryId);
                Vector2 origin = state.Player.Pos;
                Vector2 direction;
                Vector2 zonePos;
                if (part == BodyPart.Tail)
                {
                    // 꼬리는 조준하지 않는다. 근처에 적이 있을 때만 뒤쪽에 쏘거나 지나온 자리에 남긴다
                    if (grid.Nearest(origin, def.Range, null) == null) continue;
                    direction = -state.Player.Facing;
                    zonePos = state.Player.Trail.Count > 0 ? state.Player.Trail[0] : origin;
                }
                else
                {
                    // 범위 안 자기 쪽에 적이 없으면 쏘지 않고 기다린다
                    EnemyState target = NearestOnSide(origin, def.Range, part == BodyPart.LeftArm);
                    if (target == null) continue;
                    Vector2 to = target.Pos - origin;
                    float len = to.Length();
                    direction = len > 0.0001f ? to / len : state.Player.Facing;
                    zonePos = target.Pos;
                }

                Held held = slot.SubstanceId == null
                    ? new Held()
                    : new Held { Id = slot.SubstanceId, TimeLeft = data.Substance(slot.SubstanceId).Duration, Depth = 0, Level = Math.Max(1, slot.SubstanceLevel) };
                int extraLevels = Math.Max(0, slot.DeliveryLevel - 1) + Math.Max(0, slot.SubstanceLevel - 1);
                float damage = def.Damage * (1f + upgradeDamage * extraLevels);
                float radius = def.Radius * (1f + upgradeSize * Math.Max(0, slot.DeliveryLevel - 1));

                // 방식을 강화하면 몇 단계마다 동작이 바뀐다. 탄환은 발 수가 늘고 나머지는 쿨타임이 준다
                int steps = Math.Max(0, slot.DeliveryLevel - 1) / upgradeStep;
                float cooldown = def.Cooldown;

                switch (def.Kind)
                {
                    case DeliveryKind.Bolt:
                    {
                        int shots = 1 + steps;
                        bool fired = false;
                        for (int i = 0; i < shots && state.Projectiles.Count < maxProjectiles; i++)
                        {
                            // 첫 발은 조준한 방향으로 곧장, 나머지는 좌우로 번갈아 벌린다
                            float angle = ((i + 1) / 2) * boltSpread * ((i & 1) == 1 ? 1f : -1f);
                            state.Projectiles.Add(new ProjectileState
                            {
                                Id = state.NextId++,
                                Pos = origin,
                                Vel = Rotate(direction, angle) * def.Speed,
                                Radius = radius,
                                Damage = damage,
                                RangeLeft = def.Range,
                                Substance = held
                            });
                            fired = true;
                        }
                        if (!fired) continue;
                        break;
                    }
                    case DeliveryKind.Zone:
                        resolver.SpawnZone(zonePos, radius, def.ZoneDuration, damage, held);
                        cooldown *= 1f - cooldownCut * steps;
                        break;
                    case DeliveryKind.Beam:
                        FireBeam(origin, direction, def.Range, radius, damage, held);
                        cooldown *= 1f - cooldownCut * steps;
                        break;
                    case DeliveryKind.Nova:
                        FireNova(zonePos, radius, damage, held);
                        cooldown *= 1f - cooldownCut * steps;
                        break;
                }
                slot.Cooldown = Math.Max(0.05f, cooldown);
            }
        }

        /// <summary>광선. 선 위의 모든 적과 영역에 한꺼번에 닿는다</summary>
        void FireBeam(Vector2 from, Vector2 direction, float length, float halfWidth, float damage, Held held)
        {
            Vector2 to = from + direction * length;
            events.Add(new RunEvent { Type = RunEventType.Beam, Id = held.Id, Pos = from, Pos2 = to, Radius = halfWidth });

            grid.Query((from + to) / 2f, length / 2f + halfWidth, scratch);
            hits.Clear();
            foreach (EnemyState e in scratch)
                if (DistanceToSegment(e.Pos, from, to) <= halfWidth + e.Radius) hits.Add(e);
            foreach (EnemyState e in hits)
            {
                Combat.Damage(e, damage);
                resolver.ContactEnemy(e, held);
            }
            if (held.IsEmpty) return;
            foreach (ZoneState z in state.Zones)
            {
                if (z.Removed) continue;
                if (DistanceToSegment(z.Pos, from, to) <= halfWidth + z.Radius) resolver.ContactZone(z, held, z.Pos);
            }
        }

        /// <summary>파동. 한 점 둘레의 모든 적과 영역에 한꺼번에 닿는다</summary>
        void FireNova(Vector2 center, float radius, float damage, Held held)
        {
            events.Add(new RunEvent { Type = RunEventType.Nova, Id = held.Id, Pos = center, Radius = radius });
            grid.Query(center, radius, scratch);
            hits.Clear();
            hits.AddRange(scratch);
            foreach (EnemyState e in hits)
            {
                Combat.Damage(e, damage);
                resolver.ContactEnemy(e, held);
            }
            if (held.IsEmpty) return;
            foreach (ZoneState z in state.Zones)
            {
                if (z.Removed) continue;
                float limit = radius + z.Radius;
                if (Vector2.DistanceSquared(z.Pos, center) <= limit * limit) resolver.ContactZone(z, held, z.Pos);
            }
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSq = ab.LengthSquared();
            float t = lengthSq < 0.000001f ? 0f : Math.Min(1f, Math.Max(0f, Vector2.Dot(p - a, ab) / lengthSq));
            return Vector2.Distance(p, a + ab * t);
        }

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            double rad = degrees * Math.PI / 180.0;
            float c = (float)Math.Cos(rad), sn = (float)Math.Sin(rad);
            return new Vector2(v.X * c - v.Y * sn, v.X * sn + v.Y * c);
        }

        /// <summary>앞 방향을 기준으로 왼쪽 또는 오른쪽 반쪽에 있는 가장 가까운 적. 정면과 정후방의 적은 양쪽 모두에 속한다</summary>
        EnemyState NearestOnSide(Vector2 from, float range, bool left)
        {
            const float Epsilon = 0.0001f;
            Vector2 facing = state.Player.Facing;
            grid.Query(from, range, scratch);
            EnemyState best = null;
            float bestSq = range * range;
            for (int i = 0; i < scratch.Count; i++)
            {
                EnemyState e = scratch[i];
                Vector2 d = e.Pos - from;
                float sq = d.LengthSquared();
                if (sq > range * range) continue;
                // 외적이 양수면 앞 방향의 왼쪽이다
                float cross = facing.X * d.Y - facing.Y * d.X;
                if (left ? cross < -Epsilon : cross > Epsilon) continue;
                if (best == null || sq < bestSq) { best = e; bestSq = sq; }
            }
            return best;
        }
    }
}
