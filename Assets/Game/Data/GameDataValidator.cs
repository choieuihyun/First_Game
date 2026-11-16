using System.Collections.Generic;

namespace Game.Data
{
    /// <summary>읽은 데이터가 서로 맞는지 검사한다. 문제를 사람이 읽을 문장으로 돌려준다</summary>
    public static class GameDataValidator
    {
        /// <summary>코드가 쓰는 전역 수치. 하나라도 없으면 문제다</summary>
        public static readonly string[] RequiredBalanceKeys =
        {
            "tick_rate", "chain_depth_limit", "contact_interval", "max_reactions_per_tick",
            "max_enemies", "max_zones", "max_projectiles",
            "player_hp", "player_speed", "player_radius",
            "absorb_growth", "upgrade_max", "upgrade_damage", "upgrade_size", "tail_delay",
            "upgrade_step", "upgrade_bolt_spread", "upgrade_cooldown_cut",
            "charge_interval", "charge_duration", "charge_speed", "zigzag_angle", "orbit_distance", "orbit_closing", "orbit_closing_growth",
            "spawn_distance", "spawn_interval_start", "spawn_interval_min", "spawn_interval_decay_per_sec",
            "spawn_count_step_min", "enemy_hp_growth", "grid_cell_size"
        };

        /// <summary>물질과 적에 쓸 수 있는 모양</summary>
        public static readonly string[] Shapes =
            { "triangle", "drop", "cloud", "bolt", "ring", "square", "circle", "diamond", "hex", "cross", "star" };

        /// <summary>몸 종류가 움직이는 방식</summary>
        public static readonly string[] Behaviors = { "chase", "charge", "zigzag", "orbit" };

        /// <summary>상태 조각이 걸 수 있는 상태</summary>
        public static readonly string[] Statuses = { "slow", "freeze", "dot", "vulnerable" };

        /// <summary>문제 목록을 돌려준다. 비어 있으면 정상</summary>
        public static List<string> Validate(GameData d)
        {
            var p = new List<string>();

            foreach (string key in RequiredBalanceKeys)
                if (!d.HasBalance(key)) p.Add("balance.csv: 필요한 key 가 없습니다: " + key);
            if (p.Count == 0)
            {
                foreach (string key in new[] { "tick_rate", "contact_interval", "player_hp", "grid_cell_size", "spawn_count_step_min", "spawn_interval_min" })
                    if (d.Balance(key) <= 0f) p.Add("balance.csv: " + key + " 는 0 보다 커야 합니다");
                // 개수로 쓰는 값은 1 이상이어야 한다. 0 이면 반응이 영영 처리되지 않는 식으로 조용히 망가진다
                foreach (string key in new[] { "upgrade_max", "upgrade_step", "absorb_growth", "max_enemies", "max_zones", "max_projectiles", "max_reactions_per_tick", "chain_depth_limit" })
                    if (d.Balance(key) < 1f) p.Add("balance.csv: " + key + " 는 1 이상이어야 합니다");
            }

            var shapeOwner = new Dictionary<string, string>();
            foreach (SubstanceDef s in d.Substances)
            {
                if (!IsColor(s.Color)) p.Add("substances.csv: " + s.Id + " 의 color 는 #RRGGBB 형식이어야 합니다: " + s.Color);
                if (!IsShape(s.Shape)) p.Add("substances.csv: " + s.Id + " 의 shape 를 모릅니다: " + s.Shape);
                // 색을 구분하기 어려운 사람도 모양만으로 물질을 알아볼 수 있어야 한다
                if (shapeOwner.ContainsKey(s.Shape))
                    p.Add("substances.csv: " + s.Id + " 와 " + shapeOwner[s.Shape] + " 의 shape 가 같습니다: " + s.Shape);
                else shapeOwner[s.Shape] = s.Id;
                if (s.Duration <= 0f) p.Add("substances.csv: " + s.Id + " 의 duration 은 0 보다 커야 합니다");
            }

            foreach (DeliveryDef v in d.Deliveries)
            {
                // 룬 id 하나로 방식과 물질을 구분하므로 둘이 같은 id 를 쓰면 안 된다
                if (d.HasSubstance(v.Id)) p.Add("deliveries.csv: " + v.Id + " 는 물질 id 와 겹칩니다");
                if (v.Cooldown <= 0f) p.Add("deliveries.csv: " + v.Id + " 의 cooldown 은 0 보다 커야 합니다");
                if (v.Radius <= 0f) p.Add("deliveries.csv: " + v.Id + " 의 radius 는 0 보다 커야 합니다");
                if (v.Range <= 0f) p.Add("deliveries.csv: " + v.Id + " 의 range 는 0 보다 커야 합니다");
                if (v.Kind == DeliveryKind.Bolt && v.Speed <= 0f) p.Add("deliveries.csv: " + v.Id + " 의 speed 는 0 보다 커야 합니다");
                if (d.BodyForDelivery(v.Id) == null) p.Add("deliveries.csv: " + v.Id + " 를 지닌 몸 종류가 enemies.csv 에 없습니다");
                if (v.Kind == DeliveryKind.Zone && v.ZoneDuration <= 0f) p.Add("deliveries.csv: " + v.Id + " 의 zone_duration 은 0 보다 커야 합니다");
            }

            var pairs = new Dictionary<string, string>();
            foreach (ReactionDef r in d.Reactions)
            {
                if (!d.HasSubstance(r.A)) p.Add("reactions.csv: " + r.Id + " 의 a 가 없는 물질입니다: " + r.A);
                if (!d.HasSubstance(r.B)) p.Add("reactions.csv: " + r.Id + " 의 b 가 없는 물질입니다: " + r.B);
                if (!d.HasEffect(r.EffectId)) p.Add("reactions.csv: " + r.Id + " 의 effect 가 없습니다: " + r.EffectId);
                string lo = string.CompareOrdinal(r.A, r.B) <= 0 ? r.A : r.B;
                string hi = lo == r.A ? r.B : r.A;
                string key = lo + "+" + hi + "@" + r.Priority;
                if (pairs.ContainsKey(key))
                    p.Add("reactions.csv: " + r.Id + " 와 " + pairs[key] + " 는 같은 물질 쌍에 priority 도 같습니다");
                else pairs[key] = r.Id;
            }

            foreach (EffectDef e in d.Effects)
            {
                foreach (EffectStep s in e.Steps)
                {
                    if (s.Substance != null && !d.HasSubstance(s.Substance))
                        p.Add("effects.csv: " + e.Id + " 의 substance 가 없는 물질입니다: " + s.Substance);
                    bool needsRadius = s.Piece == EffectPiece.Explode || s.Piece == EffectPiece.Chain || s.Piece == EffectPiece.SpawnZone;
                    if (needsRadius && s.Radius <= 0f) p.Add("effects.csv: " + e.Id + " 의 radius 는 0 보다 커야 합니다");
                    if (s.Piece == EffectPiece.Chain && s.Count < 1) p.Add("effects.csv: " + e.Id + " 의 chain 은 count 가 1 이상이어야 합니다");
                    bool needsSubstance = s.Piece == EffectPiece.SpawnZone || s.Piece == EffectPiece.ApplySubstance;
                    if (needsSubstance && s.Substance == null) p.Add("effects.csv: " + e.Id + " 에 substance 가 필요합니다");
                    if (s.Piece == EffectPiece.Push && s.Radius <= 0f) p.Add("effects.csv: " + e.Id + " 의 push 는 radius 가 0 보다 커야 합니다");
                    if (s.Piece == EffectPiece.Push && s.Amount == 0f) p.Add("effects.csv: " + e.Id + " 의 push 는 amount 가 0 이 아니어야 합니다");
                    if (s.Piece == EffectPiece.Status)
                    {
                        bool known = false;
                        foreach (string name in Statuses) if (name == s.Status) known = true;
                        if (!known) p.Add("effects.csv: " + e.Id + " 의 status 를 모릅니다: " + s.Status);
                        if (s.Duration <= 0f) p.Add("effects.csv: " + e.Id + " 의 status 는 duration 이 0 보다 커야 합니다");
                        if (s.Status == "slow" && (s.Amount <= 0f || s.Amount >= 1f)) p.Add("effects.csv: " + e.Id + " 의 slow 는 amount 가 0 과 1 사이여야 합니다 (남는 속도 비율)");
                        if (s.Status == "vulnerable" && s.Amount <= 1f) p.Add("effects.csv: " + e.Id + " 의 vulnerable 은 amount 가 1 보다 커야 합니다 (피해 배율)");
                        if (s.Status == "dot" && s.Amount <= 0f) p.Add("effects.csv: " + e.Id + " 의 dot 은 amount 가 0 보다 커야 합니다 (초당 피해)");
                    }
                    if (s.Piece == EffectPiece.SpawnZone && s.Duration <= 0f) p.Add("effects.csv: " + e.Id + " 의 spawn_zone 은 duration 이 0 보다 커야 합니다");
                }
            }

            foreach (EnemyDef e in d.Enemies)
            {
                if (e.Hp <= 0f) p.Add("enemies.csv: " + e.Id + " 의 hp 는 0 보다 커야 합니다");
                if (e.Speed <= 0f) p.Add("enemies.csv: " + e.Id + " 의 speed 는 0 보다 커야 합니다");
                if (e.Radius <= 0f) p.Add("enemies.csv: " + e.Id + " 의 radius 는 0 보다 커야 합니다");
                bool knownBehavior = false;
                foreach (string b in Behaviors) if (b == e.Behavior) knownBehavior = true;
                if (!knownBehavior) p.Add("enemies.csv: " + e.Id + " 의 behavior 를 모릅니다: " + e.Behavior);
                if (!d.IsDelivery(e.Delivery)) p.Add("enemies.csv: " + e.Id + " 의 delivery 가 없는 방식입니다: " + e.Delivery);
                if (!IsColor(e.Color)) p.Add("enemies.csv: " + e.Id + " 의 color 는 #RRGGBB 형식이어야 합니다: " + e.Color);
                if (!IsShape(e.Shape)) p.Add("enemies.csv: " + e.Id + " 의 shape 를 모릅니다: " + e.Shape);
            }

            if (d.Stages.Count == 0) p.Add("stages.csv: 스테이지가 하나도 없습니다");
            foreach (StageDef s in d.Stages)
            {
                if (s.Width <= 0f || s.Height <= 0f) p.Add("stages.csv: " + s.Id + " 의 width 와 height 는 0 보다 커야 합니다");
                if (!d.IsDelivery(s.StartDelivery)) p.Add("stages.csv: " + s.Id + " 의 start_delivery 가 없는 방식입니다: " + s.StartDelivery);
                if (s.Enemies.Count == 0) p.Add("stages.csv: " + s.Id + " 의 enemies 가 비어 있습니다");
                foreach (StageEnemy e in s.Enemies)
                {
                    if (!d.HasEnemy(e.Body)) p.Add("stages.csv: " + s.Id + " 의 enemies 에 없는 몸 종류가 있습니다: " + e.Body);
                    if (e.Substance != null && !d.HasSubstance(e.Substance)) p.Add("stages.csv: " + s.Id + " 의 enemies 에 없는 물질이 있습니다: " + e.Substance);
                    if (e.Weight <= 0f) p.Add("stages.csv: " + s.Id + " 의 적 가중치는 0 보다 커야 합니다: " + e.Body);
                }
                if (s.Absorb.Count == 0) p.Add("stages.csv: " + s.Id + " 의 absorb 가 비어 있습니다");
                var seenTraits = new HashSet<string>();
                foreach (KeyValuePair<string, float> a in s.Absorb)
                {
                    if (!seenTraits.Add(a.Key)) p.Add("stages.csv: " + s.Id + " 의 absorb 에 같은 특징이 두 번 있습니다: " + a.Key);
                    if (a.Value < 1f) p.Add("stages.csv: " + s.Id + " 의 absorb 필요한 수는 1 이상이어야 합니다: " + a.Key);
                    if (!d.IsDelivery(a.Key) && !d.HasSubstance(a.Key)) { p.Add("stages.csv: " + s.Id + " 의 absorb 에 없는 id 가 있습니다: " + a.Key); continue; }
                    if (!d.IsDelivery(a.Key) && !d.Substance(a.Key).IsRune) p.Add("stages.csv: " + s.Id + " 의 absorb 에 흡수할 수 없는 물질이 있습니다: " + a.Key);
                    // 그 특징을 지닌 적이 이 맵에 나와야 게이지를 채울 수 있다
                    bool obtainable = false;
                    foreach (StageEnemy e in s.Enemies)
                    {
                        if (e.Substance == a.Key) obtainable = true;
                        if (d.HasEnemy(e.Body) && d.Enemy(e.Body).Delivery == a.Key) obtainable = true;
                    }
                    if (!obtainable) p.Add("stages.csv: " + s.Id + " 에는 " + a.Key + " 를 지닌 적이 나오지 않아 얻을 수 없습니다");
                }
                if (s.TerrainSubstance != null && !d.HasSubstance(s.TerrainSubstance))
                    p.Add("stages.csv: " + s.Id + " 의 terrain_substance 가 없는 물질입니다: " + s.TerrainSubstance);
                if (s.TerrainCount > 0 && s.TerrainRadius <= 0f) p.Add("stages.csv: " + s.Id + " 의 terrain_radius 는 0 보다 커야 합니다");
            }

            return p;
        }

        static bool IsShape(string shape)
        {
            foreach (string s in Shapes)
                if (s == shape) return true;
            return false;
        }

        static bool IsColor(string c)
        {
            if (c == null || c.Length != 7 || c[0] != '#') return false;
            for (int i = 1; i < 7; i++)
            {
                char ch = c[i];
                bool hex = (ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F');
                if (!hex) return false;
            }
            return true;
        }
    }
}
