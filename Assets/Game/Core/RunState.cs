using System.Collections.Generic;
using System.Numerics;
using Game.Data;

namespace Game.Core
{
    /// <summary>대상에 묻어 있는 물질 하나. Id 가 null 이면 아무것도 없다</summary>
    public struct Held
    {
        public string Id;
        /// <summary>남은 시간 (초). 음수면 저절로 사라지지 않는다</summary>
        public float TimeLeft;
        /// <summary>몇 단계 연쇄로 생긴 물질인지. 마법, 지형, 적이 원래 지닌 것은 0</summary>
        public int Depth;

        public bool IsEmpty => Id == null;
    }

    /// <summary>판의 진행 단계</summary>
    public enum RunPhase { Playing, ChoosingRune, Dead }

    public sealed class PlayerState
    {
        public Vector2 Pos;
        public float Hp;
    }

    public sealed class EnemyState
    {
        public int Id;
        public string DefId;
        public Vector2 Pos;
        public float Hp;
        public float Radius;
        public Held Substance;
    }

    public sealed class ProjectileState
    {
        public int Id;
        public Vector2 Pos;
        public Vector2 Vel;
        public float Radius;
        public float Damage;
        /// <summary>앞으로 더 날아갈 수 있는 거리</summary>
        public float RangeLeft;
        public Held Substance;
    }

    /// <summary>전장에 남아 있는 영역. 구름, 장판, 지형 웅덩이</summary>
    public sealed class ZoneState
    {
        public int Id;
        public Vector2 Pos;
        public float Radius;
        /// <summary>남은 시간 (초). 음수면 지형이라 저절로 사라지지 않는다</summary>
        public float TimeLeft;
        /// <summary>접촉 간격마다 안에 있는 적에게 주는 피해</summary>
        public float Damage;
        /// <summary>다음 접촉 처리까지 남은 시간</summary>
        public float ContactTimer;
        public Held Substance;
        /// <summary>이번 틱에 사라지기로 한 영역. 순회 중 목록을 바꾸지 않으려고 표시만 해 두고 틱 끝에 치운다. 저장하지 않는다</summary>
        public bool Removed;
    }

    public sealed class PickupState
    {
        public int Id;
        public Vector2 Pos;
        public string RuneId;
    }

    /// <summary>마법 세트 하나. 방식과 물질을 끼운다</summary>
    public sealed class SpellSlot
    {
        public string DeliveryId;
        public string SubstanceId;
        public float Cooldown;
    }

    /// <summary>일어나기로 정해졌지만 아직 처리하지 않은 반응</summary>
    public sealed class PendingReaction
    {
        public string ReactionId;
        public Vector2 Pos;
        public int Depth;
        /// <summary>반응이 일어난 적. 없으면 -1</summary>
        public int TargetEnemyId;
    }

    /// <summary>판 하나의 전체 상태. 이것만 저장하면 판을 그대로 이어갈 수 있다. 로직은 넣지 않는다</summary>
    public sealed class RunState
    {
        public int Tick;
        public Rng Rng;
        public RunPhase Phase;
        public string StageId;
        public int NextId;
        public PlayerState Player = new PlayerState();
        public SpellSlot[] Slots = new SpellSlot[0];
        public List<EnemyState> Enemies = new List<EnemyState>();
        public List<ProjectileState> Projectiles = new List<ProjectileState>();
        public List<ZoneState> Zones = new List<ZoneState>();
        public List<PickupState> Pickups = new List<PickupState>();
        /// <summary>주웠지만 아직 어디에 끼울지 고르지 않은 룬</summary>
        public List<string> PendingRunes = new List<string>();
        public List<PendingReaction> ReactionQueue = new List<PendingReaction>();
        public float SpawnTimer;
        public int Kills;
        public int ReactionCount;

        /// <summary>판이 시작된 뒤 흐른 시간 (초)</summary>
        public float Seconds(GameData data)
        {
            return Tick / data.Balance("tick_rate");
        }
    }
}
