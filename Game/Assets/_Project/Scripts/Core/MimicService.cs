using System;
using System.Collections.Generic;

namespace Closet.Core
{
    public enum FeedResult { Ok, NoKeys, MustResolvePending }

    /// <summary>
    /// 클로제(옷장 미믹) 핵심 루프: 열쇠 먹이기 → 장비 뱉기 → 장착/판매, 클로제 레벨업.
    /// </summary>
    public static class MimicService
    {
        public static ItemInstance CreateItem(int mimicLevel, Rng rng, long uid)
        {
            int tier = rng.Weighted(GameConfig.TierWeights(mimicLevel));
            var slot = (Slot)rng.Range(0, GameConfig.SlotCount - 1);
            int level = rng.Range(GameConfig.ItemLevelMin(mimicLevel), GameConfig.ItemLevelMax(mimicLevel));

            int lines = GameConfig.SubStatLines(tier);
            var pool = new List<SubStatType>((SubStatType[])Enum.GetValues(typeof(SubStatType)));
            var subs = new SubStat[lines];
            for (int i = 0; i < lines; i++)
            {
                int pick = rng.Range(0, pool.Count - 1);
                var type = pool[pick];
                pool.RemoveAt(pick);
                subs[i] = new SubStat { type = type, value = GameConfig.RollSubStat(type, tier, rng) };
            }
            return new ItemInstance { uid = uid, slot = slot, tier = tier, level = level, subs = subs };
        }

        public static FeedResult Feed(PlayerState s, Rng rng)
        {
            if (s.hasPending) return FeedResult.MustResolvePending;
            if (s.keys <= 0) return FeedResult.NoKeys;
            s.keys--;
            s.pending = CreateItem(s.mimicLevel, rng, s.nextUid++);
            s.hasPending = true;
            s.totalFeeds++;
            return FeedResult.Ok;
        }

        /// <summary>대기 중인 장비를 장착. 기존 장비는 자동 판매.</summary>
        public static long EquipPending(PlayerState s)
        {
            if (!s.hasPending) return 0;
            int idx = (int)s.pending.slot;
            var old = s.equipped[idx];
            s.equipped[idx] = s.pending;
            long gold = GameConfig.SellPrice(old);
            s.gold += gold;
            ClearPending(s);
            return gold;
        }

        public static long SellPending(PlayerState s)
        {
            if (!s.hasPending) return 0;
            long gold = GameConfig.SellPrice(s.pending);
            s.gold += gold;
            ClearPending(s);
            return gold;
        }

        /// <summary>자동 모드: 전투력이 오르면 장착, 아니면 판매. 장착했으면 true.</summary>
        public static bool AutoResolve(PlayerState s)
        {
            if (!s.hasPending) return false;
            double now = StatsCalculator.Compute(s.equipped).Power;
            double after = StatsCalculator.PowerIfEquipped(s.equipped, s.pending);
            if (after > now) { EquipPending(s); return true; }
            SellPending(s);
            return false;
        }

        static void ClearPending(PlayerState s)
        {
            s.pending = new ItemInstance();
            s.hasPending = false;
        }

        // ───────── 클로제 레벨업 ─────────

        public static bool IsUpgrading(PlayerState s) => s.mimicUpgradeEndUnix > 0;

        public static bool CanStartUpgrade(PlayerState s)
        {
            return !IsUpgrading(s) && s.mimicLevel < GameConfig.MaxMimicLevel && s.gold >= GameConfig.MimicUpgradeGold(s.mimicLevel);
        }

        public static bool TryStartUpgrade(PlayerState s, long nowUnix)
        {
            if (!CanStartUpgrade(s)) return false;
            s.gold -= GameConfig.MimicUpgradeGold(s.mimicLevel);
            s.mimicUpgradeEndUnix = nowUnix + GameConfig.MimicUpgradeSeconds(s.mimicLevel);
            return true;
        }

        /// <summary>업그레이드 시간이 끝났으면 레벨 +1. 레벨이 올랐으면 true.</summary>
        public static bool UpdateUpgrade(PlayerState s, long nowUnix)
        {
            if (!IsUpgrading(s) || nowUnix < s.mimicUpgradeEndUnix) return false;
            s.mimicLevel = Math.Min(GameConfig.MaxMimicLevel, s.mimicLevel + 1);
            s.mimicUpgradeEndUnix = 0;
            return true;
        }

        /// <summary>테스트용: 남은 시간 즉시 완료 (나중에 보석 소모로 바꿀 자리).</summary>
        public static void FinishUpgradeNow(PlayerState s)
        {
            if (!IsUpgrading(s)) return;
            s.mimicUpgradeEndUnix = 1; // UpdateUpgrade에서 바로 완료 처리
        }
    }
}
