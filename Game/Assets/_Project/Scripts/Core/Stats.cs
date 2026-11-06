using System;

namespace Closet.Core
{
    /// <summary>장비를 모두 합친 최종 전투 스탯.</summary>
    public class PlayerStats
    {
        public double Atk;
        public double MaxHp;
        public double AttackInterval;
        public double CritRate;
        public double CritDamage;
        public double Lifesteal;
        public double DoubleHit;
        public double GoldBonus;

        /// <summary>초당 기대 피해량.</summary>
        public double Dps => Atk * (1 + Math.Min(1, CritRate) * (CritDamage - 1)) * (1 + Math.Min(1, DoubleHit)) / AttackInterval;

        /// <summary>화면에 보여주는 전투력 (비교용 단일 숫자).</summary>
        public double Power => Dps * 10 + MaxHp * (1 + Lifesteal);
    }

    public static class StatsCalculator
    {
        public static PlayerStats Compute(ItemInstance[] equipped)
        {
            var st = new PlayerStats
            {
                Atk = GameConfig.PlayerBaseAtk,
                MaxHp = GameConfig.PlayerBaseHp,
                CritDamage = GameConfig.PlayerBaseCritDamage,
            };
            double atkSpeed = 0;
            foreach (var it in equipped)
            {
                if (it == null || it.IsEmpty) continue;
                if (it.IsAttack) st.Atk += it.MainStat; else st.MaxHp += it.MainStat;
                foreach (var sub in it.subs)
                {
                    switch (sub.type)
                    {
                        case SubStatType.CritRate: st.CritRate += sub.value; break;
                        case SubStatType.CritDamage: st.CritDamage += sub.value; break;
                        case SubStatType.AttackSpeed: atkSpeed += sub.value; break;
                        case SubStatType.Lifesteal: st.Lifesteal += sub.value; break;
                        case SubStatType.DoubleHit: st.DoubleHit += sub.value; break;
                        case SubStatType.GoldBonus: st.GoldBonus += sub.value; break;
                    }
                }
            }
            st.AttackInterval = Math.Max(GameConfig.MinAttackInterval, GameConfig.PlayerBaseAttackInterval / (1 + atkSpeed));
            return st;
        }

        /// <summary>이 장비를 끼면 전투력이 얼마가 되는지.</summary>
        public static double PowerIfEquipped(ItemInstance[] equipped, ItemInstance candidate)
        {
            var copy = (ItemInstance[])equipped.Clone();
            copy[(int)candidate.slot] = candidate;
            return Compute(copy).Power;
        }
    }
}
