# 길드전 자원 조사 (Cloudflare Pages)

클랜원이 매주 월요일 자원 수량을 올리면 **계수를 곱해 점수를 계산**하고, **안 낸 사람을 표시**하는 웹페이지.
게임 프로젝트(`Game/`)와는 별개인 클랜용 도구다. 게임 이름·로고·아이콘은 페이지에 넣지 않았다.

## 기능
| 탭 | 내용 |
|---|---|
| 제출하기 | ① **한 칸씩 입력**: 닉네임 선택 → 항목마다 숫자 입력<br>② **한 줄로 붙여넣기**: `daye, 스킬뽑기 346k, 펫알 118k, 펫합성 1840개, …` → 미리보기 확인 후 제출<br>숫자는 `346k`, `2.7k`, `1.2m`, `1,840개`, `3만` 모두 인식. 같은 주에 다시 내면 덮어씀 |
| 현황·점수 | 주차 선택, **미제출자 목록**(+ 게임 채팅용 복사 버튼), 점수 순위표, 엑셀용 표 복사 |
| 관리자 | 비밀번호 로그인 → **항목·계수 편집**(추가/삭제/순서/별칭), **클랜원 명단** 편집, 잘못된 제출 삭제 |

- 주차 = **한국 시간 월요일 00:00 ~ 일요일 23:59**. 월요일 0시가 되면 자동으로 새 주가 시작되고 전원이 다시 '미제출'이 된다.
- 점수 = Σ(수량 × 계수). 계수는 저장된 숫자에 매번 곱하므로, 계수를 바꾸면 지난 주 점수도 새 계수로 다시 계산된다.

## 파일 구조
```
tools/clan-survey/
├─ public/            ← 화면 (그대로 배포됨)
│  ├─ index.html
│  ├─ style.css
│  ├─ app.js          ← 화면 동작
│  └─ parse.js        ← "346k" 같은 글자를 숫자로 바꾸는 부분
├─ functions/api/[[route]].js   ← 서버 (/api/... 요청 처리, D1 저장)
└─ schema.sql         ← DB 표 구조 (참고용, 서버가 처음 실행될 때 자동으로 만듦)
```

---

## 배포 방법 (처음 1번만)

### 1단계. D1 데이터베이스 만들기
1. https://dash.cloudflare.com 로그인
2. 왼쪽 메뉴 **Storage & Databases → D1 SQL Database** → **Create**
3. 이름: `clan-survey` → Create
   (표는 사이트가 처음 열릴 때 자동으로 만들어지니 콘솔에서 SQL을 칠 필요 없음)

### 2단계. Pages 프로젝트 만들기 (GitHub 연결)
1. 왼쪽 메뉴 **Workers & Pages** → **Create** → **Pages** 탭 → **Connect to Git**
   (Workers 화면만 보이면 아래쪽의 "Looking to deploy Pages? Get started" 링크)
2. GitHub 저장소 `EYASBAN` 선택
3. 설정값:
   | 항목 | 값 |
   |---|---|
   | Production branch | 이 코드가 들어있는 브랜치 (main에 합친 뒤라면 `main`) |
   | Framework preset | None |
   | Build command | (비워둠) |
   | Build output directory | `public` |
   | Root directory (Advanced 안에 있음) | `tools/clan-survey` |
4. **Save and Deploy**. 이 시점에는 DB가 아직 안 붙어서 사이트가 "D1 데이터베이스가 연결되지 않았어요"라고 나오는 게 정상.

### 3단계. DB 연결 + 비밀번호 설정
Pages 프로젝트 → **Settings**
1. **Bindings** → Add → **D1 database** → Variable name `DB` / Database `clan-survey` → Save
2. **Variables and Secrets** → Add
   - `ADMIN_PASSWORD` (Type: **Secret**) — 관리자 탭 비밀번호. 계수·명단을 바꿀 사람만 알면 됨
   - `CLAN_CODE` (선택, Secret) — 넣으면 제출할 때 이 코드를 물어봄. 링크가 밖으로 새도 아무나 제출 못 하게 하고 싶을 때
3. **Deployments** 탭 → 최신 배포 오른쪽 `…` → **Retry deployment** (설정은 다시 배포해야 적용됨)

### 4단계. 처음 세팅
1. `https://<프로젝트이름>.pages.dev` 접속 → **관리자** 탭 → 비밀번호 입력
2. **클랜원 명단**: 한 줄에 한 명씩 붙여넣고 저장 (닉네임은 대소문자 구분 없이 맞춰줌: `daye` = `Daye`)
3. **항목·계수**: 기본 6개(스킬뽑기·펫알·펫합성·탈것뽑기·탈것합성·초록물약, 계수 1)가 들어있음. 계수 고치고 저장
   - "다른 이름"에 `스킬, 스뽑` 처럼 적으면 한 줄 입력에서 `스뽑 346k`도 알아들음
4. 사이트 주소를 클랜 공지에 올리기 → 끝

## 매주 쓰는 법
- **클랜원**: 월요일에 접속 → 제출하기 → 둘 중 편한 방식으로 입력 → 제출
- **관리자**: 현황·점수 탭 → `미제출자 목록 복사` → 게임 채팅에 붙여넣어 독촉 / `표 복사` → 엑셀·구글시트에 붙여넣기

## 코드를 고쳤을 때
GitHub의 Production branch에 push하면 Cloudflare가 자동으로 다시 배포한다 (1분 정도).

## 로컬에서 미리 돌려보기 (선택, Node.js 필요)
```bash
cd tools/clan-survey
```
```bash
npx wrangler pages dev public --d1 DB=local-db --binding ADMIN_PASSWORD=test
```
→ 브라우저에서 http://localhost:8788
