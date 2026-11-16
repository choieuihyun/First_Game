using System.Collections.Generic;
using System.Numerics;
using Game.Data;

namespace Game.Core
{
    /// <summary>마법 세트의 쿨타임을 돌리고 자동으로 발사한다</summary>
    public sealed class SpellCaster
    {
        readonly GameData data;
        readonly RunState state;
        readonly SpatialGrid grid;
        readonly List<RunEvent> events;
        readonly ReactionResolver resolver;
        readonly int maxProjectiles;

        public SpellCaster(GameData data, RunState state, SpatialGrid grid, List<RunEvent> events, ReactionResolver resolver)
        {
            this.data = data;
            this.state = state;
            this.grid = grid;
            this.events = events;
            this.resolver = resolver;
            maxProjectiles = (int)data.Balance("max_projectiles");
        }

        /// <summary>모든 세트를 한 틱 진행한다</summary>
        public void Update(float dt)
        {
            foreach (SpellSlot slot in state.Slots)
            {
                if (slot.Cooldown > 0f) slot.Cooldown -= dt;
                if (slot.Cooldown > 0f || slot.DeliveryId == null) continue;

                DeliveryDef def = data.Delivery(slot.DeliveryId);
                // 범위 안에 적이 없으면 쏘지 않고 기다린다. 헛방을 줄이기 위한 것
                EnemyState target = grid.Nearest(state.Player.Pos, def.Range, null);
                if (target == null) continue;

                Held held = slot.SubstanceId == null
                    ? new Held()
                    : new Held { Id = slot.SubstanceId, TimeLeft = data.Substance(slot.SubstanceId).Duration, Depth = 0 };

                switch (def.Kind)
                {
                    case DeliveryKind.Bolt:
                        if (state.Projectiles.Count >= maxProjectiles) continue;
                        Vector2 to = target.Pos - state.Player.Pos;
                        float len = to.Length();
                        Vector2 dir = len > 0.0001f ? to / len : new Vector2(1f, 0f);
                        state.Projectiles.Add(new ProjectileState
                        {
                            Id = state.NextId++,
                            Pos = state.Player.Pos,
                            Vel = dir * def.Speed,
                            Radius = def.Radius,
                            Damage = def.Damage,
                            RangeLeft = def.Range,
                            Substance = held
                        });
                        break;
                    case DeliveryKind.ZoneAtTarget:
                        resolver.SpawnZone(target.Pos, def.Radius, def.ZoneDuration, def.Damage, held);
                        break;
                    case DeliveryKind.ZoneAtSelf:
                        resolver.SpawnZone(state.Player.Pos, def.Radius, def.ZoneDuration, def.Damage, held);
                        break;
                }
                slot.Cooldown = def.Cooldown;
            }
        }
    }
}
