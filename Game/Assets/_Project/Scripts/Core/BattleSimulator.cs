using System;

namespace Closet.Core
{
    public enum BattleEventType { MonsterKilled, BossKilled, StageCleared, PlayerDied, BossTimeout, KeyDropped }

    public struct BattleEvent
    {
        public BattleEventType Type;
        public long Gold;
        public long Keys;
    }

    /// <summary>
    /// 자동전투 시뮬레이션. 고정 간격(1/30초)으로 Step()을 호출한다.
    /// 화면 연출은 이 클래스의 값(HP 등)을 읽어서 그리기만 한다.
    /// </summary>
    public class BattleSimulator
    {
        public const double StepSeconds = 1.0 / 30.0;

        readonly PlayerState _s;
        readonly Rng _rng;
        PlayerStats _stats;

        public double PlayerHp { get; private set; }
        public double MonsterHp { get; private set; }
        public double MonsterMaxHp { get; private set; }
        public int Wave { get; private set; }          // 1 ~ 5
        public int MonsterInWave { get; private set; }  // 1 ~ 3
        public bool IsBossFight { get; private set; }
        public double BossTimeLeft { get; private set; }
        public PlayerStats Stats => _stats;

        double _playerAtkTimer, _monsterAtkTimer, _monsterAtk;

        public event Action<BattleEvent> OnEvent;

        public BattleSimulator(PlayerState s, Rng rng)
        {
            _s = s;
            _rng = rng;
            RefreshStats();
            StartStage();
        }

        /// <summary>장비가 바뀌면 호출. 현재 체력 비율은 유지.</summary>
        public void RefreshStats()
        {
            double ratio = _stats == null || _stats.MaxHp <= 0 ? 1 : PlayerHp / _stats.MaxHp;
            _stats = StatsCalculator.Compute(_s.equipped);
            PlayerHp = Math.Max(1, _stats.MaxHp * ratio);
        }

        int Idx => GameConfig.ScalingIndex(_s.difficulty, _s.stage);
        bool IsFinalBoss => _s.stage == GameConfig.StagesPerDifficulty;

        public void StartStage()
        {
            PlayerHp = _stats.MaxHp;
            Wave = 1;
            MonsterInWave = 1;
            SpawnMonster();
        }

        void SpawnMonster()
        {
            IsBossFight = GameConfig.IsBossStage(_s.stage) && Wave == GameConfig.WavesPerStage;
            double hp = GameConfig.MonsterHp(Idx);
            double atk = GameConfig.MonsterAtk(Idx);
            if (IsBossFight)
            {
                hp *= GameConfig.BossHpMult;
                atk *= GameConfig.BossAtkMult;
                BossTimeLeft = IsFinalBoss ? GameConfig.FinalBossTimeLimit : GameConfig.BossTimeLimit;
            }
            MonsterMaxHp = MonsterHp = hp;
            _monsterAtk = atk;
            _playerAtkTimer = 0;
            _monsterAtkTimer = 0;
        }

        public void Step()
        {
            double dt = StepSeconds;

            // 플레이어 공격
            _playerAtkTimer += dt;
            while (_playerAtkTimer >= _stats.AttackInterval && MonsterHp > 0)
            {
                _playerAtkTimer -= _stats.AttackInterval;
                Hit();
                if (MonsterHp > 0 && _rng.Chance(_stats.DoubleHit)) Hit();
            }
            if (MonsterHp <= 0) { OnMonsterKilled(); return; }

            // 몬스터 공격
            _monsterAtkTimer += dt;
            double interval = IsBossFight ? GameConfig.BossAttackInterval : GameConfig.MonsterAttackInterval;
            while (_monsterAtkTimer >= interval)
            {
                _monsterAtkTimer -= interval;
                PlayerHp -= _monsterAtk;
            }
            if (PlayerHp <= 0) { OnPlayerDied(BattleEventType.PlayerDied); return; }

            if (IsBossFight)
            {
                BossTimeLeft -= dt;
                if (BossTimeLeft <= 0) OnPlayerDied(BattleEventType.BossTimeout);
            }
        }

        void Hit()
        {
            double dmg = _stats.Atk;
            if (_rng.Chance(_stats.CritRate)) dmg *= _stats.CritDamage;
            MonsterHp -= dmg;
            PlayerHp = Math.Min(_stats.MaxHp, PlayerHp + dmg * _stats.Lifesteal);
        }

        void OnMonsterKilled()
        {
            long gold = (long)Math.Ceiling(GameConfig.GoldPerKill(Idx) * (IsBossFight ? 5 : 1) * (1 + _stats.GoldBonus));
            _s.gold += gold;
            long keys = 0;
            if (_rng.Chance(GameConfig.KeyDropChance)) { keys = 1; _s.keys += 1; }
            Emit(new BattleEvent { Type = IsBossFight ? BattleEventType.BossKilled : BattleEventType.MonsterKilled, Gold = gold, Keys = keys });

            bool waveDone = IsBossFight || MonsterInWave >= GameConfig.MonstersPerWave;
            if (!waveDone) { MonsterInWave++; SpawnMonster(); return; }
            if (Wave < GameConfig.WavesPerStage) { Wave++; MonsterInWave = 1; SpawnMonster(); return; }
            OnStageCleared();
        }

        void OnStageCleared()
        {
            bool wasBoss = GameConfig.IsBossStage(_s.stage);
            long keys = wasBoss ? GameConfig.KeysPerBossClear : GameConfig.KeysPerStageClear;
            _s.keys += keys;
            Emit(new BattleEvent { Type = BattleEventType.StageCleared, Keys = keys });

            bool nextIsBoss = GameConfig.IsBossStage(_s.stage + 1);
            bool farming = !_s.autoChallengeBoss && nextIsBoss;
            if (!farming)
            {
                _s.stage++;
                if (_s.stage > GameConfig.StagesPerDifficulty)
                {
                    _s.stage = 1;
                    _s.difficulty++;
                }
            }
            StartStage();
        }

        void OnPlayerDied(BattleEventType type)
        {
            Emit(new BattleEvent { Type = type });
            if (GameConfig.IsBossStage(_s.stage))
            {
                // 보스 실패 → 직전 스테이지에서 파밍, 사용자가 "보스 도전"을 누를 때까지
                _s.autoChallengeBoss = false;
                _s.stage = Math.Max(1, _s.stage - 1);
            }
            StartStage();
        }

        /// <summary>파밍 중 "보스 도전" 버튼.</summary>
        public void ChallengeBoss()
        {
            _s.autoChallengeBoss = true;
            if (!GameConfig.IsBossStage(_s.stage) && GameConfig.IsBossStage(_s.stage + 1))
            {
                _s.stage++;
                StartStage();
            }
        }

        void Emit(BattleEvent e) => OnEvent?.Invoke(e);
    }
}
