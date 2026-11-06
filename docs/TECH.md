# 기술 설계 v0.1

## 1. 기술 스택 (권장)
| 영역 | 선택 | 이유 |
|---|---|---|
| 엔진 | **Unity 6 LTS** (2D URP) | 모바일 방치형 표준, 자료 많음 |
| 캐릭터 애니 | **Unity 2D Animation + Sprite Library (무료)** | 1인·초보·AI 아트 기준. 부위별 PNG를 갈아끼우는 방식(Sprite Swap). Spine은 유료+애니메이터 숙련 필요 → 매출 나면 이후 전환 |
| 언어 | C# | |
| 리소스 | Addressables | 코스튬·챕터 배경을 원격 다운로드(초기 용량 ↓) |
| 데이터 | Google Sheets → CSV/JSON → ScriptableObject 변환 툴 | 기획자가 직접 수치 수정 |
| 백엔드 | **PlayFab** (Microsoft, 글로벌) — 대안: 뒤끝 | 글로벌 출시 기준. 길드(Groups)·랭킹·인벤토리·서버 함수(CloudScript)·영수증 검증 기본 제공 → 서버 직접 안 만들어도 됨 |
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

## 3. 아바타 구현 (Sprite Swap — 1인 개발 버전)
- **SD(3등신) 인게임 캐릭터**: 몸 기본 PNG 1장 + 부위별 PNG(같은 캔버스 크기·같은 기준점)를 겹쳐 그림
  - Unity `SpriteLibrary` 에 카테고리(top, bottom, weapon …) / 라벨(t5_01 …) 등록 → `SpriteResolver.SetCategoryAndLabel()` 로 교체
  - 애니메이션: 2D Animation 패키지 본 리깅(대기·공격·피격 3개만) — 모든 부위가 같은 본을 공유
- **6~8등신 확대 일러스트**: 애니메이션 없는 정지 일러스트(포토 모드·가챠 연출·동료 상세)만. 레이어 합성은 안 하고 **코스튬 세트 단위 1장**으로 제작 → 아트 물량 폭증 방지
- 우선순위: 코스튬 > 장비 외형(도감에서 선택) > 장착 장비 기본 외형
- 무기 타입(검/지팡이/활/창)별 애니메이션 세트는 **애니만 분기**, 스켈레톤은 공용
- 아트 규칙: 모든 부위 PNG는 **같은 캔버스(예: 512×512), 같은 기준점, 같은 포즈** — AI 생성 이미지는 그대로 쓰면 위치·포즈가 매번 달라서 겹쳐지지 않음
  - AI로 "디자인 시안"을 뽑고 → 템플릿(몸 실루엣) 위에 맞춰 **손으로 정리**하는 단계가 반드시 필요 (Krita/Photoshop, 부위당 10~30분)
  - 정리 비용을 줄이려고 **장비 외형 수를 티어당 부위별 1종(80종)으로 런칭** → 색 변형(팔레트 스왑)으로 티어 구분

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
2. Krita(무료) — AI 시안 정리·부위 분리용
3. Git + Git LFS (`*.png *.psd *.spine *.wav` 등 LFS 추적)
4. 이 저장소를 클론:
   ```bash
   git clone https://github.com/eyasban-beep/EYASBAN.git
   ```
   ```bash
   git checkout claude/laughing-rubin-yg6vsc
   ```
5. Unity 프로젝트는 저장소 루트에 `Game/` 폴더로 생성 권장 (문서 `docs/`와 분리)
