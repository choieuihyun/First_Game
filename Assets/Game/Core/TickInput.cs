using System.Numerics;

namespace Game.Core
{
    /// <summary>한 틱 동안 플레이어가 내린 명령</summary>
    public struct TickInput
    {
        /// <summary>부위 선택이 없음</summary>
        public const int NoChoice = -2;
        /// <summary>맨 앞 특징을 버림</summary>
        public const int Discard = -1;

        /// <summary>이동 방향. 길이가 1 을 넘으면 1 로 줄인다</summary>
        public Vector2 Move;
        /// <summary>NoChoice, Discard, 또는 붙일 부위 번호 (BodyPart)</summary>
        public int PartChoice;

        /// <summary>아무 명령도 없는 입력</summary>
        public static TickInput None => new TickInput { Move = Vector2.Zero, PartChoice = NoChoice };
    }
}
