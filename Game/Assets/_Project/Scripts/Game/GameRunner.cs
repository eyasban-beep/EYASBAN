using System;
using Closet.Core;
using UnityEngine;

namespace Closet.Game
{
    /// <summary>
    /// 프로토타입 실행기 + 임시 화면(OnGUI).
    /// 화면은 "되는지 확인용" 네모 박스들이다. 아트가 준비되면 uGUI + 스프라이트로 교체한다.
    /// 게임 규칙은 전부 Closet.Core 쪽에 있고, 여기서는 시간 흐르게 하기 + 그리기 + 버튼만 담당.
    /// </summary>
    public class GameRunner : MonoBehaviour
    {
        const float RefW = 1080f, RefH = 1920f; // 세로 화면 기준 해상도
        const float AutoFeedInterval = 0.3f;
        const float AutoSaveInterval = 10f;

        GameSession _game;
        bool _autoFeed;
        float _autoFeedTimer, _saveTimer;
        OfflineReward _offline;
        bool _showOffline;

        Font _font;
        GUIStyle _toastStyle, _label, _labelBig, _labelSmall, _button, _buttonBig, _center;
        bool _stylesReady;

        static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        PlayerState S => _game.State;
        static bool DevMode => Application.isEditor || Debug.isDebugBuild;

        static readonly Color[] TierColors =
        {
            new Color(0.25f, 0.25f, 0.28f), // 0 빈 칸
            new Color(0.6f, 0.6f, 0.6f),    // T1 회색
            new Color(0.35f, 0.75f, 0.35f), // T2 초록
            new Color(0.3f, 0.55f, 0.95f),  // T3 파랑
            new Color(0.65f, 0.4f, 0.9f),   // T4 보라
            new Color(0.95f, 0.6f, 0.2f),   // T5 주황
            new Color(0.9f, 0.25f, 0.25f),  // T6 빨강
            new Color(0.4f, 0.85f, 0.95f),  // T7 하늘
            new Color(0.95f, 0.8f, 0.2f),   // T8 금색
            new Color(0.95f, 0.95f, 1f),    // T9 흰빛
            new Color(1f, 0.45f, 0.85f),    // T10 무지개(분홍으로 임시)
        };

        void Awake()
        {
            Application.targetFrameRate = 60;
            _game = new GameSession(SaveSystem.LoadOrNew(Now));
            _offline = _game.CalculateOffline(Now);
            _showOffline = !_offline.IsEmpty;
            // 한글이 네모(□)로 보이면 Assets/_Project/Resources/Fonts/KoreanFont.ttf 를 넣으면 자동 적용
            _font = Resources.Load<Font>("Fonts/KoreanFont");
        }

        void Update()
        {
            _game.Tick(Time.deltaTime, Now);

            if (_autoFeed)
            {
                _autoFeedTimer -= Time.deltaTime;
                if (_autoFeedTimer <= 0)
                {
                    _autoFeedTimer = AutoFeedInterval;
                    if (S.hasPending) _game.AutoResolve();
                    else if (_game.Feed() == FeedResult.NoKeys) _autoFeed = false;
                }
            }

            _saveTimer += Time.deltaTime;
            if (_saveTimer >= AutoSaveInterval) { _saveTimer = 0; SaveSystem.Save(S); }
        }

        void OnApplicationPause(bool paused) { if (paused && _game != null) SaveSystem.Save(S); }
        void OnApplicationQuit() { if (_game != null) SaveSystem.Save(S); }

        // ───────────────────────── 화면 그리기 ─────────────────────────

        void OnGUI()
        {
            if (_game == null) return;
            BuildStyles();

            float scale = Mathf.Min(Screen.width / RefW, Screen.height / RefH);
            float offsetX = (Screen.width - RefW * scale) / 2f;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, 0, 0), Quaternion.identity, new Vector3(scale, scale, 1));

            DrawTopBar(new Rect(0, 0, RefW, 150));
            DrawBattle(new Rect(0, 150, RefW, 560));
            DrawEquipment(new Rect(0, 710, RefW, 420));
            DrawMimic(new Rect(0, 1130, RefW, 500));
            DrawLog(new Rect(0, 1630, RefW, 290));

            if (S.hasPending && !_autoFeed) DrawPendingPopup();
            if (_showOffline) DrawOfflinePopup();
            if (_toast != null) { Box(new Rect(240, 1050, 600, 100), new Color(0, 0, 0, 0.8f)); GUI.Label(new Rect(240, 1050, 600, 100), _toast, _toastStyle); }
        }

        void DrawTopBar(Rect r)
        {
            Box(r, new Color(0.08f, 0.07f, 0.12f));
            GUI.Label(new Rect(30, 15, 500, 60), "골드 " + Fmt(S.gold) + "    열쇠 " + S.keys, _labelBig);
            GUI.Label(new Rect(30, 80, 600, 50), "전투력 " + Fmt(_game.Stats.Power), _label);
            GUI.Label(new Rect(560, 15, 500, 60), GameConfig.StageLabel(S.difficulty, S.stage), _label);
        }

        void DrawBattle(Rect r)
        {
            int ch = GameConfig.ChapterOf(S.stage) - 1;
            Box(r, Color.Lerp(new Color(0.25f, 0.45f, 0.3f), new Color(0.35f, 0.1f, 0.2f), ch / 11f));
            var b = _game.Battle;

            GUI.Label(new Rect(30, r.y + 15, 700, 50),
                (b.IsBossFight ? "보스: " + GameConfig.BossNames[ch] : "웨이브 " + b.Wave + "/" + GameConfig.WavesPerStage + "  (" + b.MonsterInWave + "/" + GameConfig.MonstersPerWave + ")"),
                _label);
            if (b.IsBossFight)
                GUI.Label(new Rect(760, r.y + 15, 300, 50), "남은 시간 " + Mathf.CeilToInt((float)b.BossTimeLeft) + "초", _label);

            // 플레이어(아바타 자리표시)
            DrawPaperDoll(new Rect(120, r.y + 120, 220, 360));
            Bar(new Rect(100, r.y + 490, 260, 30), b.PlayerHp / b.Stats.MaxHp, new Color(0.3f, 0.85f, 0.4f));

            // 몬스터
            float size = b.IsBossFight ? 300 : 200;
            Box(new Rect(780 - size / 2, r.y + 480 - size, size, size), b.IsBossFight ? new Color(0.5f, 0.1f, 0.15f) : new Color(0.35f, 0.3f, 0.4f));
            GUI.Label(new Rect(780 - size / 2, r.y + 480 - size, size, size), b.IsBossFight ? "BOSS" : "몬스터", _center);
            Bar(new Rect(650, r.y + 490, 260, 30), b.MonsterHp / b.MonsterMaxHp, new Color(0.9f, 0.3f, 0.3f));

            if (!S.autoChallengeBoss && GUI.Button(new Rect(760, r.y + 70, 290, 90), "보스 도전!", _button))
                _game.ChallengeBoss();
        }

        /// <summary>장착 장비 티어 색으로 칠한 종이인형. 나중에 SD 스프라이트 겹치기로 교체.</summary>
        void DrawPaperDoll(Rect r)
        {
            Color C(Slot s) => TierColors[S.equipped[(int)s].tier];
            float x = r.x, y = r.y, w = r.width;
            Box(new Rect(x - 30, y + 90, w + 60, 170), C(Slot.Back));                 // 망토
            Box(new Rect(x + w * 0.3f, y + 10, w * 0.4f, 90), new Color(1f, 0.85f, 0.75f)); // 얼굴
            Box(new Rect(x + w * 0.25f, y, w * 0.5f, 30), C(Slot.Head));               // 머리장식
            Box(new Rect(x + w * 0.42f, y + 100, w * 0.16f, 20), C(Slot.Necklace));    // 목걸이
            Box(new Rect(x + w * 0.2f, y + 120, w * 0.6f, 110), C(Slot.Top));          // 상의
            Box(new Rect(x + w * 0.05f, y + 200, w * 0.15f, 40), C(Slot.Gloves));      // 장갑
            Box(new Rect(x + w * 0.8f, y + 200, w * 0.15f, 40), C(Slot.Gloves));
            Box(new Rect(x + w * 0.25f, y + 230, w * 0.5f, 90), C(Slot.Bottom));       // 하의
            Box(new Rect(x + w * 0.25f, y + 320, w * 0.5f, 40), C(Slot.Shoes));        // 신발
            Box(new Rect(x + w + 10, y + 60, 25, 220), C(Slot.Weapon));                // 무기
        }

        void DrawEquipment(Rect r)
        {
            Box(r, new Color(0.15f, 0.14f, 0.2f));
            float cw = 255, chh = 190, gap = 12;
            for (int i = 0; i < GameConfig.SlotCount; i++)
            {
                var it = S.equipped[i];
                float x = gap + (i % 4) * (cw + gap) + 6;
                float y = r.y + gap + (i / 4) * (chh + gap) + 5;
                Box(new Rect(x, y, cw, chh), TierColors[it.tier] * 0.8f + new Color(0, 0, 0, 0.2f));
                GUI.Label(new Rect(x + 10, y + 5, cw - 20, 40), GameConfig.SlotNames[i], _labelSmall);
                GUI.Label(new Rect(x + 10, y + 45, cw - 20, 50), it.IsEmpty ? "-" : GameConfig.TierNames[it.tier], _label);
                if (!it.IsEmpty)
                    GUI.Label(new Rect(x + 10, y + 100, cw - 20, 80), "Lv." + it.level + "\n" + (it.IsAttack ? "공격 " : "체력 ") + Fmt(it.MainStat), _labelSmall);
            }
        }

        void DrawMimic(Rect r)
        {
            Box(r, new Color(0.2f, 0.15f, 0.12f));

            // 클로제 자리표시 (레벨에 따라 색이 변함)
            float t = (S.mimicLevel - 1) / (float)(GameConfig.MaxMimicLevel - 1);
            Box(new Rect(60, r.y + 40, 300, 380), Color.Lerp(new Color(0.45f, 0.3f, 0.2f), new Color(0.95f, 0.85f, 1f), t));
            GUI.Label(new Rect(60, r.y + 40, 300, 380), "클로제\nLv." + S.mimicLevel + (S.hasPending ? "\n\n(퉤!)" : "\n\n(배고파…)"), _center);

            float bx = 400, bw = 640;
            if (GUI.Button(new Rect(bx, r.y + 40, bw, 120), "열쇠 먹이기 (" + S.keys + "개)", _buttonBig))
            {
                var res = _game.Feed();
                if (res == FeedResult.NoKeys) Toast("열쇠가 없어요");
            }

            if (GUI.Button(new Rect(bx, r.y + 175, 300, 100), _autoFeed ? "자동: 켜짐" : "자동: 꺼짐", _button))
                _autoFeed = !_autoFeed;

            // 레벨업
            string up;
            if (S.mimicLevel >= GameConfig.MaxMimicLevel) up = "최대 레벨";
            else if (MimicService.IsUpgrading(S)) up = "성장 중… " + FmtTime(S.mimicUpgradeEndUnix - Now);
            else up = "레벨업 (골드 " + Fmt(GameConfig.MimicUpgradeGold(S.mimicLevel)) + ")";
            GUI.enabled = MimicService.CanStartUpgrade(S);
            if (GUI.Button(new Rect(bx + 320, r.y + 175, 320, 100), up, _button)) _game.StartMimicUpgrade(Now);
            GUI.enabled = true;

            if (DevMode)
            {
                GUI.Label(new Rect(bx, r.y + 295, bw, 40), "[개발용]", _labelSmall);
                if (GUI.Button(new Rect(bx, r.y + 335, 200, 80), "열쇠+100", _button)) S.keys += 100;
                if (GUI.Button(new Rect(bx + 215, r.y + 335, 200, 80), "성장 즉시", _button)) MimicService.FinishUpgradeNow(S);
                if (GUI.Button(new Rect(bx + 430, r.y + 335, 210, 80), "초기화", _button))
                {
                    SaveSystem.Delete();
                    _game = new GameSession(PlayerState.NewGame(Now, Now));
                    _autoFeed = false;
                }
            }
        }

        void DrawLog(Rect r)
        {
            Box(r, new Color(0.06f, 0.06f, 0.08f));
            var log = _game.Log;
            int lines = 6;
            for (int i = 0; i < lines && i < log.Count; i++)
                GUI.Label(new Rect(30, r.y + 15 + i * 44, RefW - 60, 44), log[log.Count - 1 - i], _labelSmall);
        }

        void DrawPendingPopup()
        {
            var p = S.pending;
            var cur = S.equipped[(int)p.slot];
            double powNow = _game.Stats.Power;
            double powNew = StatsCalculator.PowerIfEquipped(S.equipped, p);

            var r = new Rect(90, 520, 900, 760);
            Box(new Rect(0, 0, RefW, RefH), new Color(0, 0, 0, 0.6f));
            Box(r, new Color(0.18f, 0.16f, 0.24f));
            Box(new Rect(r.x, r.y, r.width, 16), TierColors[p.tier]);

            GUI.Label(new Rect(r.x + 40, r.y + 40, 820, 60), "새 장비: " + p.Name, _labelBig);
            GUI.Label(new Rect(r.x + 40, r.y + 120, 400, 300), ItemText(p), _label);
            GUI.Label(new Rect(r.x + 470, r.y + 120, 400, 300), "현재: " + cur.Name + "\n" + ItemText(cur), _labelSmall);

            double diff = powNew - powNow;
            GUI.color = diff >= 0 ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.5f, 0.5f);
            GUI.Label(new Rect(r.x + 40, r.y + 450, 820, 60), "전투력 " + (diff >= 0 ? "▲ +" : "▼ ") + Fmt(diff), _labelBig);
            GUI.color = Color.white;

            if (GUI.Button(new Rect(r.x + 40, r.y + 560, 390, 150), "장착", _buttonBig)) _game.EquipPending();
            if (GUI.Button(new Rect(r.x + 470, r.y + 560, 390, 150), "판매 (+" + Fmt(GameConfig.SellPrice(p)) + "G)", _buttonBig)) _game.SellPending();
        }

        void DrawOfflinePopup()
        {
            var r = new Rect(140, 650, 800, 520);
            Box(new Rect(0, 0, RefW, RefH), new Color(0, 0, 0, 0.6f));
            Box(r, new Color(0.18f, 0.16f, 0.24f));
            GUI.Label(new Rect(r.x + 40, r.y + 40, 720, 60), "다녀오셨어요!", _labelBig);
            GUI.Label(new Rect(r.x + 40, r.y + 130, 720, 200),
                FmtTime(_offline.Seconds) + " 동안 클로제가 모아둔 것\n\n골드 +" + Fmt(_offline.Gold) + "\n열쇠 +" + _offline.Keys, _label);
            if (GUI.Button(new Rect(r.x + 200, r.y + 360, 400, 120), "받기", _buttonBig))
            {
                _game.ApplyOffline(_offline, Now);
                _showOffline = false;
            }
        }

        // ───────────────────────── 도우미 ─────────────────────────

        static string ItemText(ItemInstance it)
        {
            if (it.IsEmpty) return "-";
            string s = "Lv." + it.level + "\n" + (it.IsAttack ? "공격력 " : "체력 ") + Fmt(it.MainStat);
            foreach (var sub in it.subs)
                s += "\n" + GameConfig.SubStatNames[(int)sub.type] + " +" + (sub.value * 100).ToString("0.#") + "%";
            return s;
        }

        string _toast; float _toastUntil;
        void Toast(string msg) { _toast = msg; _toastUntil = Time.time + 1.5f; Debug.Log(msg); }

        static void Box(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static void Bar(Rect r, double ratio, Color c)
        {
            Box(r, new Color(0, 0, 0, 0.6f));
            Box(new Rect(r.x, r.y, r.width * Mathf.Clamp01((float)ratio), r.height), c);
        }

        /// <summary>큰 숫자 줄여 쓰기: 1.2k, 3.4m, 5.6b, 7.8t …</summary>
        public static string Fmt(double v)
        {
            string sign = v < 0 ? "-" : "";
            v = Math.Abs(v);
            string[] units = { "", "k", "m", "b", "t", "q", "Q", "s", "S" };
            int u = 0;
            while (v >= 1000 && u < units.Length - 1) { v /= 1000; u++; }
            return sign + (u == 0 ? Math.Floor(v).ToString("0") : v.ToString(v < 10 ? "0.00" : v < 100 ? "0.0" : "0")) + units[u];
        }

        static string FmtTime(long secs)
        {
            if (secs < 0) secs = 0;
            long h = secs / 3600, m = secs % 3600 / 60, s = secs % 60;
            return h > 0 ? h + "시간 " + m + "분" : m > 0 ? m + "분 " + s + "초" : s + "초";
        }

        void BuildStyles()
        {
            if (_stylesReady) return;
            GUIStyle Make(GUIStyle baseStyle, int size, TextAnchor anchor)
            {
                var st = new GUIStyle(baseStyle) { fontSize = size, alignment = anchor, wordWrap = true };
                st.normal.textColor = Color.white;
                if (_font != null) st.font = _font;
                return st;
            }
            _label = Make(GUI.skin.label, 38, TextAnchor.UpperLeft);
            _labelBig = Make(GUI.skin.label, 48, TextAnchor.UpperLeft);
            _labelBig.fontStyle = FontStyle.Bold;
            _labelSmall = Make(GUI.skin.label, 30, TextAnchor.UpperLeft);
            _center = Make(GUI.skin.label, 40, TextAnchor.MiddleCenter);
            _center.normal.textColor = Color.black;
            _button = Make(GUI.skin.button, 36, TextAnchor.MiddleCenter);
            _buttonBig = Make(GUI.skin.button, 48, TextAnchor.MiddleCenter);
            _toastStyle = Make(GUI.skin.label, 40, TextAnchor.MiddleCenter);
            _stylesReady = true;
        }

        void LateUpdate()
        {
            if (_toast != null && Time.time > _toastUntil) _toast = null;
        }
    }
}
