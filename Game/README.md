# Unity 프로젝트 — 처음 여는 방법 (집 데스크톱)

> 개발 처음이어도 따라 할 수 있게 순서대로 적었습니다. 막히면 화면을 캡처해서 Claude에게 보여주세요.

## 1. 설치 (처음 한 번만)
1. **Git** 설치: https://git-scm.com → 설치 후 명령 프롬프트에서 아래 실행
   ```bash
   git lfs install
   ```
2. **Unity Hub** 설치: https://unity.com/download
3. Unity Hub → Installs → **Install Editor** → **Unity 6 (LTS)** 최신 버전 선택
   - 모듈 선택 화면에서 **Android Build Support** (하위의 OpenJDK, Android SDK & NDK 포함) 체크
4. (선택) 코드 편집기: Unity 설치 시 **Visual Studio Community** 함께 설치 체크

## 2. 저장소 받기
```bash
git clone https://github.com/eyasban-beep/EYASBAN.git
```
```bash
cd EYASBAN
```
```bash
git checkout claude/laughing-rubin-yg6vsc
```

## 3. Unity에서 열기
1. Unity Hub → **Projects** → 오른쪽 위 **Add** → **Add project from disk**
2. `EYASBAN/Game` 폴더 선택
3. 에디터 버전을 물어보면 설치한 **Unity 6** 선택 → 처음 열 때 몇 분 걸림 (Library 폴더 생성)
4. 열리면 위쪽 가운데 **▶ (Play)** 버튼 클릭 → 게임 시작
   - 씬을 따로 만들 필요 없음 (`GameBootstrap`이 자동으로 게임을 띄움)
5. **Game** 탭 위쪽 해상도 드롭다운 → **+** → 1080×1920 (세로) 추가해서 선택하면 폰 화면처럼 보임

## 4. 지금 들어 있는 것 (M1 코어 루프 프로토타입)
| 기능 | 확인 방법 |
|---|---|
| 클로제 열쇠 먹이기 → 랜덤 장비 | "열쇠 먹이기" 버튼 |
| 비교 팝업 → 장착 / 판매 | 전투력 ▲▼ 표시 확인 |
| 자동 먹이기 (좋으면 장착, 아니면 판매) | "자동" 버튼 |
| 클로제 레벨업 (골드 + 대기시간) → 고티어 확률 상승 | "레벨업" 버튼 |
| 자동전투, 5웨이브, 10스테이지마다 보스(제한시간), 12챕터 → 난이도 상승 | 위쪽 전투 영역 |
| 보스 실패 시 이전 스테이지 파밍 → "보스 도전" | 장비가 약할 때 |
| 장비 색이 종이인형(아바타 자리)에 반영 | 왼쪽 인형 색 변화 |
| 오프라인 보상 (최대 12시간) | 끄고 1분 이상 뒤 다시 Play |
| 자동 저장 (10초마다 + 종료 시) | |
| [개발용] 열쇠+100 / 성장 즉시 / 초기화 | 에디터에서만 보임 |

**화면은 임시 네모 박스**입니다. 규칙이 재미있는지 먼저 확인하고, 아트가 준비되면 교체합니다.

## 5. 한글이 네모(□)로 보이면
1. 무료 한글 폰트 다운로드 (예: Google Fonts의 **Noto Sans KR**)
2. `Game/Assets/_Project/Resources/Fonts/` 폴더를 만들고 ttf 파일을 넣은 뒤 이름을 **`KoreanFont.ttf`** 로 변경
3. 다시 Play → 자동 적용

## 6. 폴더 구조
```
Game/
  Assets/_Project/Scripts/
    Core/   ← 게임 규칙 (Unity와 무관한 순수 C#). 밸런스 숫자는 GameConfig.cs
    Game/   ← Unity 연결부 (실행, 저장, 임시 화면)
  CoreTests/ ← Unity 없이 규칙만 검사하는 프로그램 (Claude가 사용)
  Packages/manifest.json
```

## 7. 밸런스 숫자 바꿔보기
`Assets/_Project/Scripts/Core/GameConfig.cs` 를 열어 숫자를 바꾸고 저장 → Unity로 돌아오면 자동 반영
- `StartingKeys` 시작 열쇠 수, `KeyDropChance` 열쇠 드랍 확률, `MonsterHp` 몬스터 체력 공식 등

## 8. 규칙 테스트 (선택, .NET 8 SDK 필요)
```bash
cd Game/CoreTests
```
```bash
dotnet run
```
