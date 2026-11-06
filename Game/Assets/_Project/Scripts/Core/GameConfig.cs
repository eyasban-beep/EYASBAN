using System;

namespace Closet.Core
{
    /// <summary>장비 부위 8개. 아바타 레이어와 1:1 대응.</summary>
    public enum Slot { Weapon = 0, Head, Top, Bottom, Gloves, Shoes, Necklace, Back }

    /// <summary>부가 옵션 종류.</summary>
    public enum SubStatType { CritRate = 0, CritDamage, AttackSpeed, Lifesteal, DoubleHit, GoldBonus }

    /// <summary>
    /// 밸런스 수치와 공식 모음. 숫자를 바꾸고 싶으면 여기만 고치면 된다.
    /// (나중에 구글시트 → JSON 테이블로 옮길 예정)
    /// </summary>
    public static class GameConfig
    {
        public const int SlotCount = 8;
        public const int MaxTier = 10;
        public const int MaxMimicLevel = 60;
        public const int StagesPerChapter = 10;
        public const int ChapterCount = 12;
        public const int StagesPerDifficulty = StagesPerChapter * ChapterCount; // 120
        public const int WavesPerStage = 5;
        public const int MonstersPerWave = 3;

        public const int StartingKeys = 30;
        public const double PlayerBaseAtk = 10;
        public const double PlayerBaseHp = 100;
        public const double PlayerBaseAttackInterval = 1.0; // 초당 1회
        public const double PlayerBaseCritDamage = 1.5;      // 치명타 150%
        public const double MinAttackInterval = 0.25;

        public const double MonsterAttackInterval = 1.0;
        public const double BossAttackInterval = 1.2;
        public const double BossHpMult = 12;
        public const double BossAtkMult = 3;
        public const double BossTimeLimit = 60;
        public const double FinalBossTimeLimit = 90;

        public const double KeyDropChance = 0.05;
        public const int KeysPerStageClear = 1;
        public const int KeysPerBossClear = 3;

        public const long OfflineCapSeconds = 12 * 3600;
        public const double OfflineEfficiency = 0.5;

        public static readonly string[] TierNames =
        {
            "(없음)", "나무·가죽", "청동", "철", "강철", "미스릴", "오리하르콘", "정령석", "용린", "성휘", "신화"
        };

        public static readonly string[] SlotNames =
        {
            "무기", "머리장식", "상의", "하의", "장갑", "신발", "목걸이", "망토"
        };

        public static readonly string[] SubStatNames =
        {
            "치명타 확률", "치명타 피해", "공격 속도", "생명력 흡수", "연속 공격", "골드 획득"
        };

        public static readonly string[] ChapterNames =
        {
            "시작의 마을", "바람의 초원", "속삭이는 숲", "요정의 깊은 숲", "수정 동굴", "드워프 폐광",
            "황금 사막", "잊혀진 피라미드", "은빛 대하", "폭풍의 바다", "마계 해안", "마왕성"
        };

        public static readonly string[] BossNames =
        {
            "고블린 대장 그룩", "거대 늑대", "엘더 트렌트", "타락한 요정여왕", "수정 거미", "광산 골렘왕",
            "샌드웜", "미라 여왕", "수호룡 실버린", "크라켄", "흑기사 발데르", "마왕 벨제리아"
        };

        public static readonly string[] DifficultyNames = { "노멀", "하드", "지옥" };

        // ───────── 장비 ─────────

        public static bool IsAttackSlot(Slot s)
        {
            return s == Slot.Weapon || s == Slot.Gloves || s == Slot.Necklace || s == Slot.Back;
        }

        /// <summary>부위별 기본 수치 (공격 부위는 공격력, 방어 부위는 체력).</summary>
        public static double SlotBase(Slot s)
        {
            switch (s)
            {
                case Slot.Weapon: return 10;
                case Slot.Gloves: return 4;
                case Slot.Necklace: return 6;
                case Slot.Back: return 5;
                case Slot.Head: return 40;
                case Slot.Top: return 80;
                case Slot.Bottom: return 60;
                case Slot.Shoes: return 30;
                default: return 0;
            }
        }

        public static double TierMult(int tier) => Math.Pow(1.9, tier - 1);

        public static double MainStat(Slot slot, int tier, int level)
        {
            if (tier <= 0) return 0;
            return SlotBase(slot) * TierMult(tier) * (1 + 0.08 * level);
        }

        public static int SubStatLines(int tier) => tier >= 8 ? 3 : tier >= 5 ? 2 : tier >= 3 ? 1 : 0;

        /// <summary>부가 옵션 수치 범위 (티어가 높을수록 커짐).</summary>
        public static double RollSubStat(SubStatType type, int tier, Rng rng)
        {
            double min, max;
            switch (type)
            {
                case SubStatType.CritRate: min = 0.01; max = 0.03; break;
                case SubStatType.CritDamage: min = 0.05; max = 0.15; break;
                case SubStatType.AttackSpeed: min = 0.02; max = 0.05; break;
                case SubStatType.Lifesteal: min = 0.01; max = 0.03; break;
                case SubStatType.DoubleHit: min = 0.02; max = 0.05; break;
                default: min = 0.03; max = 0.08; break; // GoldBonus
            }
            double scale = 1 + 0.15 * Math.Max(0, tier - 3);
            return Math.Round((min + (max - min) * rng.NextDouble()) * scale, 4);
        }

        public static long SellPrice(ItemInstance item)
        {
            if (item == null || item.IsEmpty) return 0;
            return (long)Math.Ceiling(5 * TierMult(item.tier) * (1 + 0.1 * item.level));
        }

        // ───────── 클로제(옷장 미믹) ─────────

        /// <summary>
        /// 클로제 레벨별 티어 확률(가중치). 레벨이 오를수록 중심 티어가 올라가는 종 모양 분포.
        /// 원작 수치가 아닌 자체 공식.
        /// </summary>
        public static double[] TierWeights(int mimicLevel)
        {
            var w = new double[MaxTier + 1];
            double center = 1 + (mimicLevel - 1) / 6.0;
            int maxTier = Math.Min(MaxTier, (int)Math.Floor(center) + 2);
            double sum = 0;
            for (int t = 1; t <= maxTier; t++)
            {
                double d = t - center;
                w[t] = Math.Exp(-(d * d) / (2 * 0.8 * 0.8));
                sum += w[t];
            }
            for (int t = 1; t <= maxTier; t++) w[t] /= sum;
            return w;
        }

        public static int ItemLevelMin(int mimicLevel) => Math.Max(1, mimicLevel * 2 - 2);
        public static int ItemLevelMax(int mimicLevel) => mimicLevel * 2 + 2;

        public static long MimicUpgradeGold(int level) => (long)(100 * Math.Pow(1.35, level - 1));
        public static long MimicUpgradeSeconds(int level) => (long)(20 * Math.Pow(level, 1.6));

        // ───────── 스테이지 ─────────

        /// <summary>난이도까지 합친 누적 스테이지 번호 (몬스터 강함 계산용).</summary>
        public static int ScalingIndex(int difficulty, int stage) => difficulty * StagesPerDifficulty + stage;

        public static double MonsterHp(int idx) => 50 * Math.Pow(1.13, idx);
        public static double MonsterAtk(int idx) => 8 * Math.Pow(1.12, idx);
        public static double GoldPerKill(int idx) => 5 * Math.Pow(1.10, idx);

        public static bool IsBossStage(int stage) => stage % StagesPerChapter == 0;
        public static int ChapterOf(int stage) => (stage - 1) / StagesPerChapter + 1;
        public static int SubStageOf(int stage) => (stage - 1) % StagesPerChapter + 1;

        public static string DifficultyName(int difficulty)
        {
            return difficulty < DifficultyNames.Length ? DifficultyNames[difficulty] : "심연 " + (difficulty - DifficultyNames.Length + 1);
        }

        public static string StageLabel(int difficulty, int stage)
        {
            return DifficultyName(difficulty) + " " + ChapterOf(stage) + "-" + SubStageOf(stage) + " " + ChapterNames[ChapterOf(stage) - 1];
        }
    }
}
