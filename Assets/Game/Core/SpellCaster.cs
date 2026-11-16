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
                    : new Held { Id = slot.SubstanceId, TimeLeft = data.Substance(slot.SubstanceId).Duration, Depth = 0 };
                int extraLevels = Math.Max(0, slot.DeliveryLevel - 1) + Math.Max(0, slot.SubstanceLevel - 1);
                float damage = def.Damage * (1f + upgradeDamage * extraLevels);
                float radius = def.Radius * (1f + upgradeSize * Math.Max(0, slot.DeliveryLevel - 1));

                if (def.Kind == DeliveryKind.Bolt)
                {
                    if (state.Projectiles.Count >= maxProjectiles) continue;
                    state.Projectiles.Add(new ProjectileState
                    {
                        Id = state.NextId++,
                        Pos = origin,
                        Vel = direction * def.Speed,
                        Radius = radius,
                        Damage = damage,
                        RangeLeft = def.Range,
                        Substance = held
                    });
                }
                else
                {
                    resolver.SpawnZone(zonePos, radius, def.ZoneDuration, damage, held);
                }
                slot.Cooldown = def.Cooldown;
            }
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
