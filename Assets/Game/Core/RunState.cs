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
    public enum RunPhase { Playing, ChoosingPart, Dead }

    /// <summary>신체 부위 번호. 마법 세트 하나가 부위 하나다</summary>
    public static class BodyPart
    {
        public const int LeftArm = 0;
        public const int RightArm = 1;
        public const int Tail = 2;
        public const int Count = 3;
    }

    public sealed class PlayerState
    {
        public Vector2 Pos;
        public float Hp;
        /// <summary>앞 방향. 마지막으로 움직인 쪽이다. 팔의 왼쪽과 오른쪽, 꼬리의 뒤가 이것으로 정해진다</summary>
        public Vector2 Facing = new Vector2(0f, 1f);
        /// <summary>최근에 지나온 자리. 맨 앞이 가장 오래된 자리다. 꼬리가 여기에 남긴다</summary>
        public List<Vector2> Trail = new List<Vector2>();
    }

    public sealed class EnemyState
    {
        public int Id;
        public string DefId;
        public Vector2 Pos;
        public float Hp;
        public float Radius;
        public Held Substance;
        /// <summary>처음부터 지닌 물질. 잡았을 때 포식 게이지는 이것으로 센다. 없으면 null</summary>
        public string InnateSubstance;
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

    /// <summary>신체 부위 하나에 붙은 마법. 방식과 물질, 그리고 각각의 강화 단계</summary>
    public sealed class SpellSlot
    {
        public string DeliveryId;
        public string SubstanceId;
        public float Cooldown;
        public int DeliveryLevel;
        public int SubstanceLevel;
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
        /// <summary>흡수했지만 아직 어느 부위에 붙일지 고르지 않은 특징</summary>
        public List<string> PendingTraits = new List<string>();
        /// <summary>특징별로 지금까지 채운 포식 게이지. 순서는 스테이지의 absorb 목록과 같다</summary>
        public int[] AbsorbCount = new int[0];
        /// <summary>특징별로 지금까지 얻은 횟수. 얻을수록 다음에 필요한 수가 늘어난다</summary>
        public int[] AbsorbAcquired = new int[0];
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
