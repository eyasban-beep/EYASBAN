using System;
using System.Collections.Generic;

namespace Closet.Core
{
    public struct OfflineReward
    {
        public long Seconds;
        public long Gold;
        public long Keys;
        public bool IsEmpty => Gold <= 0 && Keys <= 0;
    }

    /// <summary>
    /// 게임 한 판 전체를 묶는 객체. Unity 쪽은 이것만 들고 있으면 된다.
    /// 시간은 밖에서 넣어준다(nowUnix) → 테스트·서버에서도 같은 코드 사용 가능.
    /// </summary>
    public class GameSession
    {
        public PlayerState State { get; }
        public BattleSimulator Battle { get; }
        public IReadOnlyList<string> Log => _log;

        readonly Rng _rng;
        readonly List<string> _log = new List<string>();
        double _accumulator;

        public GameSession(PlayerState state)
        {
            State = state;
            State.EnsureValid();
            _rng = new Rng(State.rngState);
            Battle = new BattleSimulator(State, _rng);
            Battle.OnEvent += HandleBattleEvent;
        }

        public PlayerStats Stats => Battle.Stats;

        /// <summary>매 프레임 호출. deltaSeconds만큼 전투를 고정 간격으로 진행.</summary>
        public void Tick(double deltaSeconds, long nowUnix)
        {
            _accumulator += Math.Min(deltaSeconds, 1.0); // 렉 걸려도 한 번에 1초까지만
            while (_accumulator >= BattleSimulator.StepSeconds)
            {
                _accumulator -= BattleSimulator.StepSeconds;
                Battle.Step();
            }
            if (MimicService.UpdateUpgrade(State, nowUnix))
                AddLog("클로제가 Lv." + State.mimicLevel + "(으)로 성장했다!");
            State.lastSeenUnix = nowUnix;
            State.rngState = _rng.State;
        }

        // ───────── 클로제 ─────────

        public FeedResult Feed()
        {
            var r = MimicService.Feed(State, _rng);
            if (r == FeedResult.Ok) AddLog("클로제가 [" + State.pending.Name + "]을(를) 뱉었다!");
            return r;
        }

        public void EquipPending()
        {
            string name = State.pending.Name;
            long gold = MimicService.EquipPending(State);
            Battle.RefreshStats();
            AddLog(name + " 장착" + (gold > 0 ? " (기존 장비 판매 +" + gold + "G)" : ""));
        }

        public void SellPending()
        {
            string name = State.pending.Name;
            long gold = MimicService.SellPending(State);
            AddLog(name + " 판매 +" + gold + "G");
        }

        public void AutoResolve()
        {
            if (!State.hasPending) return;
            if (MimicService.AutoResolve(State)) { Battle.RefreshStats(); AddLog("자동 장착 (전투력 상승)"); }
        }

        public bool StartMimicUpgrade(long nowUnix)
        {
            bool ok = MimicService.TryStartUpgrade(State, nowUnix);
            if (ok) AddLog("클로제 레벨업 시작!");
            return ok;
        }

        public void ChallengeBoss() => Battle.ChallengeBoss();

        // ───────── 오프라인 보상 ─────────

        /// <summary>마지막 접속 이후 시간만큼 보상 계산 (적용은 ApplyOffline).</summary>
        public OfflineReward CalculateOffline(long nowUnix)
        {
            long secs = Math.Max(0, Math.Min(GameConfig.OfflineCapSeconds, nowUnix - State.lastSeenUnix));
            if (secs < 60) return new OfflineReward();
            int idx = GameConfig.ScalingIndex(State.difficulty, State.stage);
            double killsPerSec = Math.Min(0.5, Stats.Dps / GameConfig.MonsterHp(idx));
            double kills = killsPerSec * secs * GameConfig.OfflineEfficiency;
            return new OfflineReward
            {
                Seconds = secs,
                Gold = (long)(kills * GameConfig.GoldPerKill(idx) * (1 + Stats.GoldBonus)),
                Keys = (long)(kills * GameConfig.KeyDropChance),
            };
        }

        public void ApplyOffline(OfflineReward r, long nowUnix)
        {
            State.gold += r.Gold;
            State.keys += r.Keys;
            State.lastSeenUnix = nowUnix;
            if (!r.IsEmpty) AddLog("오프라인 보상: +" + r.Gold + "G, 열쇠 +" + r.Keys);
        }

        void HandleBattleEvent(BattleEvent e)
        {
            switch (e.Type)
            {
                case BattleEventType.StageCleared:
                    AddLog("스테이지 클리어! 열쇠 +" + e.Keys);
                    break;
                case BattleEventType.BossKilled:
                    AddLog("보스 처치!");
                    break;
                case BattleEventType.PlayerDied:
                    AddLog("쓰러졌다... 다시 도전");
                    break;
                case BattleEventType.BossTimeout:
                    AddLog("보스 제한시간 초과");
                    break;
            }
        }

        void AddLog(string msg)
        {
            _log.Add(msg);
            if (_log.Count > 30) _log.RemoveAt(0);
        }
    }
}
