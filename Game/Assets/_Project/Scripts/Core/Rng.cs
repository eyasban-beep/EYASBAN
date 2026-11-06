using System;

namespace Closet.Core
{
    /// <summary>
    /// 시드 고정 난수 (xorshift64*). 같은 시드면 같은 결과 → 나중에 서버 검증에 사용.
    /// UnityEngine.Random 대신 이걸 쓴다.
    /// </summary>
    public class Rng
    {
        ulong _state;

        public Rng(long seed)
        {
            _state = (ulong)seed;
            if (_state == 0) _state = 0x9E3779B97F4A7C15UL;
        }

        public long State => (long)_state;

        public ulong NextULong()
        {
            _state ^= _state >> 12;
            _state ^= _state << 25;
            _state ^= _state >> 27;
            return _state * 0x2545F4914F6CDD1DUL;
        }

        /// <summary>0 이상 1 미만.</summary>
        public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

        /// <summary>min 이상 max 이하 정수.</summary>
        public int Range(int min, int maxInclusive)
        {
            if (maxInclusive <= min) return min;
            return min + (int)(NextULong() % (ulong)(maxInclusive - min + 1));
        }

        public bool Chance(double p) => NextDouble() < p;

        /// <summary>가중치 배열에서 인덱스 하나 뽑기.</summary>
        public int Weighted(double[] weights)
        {
            double total = 0;
            for (int i = 0; i < weights.Length; i++) total += weights[i];
            double r = NextDouble() * total;
            for (int i = 0; i < weights.Length; i++)
            {
                r -= weights[i];
                if (r < 0 && weights[i] > 0) return i;
            }
            for (int i = weights.Length - 1; i >= 0; i--) if (weights[i] > 0) return i;
            throw new InvalidOperationException("All weights are zero");
        }
    }
}
