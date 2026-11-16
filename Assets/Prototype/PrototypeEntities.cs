// 시제품 전용 코드. 재미 확인이 끝나면 통째로 지운다
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>적 하나의 상태</summary>
    public class Enemy
    {
        public GameObject Go;
        public SpriteRenderer Sr;
        public Vector2 Pos;
        public float Hp;
        public float Speed;
        public float Radius = 0.35f;
        public float BurnTime;
        public float SlowTime;
        public float PoisonTime;
        public int PoisonStacks;
        public float FlashTime;
        public bool Dead;
    }

    /// <summary>날아가는 탄 하나</summary>
    public class Projectile
    {
        public GameObject Go;
        public Vector2 Pos;
        public Vector2 Vel;
        public float Speed;
        public float Radius;
        public float Damage;
        public float Life;
        public RuneId Element;
        public bool Homing;
        public bool CanSplit;
        // 남은 관통 횟수. 0 이면 처음 맞힌 적에서 사라진다
        public int Pierce;
        // 같은 적을 두 번 맞히지 않기 위한 기록
        public readonly HashSet<Enemy> Hit = new HashSet<Enemy>();
    }

    /// <summary>퍼져 나가는 고리 하나</summary>
    public class RingFx
    {
        public GameObject Go;
        public SpriteRenderer Sr;
        public Vector2 Center;
        // true 면 플레이어를 따라다닌다
        public bool Follow;
        public float MaxRadius;
        public float Time;
        public float Duration;
        public float Damage;
        public RuneId Element;
        public bool SplitOnEnd;
        public readonly HashSet<Enemy> Hit = new HashSet<Enemy>();
    }

    /// <summary>잠깐 보였다 사라지는 그림 (광선, 번개 연결선)</summary>
    public class FadeFx
    {
        public GameObject Go;
        public SpriteRenderer Sr;
        public Color BaseColor;
        public float Time;
        public float Duration;
    }

    /// <summary>바닥에 떨어진 룬</summary>
    public class Pickup
    {
        public GameObject Go;
        public Vector2 Pos;
        public RuneId Id;
        public bool Taken;
    }

    /// <summary>시간차로 실행할 일 (다중 고리 연속 발사 등)</summary>
    public class Delayed
    {
        public float Time;
        public System.Action Action;
    }
}
