using System;
using System.Collections.Generic;
using System.Globalization;

namespace Game.Data
{
    /// <summary>CSV 에서 읽은 모든 정의를 모아 둔 곳. 한 번 읽으면 바뀌지 않는다</summary>
    public sealed class GameData
    {
        /// <summary>읽어야 하는 CSV 이름 (확장자 제외)</summary>
        public static readonly string[] FileNames =
            { "substances", "deliveries", "reactions", "effects", "enemies", "stages", "balance" };

        readonly List<SubstanceDef> substances = new List<SubstanceDef>();
        readonly List<DeliveryDef> deliveries = new List<DeliveryDef>();
        readonly List<ReactionDef> reactions = new List<ReactionDef>();
        readonly List<EffectDef> effects = new List<EffectDef>();
        readonly List<EnemyDef> enemies = new List<EnemyDef>();
        readonly List<StageDef> stages = new List<StageDef>();
        readonly Dictionary<string, SubstanceDef> substanceById = new Dictionary<string, SubstanceDef>();
        readonly Dictionary<string, DeliveryDef> deliveryById = new Dictionary<string, DeliveryDef>();
        readonly Dictionary<string, EffectDef> effectById = new Dictionary<string, EffectDef>();
        readonly Dictionary<string, EnemyDef> enemyById = new Dictionary<string, EnemyDef>();
        readonly Dictionary<string, StageDef> stageById = new Dictionary<string, StageDef>();
        readonly Dictionary<string, float> balance = new Dictionary<string, float>();

        public IReadOnlyList<SubstanceDef> Substances => substances;
        public IReadOnlyList<DeliveryDef> Deliveries => deliveries;
        public IReadOnlyList<ReactionDef> Reactions => reactions;
        public IReadOnlyList<EffectDef> Effects => effects;
        public IReadOnlyList<EnemyDef> Enemies => enemies;
        public IReadOnlyList<StageDef> Stages => stages;

        GameData() { }

        /// <summary>CSV 글자를 받아 전부 읽고 검사한다. readCsvText 는 FileNames 의 이름을 받아 파일 내용을 돌려준다</summary>
        public static GameData Load(Func<string, string> readCsvText)
        {
            var d = new GameData();
            d.LoadSubstances(Table(readCsvText, "substances"));
            d.LoadDeliveries(Table(readCsvText, "deliveries"));
            d.LoadReactions(Table(readCsvText, "reactions"));
            d.LoadEffects(Table(readCsvText, "effects"));
            d.LoadEnemies(Table(readCsvText, "enemies"));
            d.LoadStages(Table(readCsvText, "stages"));
            d.LoadBalance(Table(readCsvText, "balance"));

            List<string> problems = GameDataValidator.Validate(d);
            if (problems.Count > 0) throw new DataException(problems[0]);
            return d;
        }

        static CsvTable Table(Func<string, string> readCsvText, string name)
        {
            return CsvTable.Parse(name + ".csv", readCsvText(name));
        }

        public SubstanceDef Substance(string id) => substanceById[id];
        public DeliveryDef Delivery(string id) => deliveryById[id];
        public EffectDef Effect(string id) => effectById[id];
        public EnemyDef Enemy(string id) => enemyById[id];
        public StageDef Stage(string id) => stageById[id];

        public bool HasSubstance(string id) => id != null && substanceById.ContainsKey(id);
        public bool HasEffect(string id) => id != null && effectById.ContainsKey(id);
        public bool HasEnemy(string id) => id != null && enemyById.ContainsKey(id);
        public bool HasBalance(string key) => balance.ContainsKey(key);

        /// <summary>특징 id 가 방식인지. 아니면 물질이다</summary>
        public bool IsDelivery(string traitId) => traitId != null && deliveryById.ContainsKey(traitId);

        /// <summary>전역 수치 하나를 꺼낸다</summary>
        public float Balance(string key) => balance[key];

        /// <summary>두 물질이 만났을 때의 반응. 순서는 상관없다. 여러 개면 우선순위가 가장 높은 것. 없으면 null</summary>
        public ReactionDef FindReaction(string a, string b)
        {
            if (a == null || b == null) return null;
            ReactionDef best = null;
            foreach (ReactionDef r in reactions)
            {
                bool match = (r.A == a && r.B == b) || (r.A == b && r.B == a);
                if (match && (best == null || r.Priority > best.Priority)) best = r;
            }
            return best;
        }

        static string OrNull(string s) => s.Length == 0 ? null : s;

        static void AddUnique<T>(CsvTable t, int row, Dictionary<string, T> map, string id, T def)
        {
            if (id.Length == 0) throw t.Error(row, "id", "id 가 비어 있습니다");
            if (map.ContainsKey(id)) throw t.Error(row, "id", "id 가 겹칩니다: " + id);
            map[id] = def;
        }

        void LoadSubstances(CsvTable t)
        {
            for (int r = 0; r < t.RowCount; r++)
            {
                var def = new SubstanceDef(t.Get(r, "id"), t.Get(r, "name"), t.Get(r, "color"), t.Get(r, "shape"),
                    t.GetFloat(r, "duration"), t.GetBool(r, "rune"));
                AddUnique(t, r, substanceById, def.Id, def);
                substances.Add(def);
            }
        }

        void LoadDeliveries(CsvTable t)
        {
            for (int r = 0; r < t.RowCount; r++)
            {
                DeliveryKind kind;
                switch (t.Get(r, "kind"))
                {
                    case "bolt": kind = DeliveryKind.Bolt; break;
                    case "zone": kind = DeliveryKind.Zone; break;
                    case "beam": kind = DeliveryKind.Beam; break;
                    case "nova": kind = DeliveryKind.Nova; break;
                    default: throw t.Error(r, "kind", "모르는 방식 종류입니다: " + t.Get(r, "kind"));
                }
                var def = new DeliveryDef(t.Get(r, "id"), t.Get(r, "name"), kind, t.GetFloat(r, "cooldown"),
                    t.GetFloat(r, "damage"), t.GetFloatOr(r, "speed", 0f), t.GetFloat(r, "radius"),
                    t.GetFloat(r, "range"), t.GetFloatOr(r, "zone_duration", 0f));
                AddUnique(t, r, deliveryById, def.Id, def);
                deliveries.Add(def);
            }
        }

        void LoadReactions(CsvTable t)
        {
            var seen = new Dictionary<string, ReactionDef>();
            for (int r = 0; r < t.RowCount; r++)
            {
                var def = new ReactionDef(t.Get(r, "id"), t.Get(r, "name"), t.Get(r, "a"), t.Get(r, "b"),
                    (int)t.GetFloat(r, "priority"), t.Get(r, "effect"));
                AddUnique(t, r, seen, def.Id, def);
                reactions.Add(def);
            }
        }

        void LoadEffects(CsvTable t)
        {
            // 같은 effect 이름의 행들을 order 순으로 묶는다. 처음 나온 순서를 유지한다
            var order = new List<string>();
            var steps = new Dictionary<string, List<KeyValuePair<float, EffectStep>>>();
            for (int r = 0; r < t.RowCount; r++)
            {
                string id = t.Get(r, "effect");
                if (id.Length == 0) throw t.Error(r, "effect", "effect 가 비어 있습니다");
                EffectPiece piece;
                switch (t.Get(r, "piece"))
                {
                    case "damage": piece = EffectPiece.Damage; break;
                    case "apply_substance": piece = EffectPiece.ApplySubstance; break;
                    case "spawn_zone": piece = EffectPiece.SpawnZone; break;
                    case "explode": piece = EffectPiece.Explode; break;
                    case "chain": piece = EffectPiece.Chain; break;
                    case "status": piece = EffectPiece.Status; break;
                    case "push": piece = EffectPiece.Push; break;
                    default: throw t.Error(r, "piece", "모르는 효과 조각입니다: " + t.Get(r, "piece"));
                }
                var step = new EffectStep(piece, t.GetFloatOr(r, "amount", 0f), t.GetFloatOr(r, "radius", 0f),
                    (int)t.GetFloatOr(r, "count", 0f), OrNull(t.Get(r, "substance")), t.GetFloatOr(r, "duration", 0f),
                    t.HasColumn("status") ? OrNull(t.Get(r, "status")) : null);
                if (!steps.ContainsKey(id)) { steps[id] = new List<KeyValuePair<float, EffectStep>>(); order.Add(id); }
                steps[id].Add(new KeyValuePair<float, EffectStep>(t.GetFloat(r, "order"), step));
            }
            foreach (string id in order)
            {
                List<KeyValuePair<float, EffectStep>> list = steps[id];
                // 안정 정렬. order 가 같으면 파일 순서
                var sorted = new List<EffectStep>();
                var remaining = new List<KeyValuePair<float, EffectStep>>(list);
                while (remaining.Count > 0)
                {
                    int best = 0;
                    for (int i = 1; i < remaining.Count; i++)
                        if (remaining[i].Key < remaining[best].Key) best = i;
                    sorted.Add(remaining[best].Value);
                    remaining.RemoveAt(best);
                }
                var def = new EffectDef(id, sorted);
                effectById[id] = def;
                effects.Add(def);
            }
        }

        void LoadEnemies(CsvTable t)
        {
            for (int r = 0; r < t.RowCount; r++)
            {
                var def = new EnemyDef(t.Get(r, "id"), t.Get(r, "name"), t.Get(r, "delivery"), t.Get(r, "behavior"), t.GetFloat(r, "hp"), t.GetFloat(r, "speed"),
                    t.GetFloat(r, "radius"), t.GetFloat(r, "contact_dps"), t.Get(r, "color"), t.Get(r, "shape"));
                AddUnique(t, r, enemyById, def.Id, def);
                enemies.Add(def);
            }
        }

        void LoadStages(CsvTable t)
        {
            for (int r = 0; r < t.RowCount; r++)
            {
                var absorb = new List<KeyValuePair<string, float>>();
                foreach (string item in t.GetList(r, "absorb"))
                {
                    string[] parts = item.Split(':');
                    if (parts.Length != 2 || !TryNumber(parts[1], out float need))
                        throw t.Error(r, "absorb", "특징id:필요한수 형식이어야 합니다: " + item);
                    absorb.Add(new KeyValuePair<string, float>(parts[0].Trim(), need));
                }
                var stageEnemies = new List<StageEnemy>();
                foreach (string item in t.GetList(r, "enemies"))
                {
                    // 몸종류:물질:가중치. 물질은 비워 둘 수 있다
                    string[] parts = item.Split(':');
                    if (parts.Length != 3 || !TryNumber(parts[2], out float weight))
                        throw t.Error(r, "enemies", "몸종류:물질:가중치 형식이어야 합니다: " + item);
                    stageEnemies.Add(new StageEnemy(parts[0].Trim(), OrNull(parts[1].Trim()), weight));
                }
                var def = new StageDef(t.Get(r, "id"), t.Get(r, "name"), t.GetFloat(r, "width"), t.GetFloat(r, "height"),
                    absorb, stageEnemies, OrNull(t.Get(r, "terrain_substance")),
                    (int)t.GetFloatOr(r, "terrain_count", 0f), t.GetFloatOr(r, "terrain_radius", 0f),
                    t.Get(r, "start_delivery"));
                AddUnique(t, r, stageById, def.Id, def);
                stages.Add(def);
            }
        }

        static bool TryNumber(string text, out float value)
        {
            return float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        /// <summary>그 방식을 지닌 몸 종류. 없으면 null</summary>
        public EnemyDef BodyForDelivery(string deliveryId)
        {
            foreach (EnemyDef e in enemies)
                if (e.Delivery == deliveryId) return e;
            return null;
        }

        void LoadBalance(CsvTable t)
        {
            for (int r = 0; r < t.RowCount; r++)
            {
                string key = t.Get(r, "key");
                if (key.Length == 0) throw t.Error(r, "key", "key 가 비어 있습니다");
                if (balance.ContainsKey(key)) throw t.Error(r, "key", "key 가 겹칩니다: " + key);
                balance[key] = t.GetFloat(r, "value");
            }
        }
    }
}
