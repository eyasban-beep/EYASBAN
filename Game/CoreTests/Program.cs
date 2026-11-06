using System;
using System.Linq;
using Closet.Core;

// 핵심 로직 자동 점검 + 밸런스 시뮬레이션.
static class Program
{
    static int _fail;
    static void Check(bool ok, string name) { Console.WriteLine((ok ? "  PASS " : "  FAIL ") + name); if (!ok) _fail++; }

    static int Main()
    {
        Console.WriteLine("[1] 티어 확률 합 = 1, 레벨이 오르면 평균 티어 상승");
        double prevAvg = 0;
        foreach (int lv in new[] { 1, 10, 20, 30, 40, 50, 60 })
        {
            var w = GameConfig.TierWeights(lv);
            double sum = w.Sum(), avg = 0;
            for (int t = 0; t < w.Length; t++) avg += t * w[t];
            Console.WriteLine($"    클로제 Lv{lv,2}: 평균 티어 {avg:F2}  " + string.Join(" ", Enumerable.Range(1, 10).Select(t => $"T{t}:{w[t] * 100:F0}%")));
            Check(Math.Abs(sum - 1) < 1e-9 && avg >= prevAvg, $"Lv{lv} 분포 정상");
            prevAvg = avg;
        }

        Console.WriteLine("[2] 같은 시드 = 같은 결과 (서버 검증 전제)");
        var a = new Rng(42); var b = new Rng(42);
        Check(Enumerable.Range(0, 1000).All(_ => a.NextULong() == b.NextULong()), "Rng 결정성");

        Console.WriteLine("[3] 먹이기/장착/판매 규칙");
        var s = PlayerState.NewGame(0, 7);
        var rng = new Rng(7);
        long k0 = s.keys;
        Check(MimicService.Feed(s, rng) == FeedResult.Ok && s.keys == k0 - 1 && s.hasPending, "먹이면 열쇠 -1, 장비 대기");
        Check(MimicService.Feed(s, rng) == FeedResult.MustResolvePending, "대기 장비 처리 전엔 못 먹임");
        var slot = s.pending.slot;
        MimicService.EquipPending(s);
        Check(!s.equipped[(int)slot].IsEmpty && !s.hasPending, "장착됨");
        s.keys = 0;
        Check(MimicService.Feed(s, rng) == FeedResult.NoKeys, "열쇠 없으면 실패");

        Console.WriteLine("[4] 클로제 레벨업 타이머");
        s.gold = 1_000_000;
        Check(MimicService.TryStartUpgrade(s, 100), "업그레이드 시작");
        Check(!MimicService.UpdateUpgrade(s, 101), "시간 전엔 그대로");
        Check(MimicService.UpdateUpgrade(s, 100 + GameConfig.MimicUpgradeSeconds(1)) && s.mimicLevel == 2, "시간 지나면 Lv2");

        Console.WriteLine("[5] 자동 플레이 시뮬레이션 (자동 먹이기 + 자동 장착 + 레벨업 즉시완료, 게임 시간 4시간)");
        var st = PlayerState.NewGame(0, 2026);
        var game = new GameSession(st);
        long now = 0;
        for (int sec = 1; sec <= 4 * 3600; sec++)
        {
            now = sec;
            game.Tick(1.0, now);
            while (st.keys > 0) { game.Feed(); game.AutoResolve(); }
            if (game.StartMimicUpgrade(now)) MimicService.FinishUpgradeNow(st);
            if (!st.autoChallengeBoss && sec % 60 == 0) game.ChallengeBoss();
            if (sec % 1800 == 0)
                Console.WriteLine($"    {sec / 3600.0:F1}h  {GameConfig.StageLabel(st.difficulty, st.stage),-22} 클로제 Lv{st.mimicLevel,2}  전투력 {game.Stats.Power,12:N0}  먹이기 {st.totalFeeds,5}회  골드 {st.gold,10:N0}");
        }
        Check(st.stage > 10 || st.difficulty > 0, "4시간 안에 1챕터 이상 진행");

        Console.WriteLine("[6] 오프라인 보상");
        var off = game.CalculateOffline(now + 8 * 3600);
        Console.WriteLine($"    8시간 오프라인: {off.Gold:N0}G, 열쇠 {off.Keys}");
        Check(off.Seconds == 8 * 3600 && off.Gold > 0, "오프라인 보상 계산");
        Check(game.CalculateOffline(now + 100 * 3600).Seconds == GameConfig.OfflineCapSeconds, "12시간 상한");

        Console.WriteLine(_fail == 0 ? "\n모든 점검 통과" : $"\n실패 {_fail}개");
        return _fail == 0 ? 0 : 1;
    }
}
