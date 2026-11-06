# 기술 설계 v0.1

## 1. 기술 스택 (권장)
| 영역 | 선택 | 이유 |
|---|---|---|
| 엔진 | **Unity 6 LTS** (2D URP) | 모바일 방치형 표준, 자료 많음 |
| 캐릭터 애니 | **Spine 4.x** (spine-unity) | 스킨/어태치먼트 교체로 아바타 레이어링이 가장 쉬움 |
| 언어 | C# | |
| 리소스 | Addressables | 코스튬·챕터 배경을 원격 다운로드(초기 용량 ↓) |
| 데이터 | Google Sheets → CSV/JSON → ScriptableObject 변환 툴 | 기획자가 직접 수치 수정 |
| 백엔드 | 1안: **뒤끝(TheBackend)** (국내, 클랜/랭킹/우편 기본 제공) · 2안: PlayFab · 3안: Firebase + Cloud Functions | 1인/소규모면 뒤끝이 클랜·랭킹 구현 부담이 가장 적음 |
| 결제 | Unity IAP (Google Play Billing) + 서버 영수증 검증 | |
| 광고 | AdMob (미디에이션: AppLovin MAX 등) | |
| 분석 | Firebase Analytics + Crashlytics | |

## 2. 프로젝트 폴더 구조 (Unity)
```
Assets/
  _Project/
    Scripts/
      Core/          # GameManager, SaveSystem, TimeService(서버시간), EventBus
      Data/          # ScriptableObject 정의, CSV 임포터
      Closet/        # MimicService(먹이기→장비 생성 로직), ItemFactory, AutoSellFilter
      Avatar/        # AvatarController(Spine 스킨 조합), CostumeService
      Battle/        # BattleSimulator(결정론적), Unit, SkillRunner, StageRunner
      Progression/   # Companion, Familiar, Mount, ResearchTree
      Content/       # Dungeon, Arena, Clan, ClanWar
      Economy/       # Wallet(유료/무료 보석 분리), Shop, Gacha(서버 호출)
      UI/            # 화면별 Presenter
      Net/           # BackendClient 래퍼
    Data/            # 변환된 SO 에셋
    Art/
      Spine/Heroine/ # 리아 (스킨: 부위별)
      Spine/Companions/
      Backgrounds/Ch01~Ch12/
    Addressables/
Tools/
  SheetExporter/     # 구글시트 → JSON 스크립트
```

## 3. 아바타 구현 (Spine)
- 리아 Spine 프로젝트 1개, 부위별 **스킨 폴더**: `weapon/sword_t3_02`, `top/t5_01`, `hair_front/twin_01` …
- 런타임: `new Skin("combined")` 에 부위 스킨들을 `AddSkin`으로 합성 → `skeleton.SetSkin(combined)`
- 우선순위: 코스튬 > 장비 외형(도감에서 선택) > 장착 장비 기본 외형
- 무기 타입(검/지팡이/활/창)별 애니메이션 세트는 **애니만 분기**, 스켈레톤은 공용
- 아트 규칙: 모든 장비는 같은 본/어태치먼트 슬롯 이름 사용 (아트 가이드 문서화 필수)

## 4. 데이터 스키마 (요약)
```jsonc
// Item (장비 원형)
{ "id": "top_t5_01", "slot": "top", "tier": 5, "baseStat": {"hp": 120},
  "spineSkin": "top/t5_01", "nameKey": "item.top_t5_01" }

// ItemInstance (보유 장비)
{ "uid": "...", "itemId": "top_t5_01", "level": 23, "subStats": [{"type":"critRate","v":0.03}] }

// MimicLevel (클로제 레벨)
{ "lv": 20, "upgradeGold": 150000, "upgradeSec": 3600,
  "tierWeights": [10,35,40,15,0,0,0,0,0,0], "itemLvRange": [18,22] }

// Stage
{ "chapter": 12, "stage": 10, "isBoss": true, "monsterHp": ..., "monsterAtk": ...,
  "bossId": "boss_belzeria", "bgKey": "ch12_pandemonium", "dropTable": "drop_ch12" }

// Companion
{ "id": "comp_sera", "element": "fire", "grade": "legend",
  "activeSkill": "skill_firestorm", "ownedEffect": {"atkPct": 0.02} }
```

## 5. 결정론적 전투 & 서버 검증
- 전투는 `BattleSimulator(seed, attackerSnapshot, defenderSnapshot)` 순수 함수로 작성 (Unity 의존 X, 고정 틱 30/s)
- 화면 연출은 시뮬레이션 결과를 재생만 함
- 아레나·클랜전: 서버(Cloud Function/뒤끝 함수)에서 동일 시뮬레이터로 재계산 → 결과 확정
- 같은 C# 코드를 서버에서도 쓰려면 Simulator를 **.NET Standard 별도 어셈블리**로 분리

## 6. 세이브 & 치트 방지
- 로컬 세이브(암호화) + 주기적 서버 동기화(재화/가챠/결제는 서버 우선)
- 시간: 서버 시간 기준(오프라인 보상, 연구 대기) — 기기 시간 변경 치트 방지
- 먹이기 결과 RNG: 오프라인에서도 되도록 클라 RNG 허용하되, 열쇠 소모/장비 수는 서버 동기화 시 검증 (한도 초과 시 롤백)

## 7. 개발 환경 (집 데스크톱)
1. Unity Hub + Unity 6 LTS + Android Build Support (SDK/NDK/OpenJDK 포함)
2. Spine 에디터(유료 라이선스, 아트 담당) + spine-unity 런타임 (Unity 버전과 맞는 버전)
3. Git + Git LFS (`*.png *.psd *.spine *.wav` 등 LFS 추적)
4. 이 저장소를 클론:
   ```bash
   git clone https://github.com/eyasban-beep/EYASBAN.git
   ```
   ```bash
   git checkout claude/laughing-rubin-yg6vsc
   ```
5. Unity 프로젝트는 저장소 루트에 `Game/` 폴더로 생성 권장 (문서 `docs/`와 분리)
