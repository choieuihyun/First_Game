using System.Collections.Generic;

namespace Game.Data
{
    /// <summary>물질 하나의 정의. 색과 모양 둘 다로 구분한다</summary>
    public sealed class SubstanceDef
    {
        public string Id { get; }
        public string Name { get; }
        public string Color { get; }
        public string Shape { get; }
        /// <summary>적에게 묻었을 때 남아 있는 시간 (초)</summary>
        public float Duration { get; }
        /// <summary>룬으로 떨어질 수 있는 물질인지. 반응 결과로만 생기는 물질은 false</summary>
        public bool IsRune { get; }

        public SubstanceDef(string id, string name, string color, string shape, float duration, bool isRune)
        {
            Id = id; Name = name; Color = color; Shape = shape; Duration = duration; IsRune = isRune;
        }
    }

    /// <summary>마법이 물질을 전달하는 방법</summary>
    public enum DeliveryKind { Bolt, Zone }

    /// <summary>방식 하나의 정의</summary>
    public sealed class DeliveryDef
    {
        public string Id { get; }
        public string Name { get; }
        public DeliveryKind Kind { get; }
        public float Cooldown { get; }
        /// <summary>탄은 맞을 때 피해, 영역은 접촉 간격마다 주는 피해</summary>
        public float Damage { get; }
        public float Speed { get; }
        public float Radius { get; }
        /// <summary>이 거리 안에 적이 있어야 쏜다. 탄은 이 거리만큼 날아간다</summary>
        public float Range { get; }
        public float ZoneDuration { get; }

        public DeliveryDef(string id, string name, DeliveryKind kind, float cooldown, float damage, float speed,
            float radius, float range, float zoneDuration)
        {
            Id = id; Name = name; Kind = kind; Cooldown = cooldown; Damage = damage; Speed = speed;
            Radius = radius; Range = range; ZoneDuration = zoneDuration;
        }
    }

    /// <summary>두 물질이 만나면 일어나는 반응 하나</summary>
    public sealed class ReactionDef
    {
        public string Id { get; }
        public string Name { get; }
        public string A { get; }
        public string B { get; }
        public int Priority { get; }
        public string EffectId { get; }

        public ReactionDef(string id, string name, string a, string b, int priority, string effectId)
        {
            Id = id; Name = name; A = a; B = b; Priority = priority; EffectId = effectId;
        }
    }

    /// <summary>효과 조각 종류. 반응 결과는 이 조각들을 조합해 만든다</summary>
    public enum EffectPiece { Damage, ApplySubstance, SpawnZone, Explode, Chain }

    /// <summary>효과의 한 단계</summary>
    public sealed class EffectStep
    {
        public EffectPiece Piece { get; }
        public float Amount { get; }
        public float Radius { get; }
        public int Count { get; }
        /// <summary>없으면 null</summary>
        public string Substance { get; }
        public float Duration { get; }

        public EffectStep(EffectPiece piece, float amount, float radius, int count, string substance, float duration)
        {
            Piece = piece; Amount = amount; Radius = radius; Count = count; Substance = substance; Duration = duration;
        }
    }

    /// <summary>효과 하나. 단계를 순서대로 실행한다</summary>
    public sealed class EffectDef
    {
        public string Id { get; }
        public IReadOnlyList<EffectStep> Steps { get; }

        public EffectDef(string id, IReadOnlyList<EffectStep> steps)
        {
            Id = id; Steps = steps;
        }
    }

    /// <summary>적의 몸 종류 하나. 먹으면 이 몸이 지닌 방식을 얻는다</summary>
    public sealed class EnemyDef
    {
        public string Id { get; }
        public string Name { get; }
        /// <summary>이 몸 종류를 많이 잡으면 얻는 방식</summary>
        public string Delivery { get; }
        public float Hp { get; }
        public float Speed { get; }
        public float Radius { get; }
        public float ContactDps { get; }
        public string Color { get; }
        public string Shape { get; }

        public EnemyDef(string id, string name, string delivery, float hp, float speed, float radius, float contactDps,
            string color, string shape)
        {
            Id = id; Name = name; Delivery = delivery; Hp = hp; Speed = speed; Radius = radius; ContactDps = contactDps;
            Color = color; Shape = shape;
        }
    }

    /// <summary>스테이지에 나오는 적 한 종류. 몸 종류와 처음부터 지닌 물질의 짝</summary>
    public sealed class StageEnemy
    {
        public string Body { get; }
        /// <summary>없으면 null</summary>
        public string Substance { get; }
        public float Weight { get; }

        public StageEnemy(string body, string substance, float weight)
        {
            Body = body; Substance = substance; Weight = weight;
        }
    }

    /// <summary>스테이지 (판 시작 때 고르는 맵) 하나의 정의</summary>
    public sealed class StageDef
    {
        public string Id { get; }
        public string Name { get; }
        public float Width { get; }
        public float Height { get; }
        /// <summary>이 맵에서 흡수할 수 있는 특징 (방식 id 또는 물질 id) 과 처음 필요한 처치 수</summary>
        public IReadOnlyList<KeyValuePair<string, float>> Absorb { get; }
        /// <summary>나오는 적</summary>
        public IReadOnlyList<StageEnemy> Enemies { get; }
        /// <summary>지형이 지닌 물질. 없으면 null</summary>
        public string TerrainSubstance { get; }
        public int TerrainCount { get; }
        public float TerrainRadius { get; }
        /// <summary>판을 시작할 때 오른팔에 붙어 있는 방식</summary>
        public string StartDelivery { get; }

        public StageDef(string id, string name, float width, float height, IReadOnlyList<KeyValuePair<string, float>> absorb,
            IReadOnlyList<StageEnemy> enemies, string terrainSubstance, int terrainCount, float terrainRadius,
            string startDelivery)
        {
            Id = id; Name = name; Width = width; Height = height; Absorb = absorb; Enemies = enemies;
            TerrainSubstance = terrainSubstance; TerrainCount = terrainCount; TerrainRadius = terrainRadius;
            StartDelivery = startDelivery;
        }

        /// <summary>특징이 흡수 목록의 몇 번째인지. 없으면 -1</summary>
        public int AbsorbIndex(string trait)
        {
            if (trait == null) return -1;
            for (int i = 0; i < Absorb.Count; i++)
                if (Absorb[i].Key == trait) return i;
            return -1;
        }
    }
}
