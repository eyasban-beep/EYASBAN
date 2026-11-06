using System;

namespace Closet.Core
{
    [Serializable]
    public class SubStat
    {
        public SubStatType type;
        public double value;
    }

    /// <summary>보유 장비 1개. tier 0 = 빈 칸.</summary>
    [Serializable]
    public class ItemInstance
    {
        public long uid;
        public Slot slot;
        public int tier;
        public int level;
        public SubStat[] subs = new SubStat[0];

        public bool IsEmpty => tier <= 0;
        public double MainStat => GameConfig.MainStat(slot, tier, level);
        public bool IsAttack => GameConfig.IsAttackSlot(slot);
        public string Name => IsEmpty ? "(비어 있음)" : GameConfig.TierNames[tier] + " " + GameConfig.SlotNames[(int)slot];

        public static ItemInstance Empty(Slot slot) => new ItemInstance { slot = slot, tier = 0, level = 0 };
    }

    /// <summary>
    /// 저장되는 모든 플레이어 데이터. Unity JsonUtility로 저장하므로 public 필드만 사용.
    /// </summary>
    [Serializable]
    public class PlayerState
    {
        public int version = 1;
        public long gold;
        public long keys;
        public long gems;

        public int mimicLevel = 1;
        public long mimicUpgradeEndUnix; // 0 = 업그레이드 중 아님

        public ItemInstance[] equipped = new ItemInstance[GameConfig.SlotCount];
        public bool hasPending;
        public ItemInstance pending = new ItemInstance();

        public int difficulty;
        public int stage = 1;              // 1 ~ 120
        public bool autoChallengeBoss = true;

        public long lastSeenUnix;
        public long rngState;
        public long nextUid = 1;
        public long totalFeeds;

        public static PlayerState NewGame(long nowUnix, long seed)
        {
            var s = new PlayerState
            {
                keys = GameConfig.StartingKeys,
                lastSeenUnix = nowUnix,
                rngState = seed,
            };
            s.EnsureValid();
            return s;
        }

        /// <summary>불러온 세이브의 빈 값/깨진 값을 정리.</summary>
        public void EnsureValid()
        {
            if (equipped == null || equipped.Length != GameConfig.SlotCount)
            {
                var old = equipped;
                equipped = new ItemInstance[GameConfig.SlotCount];
                if (old != null)
                    for (int i = 0; i < Math.Min(old.Length, equipped.Length); i++) equipped[i] = old[i];
            }
            for (int i = 0; i < equipped.Length; i++)
            {
                if (equipped[i] == null) equipped[i] = ItemInstance.Empty((Slot)i);
                equipped[i].slot = (Slot)i;
                if (equipped[i].subs == null) equipped[i].subs = new SubStat[0];
            }
            if (pending == null) pending = new ItemInstance();
            if (pending.subs == null) pending.subs = new SubStat[0];
            if (pending.IsEmpty) hasPending = false;
            mimicLevel = Math.Max(1, Math.Min(GameConfig.MaxMimicLevel, mimicLevel));
            stage = Math.Max(1, Math.Min(GameConfig.StagesPerDifficulty, stage));
            if (difficulty < 0) difficulty = 0;
            if (rngState == 0) rngState = 12345;
            if (nextUid <= 0) nextUid = 1;
        }
    }
}
