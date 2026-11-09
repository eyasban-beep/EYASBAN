-- 참고용: functions/api/[[route]].js 가 첫 요청 때 같은 표를 자동으로 만든다.
CREATE TABLE IF NOT EXISTS items (
  id TEXT PRIMARY KEY,          -- 바꾸지 않는 내부 ID (제출 기록이 이 ID로 연결)
  name TEXT NOT NULL,           -- 화면에 보이는 이름
  aliases TEXT NOT NULL DEFAULT '', -- 한 줄 입력용 별칭, 쉼표 구분
  coef REAL NOT NULL DEFAULT 1, -- 점수 계수
  sort INTEGER NOT NULL DEFAULT 0,
  days TEXT NOT NULL DEFAULT ''   -- 클랜전에서 점수가 되는 날, 예: '1,3' = Day 1, Day 3
);
CREATE TABLE IF NOT EXISTS settings (key TEXT PRIMARY KEY, value TEXT NOT NULL); -- 카톡 재촉 문구 등
CREATE TABLE IF NOT EXISTS members (
  name TEXT PRIMARY KEY,
  active INTEGER NOT NULL DEFAULT 1, -- 0 = 명단에서 뺀 사람 (기록은 유지)
  sort INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE IF NOT EXISTS submissions (
  week TEXT NOT NULL,           -- 그 주 월요일 (한국 시간) 예: 2026-11-09
  member TEXT NOT NULL,
  vals TEXT NOT NULL,           -- {"skill":346000,"petegg":118000,...}
  raw TEXT NOT NULL DEFAULT '', -- 입력 원문
  submitted_at INTEGER NOT NULL,
  PRIMARY KEY (week, member)
);
