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
    public enum DeliveryKind { Bolt, ZoneAtTarget, ZoneAtSelf }

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

    /// <summary>적 하나의 정의</summary>
    public sealed class EnemyDef
    {
        public string Id { get; }
        public string Name { get; }
        public float Hp { get; }
        public float Speed { get; }
        public float Radius { get; }
        public float ContactDps { get; }
        /// <summary>처음부터 지닌 물질. 없으면 null</summary>
        public string Substance { get; }
        public string Color { get; }
        public string Shape { get; }

        public EnemyDef(string id, string name, float hp, float speed, float radius, float contactDps,
            string substance, string color, string shape)
        {
            Id = id; Name = name; Hp = hp; Speed = speed; Radius = radius; ContactDps = contactDps;
            Substance = substance; Color = color; Shape = shape;
        }
    }

    /// <summary>스테이지 (판 시작 때 고르는 맵) 하나의 정의</summary>
    public sealed class StageDef
    {
        public string Id { get; }
        public string Name { get; }
        public float Width { get; }
        public float Height { get; }
        /// <summary>이 맵에서 떨어지는 룬. 방식 id 또는 물질 id</summary>
        public IReadOnlyList<string> Runes { get; }
        /// <summary>나오는 적과 가중치</summary>
        public IReadOnlyList<KeyValuePair<string, float>> Enemies { get; }
        /// <summary>지형이 지닌 물질. 없으면 null</summary>
        public string TerrainSubstance { get; }
        public int TerrainCount { get; }
        public float TerrainRadius { get; }
        /// <summary>판을 시작할 때 첫 세트에 끼워져 있는 방식</summary>
        public string StartDelivery { get; }

        public StageDef(string id, string name, float width, float height, IReadOnlyList<string> runes,
            IReadOnlyList<KeyValuePair<string, float>> enemies, string terrainSubstance, int terrainCount, float terrainRadius,
            string startDelivery)
        {
            StartDelivery = startDelivery;
            Id = id; Name = name; Width = width; Height = height; Runes = runes; Enemies = enemies;
            TerrainSubstance = terrainSubstance; TerrainCount = terrainCount; TerrainRadius = terrainRadius;
        }
    }
}
