namespace Game.Core
{
    /// <summary>시드 랜덤 생성기. 판 안의 모든 랜덤은 이것 하나에서 나온다. State 만 저장하면 그대로 이어진다</summary>
    public struct Rng
    {
        // xorshift32 는 상태가 0 이면 계속 0 만 나온다. 그래서 0 은 이 값으로 바꾼다
        const uint ZeroSeedReplacement = 0x9E3779B9u;

        /// <summary>현재 상태. 저장과 복원 대상</summary>
        public uint State;

        public Rng(uint seed)
        {
            State = seed == 0 ? ZeroSeedReplacement : seed;
        }

        /// <summary>다음 32비트 값</summary>
        public uint NextUInt()
        {
            uint x = State == 0 ? ZeroSeedReplacement : State;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            State = x;
            return x;
        }

        /// <summary>0 이상 1 미만의 값</summary>
        public float NextFloat()
        {
            // float 가 정확히 담을 수 있는 24비트만 쓴다. 그래야 1 이 나오지 않는다
            return (NextUInt() >> 8) / 16777216f;
        }

        /// <summary>0 이상 maxExclusive 미만의 정수</summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 1) return 0;
            return (int)(NextUInt() % (uint)maxExclusive);
        }

        /// <summary>min 이상 max 미만의 값</summary>
        public float Range(float min, float max)
        {
            return min + (max - min) * NextFloat();
        }
    }
}
