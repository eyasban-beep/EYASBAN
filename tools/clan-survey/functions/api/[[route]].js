// Cloudflare Pages Functions — /api/* 전부 이 파일이 처리
// 필요한 설정 (Cloudflare 대시보드 → Pages 프로젝트 → Settings)
//   - D1 바인딩: 변수 이름 DB
//   - 환경 변수(비밀): ADMIN_PASSWORD (관리자 비밀번호), CLAN_CODE (선택: 제출용 클랜 코드)

// [id, 이름, 별칭, 계수(개당 전쟁 포인트), 쓰는 날(Day)]
// 계수는 처음 값일 뿐, 관리자 화면에서 바꾼다.
const DEFAULT_ITEMS = [
  ['skill', '스킬뽑기', '스킬', 125, '1,3'],
  ['petegg', '펫알', '알', 400, '3,5'],
  ['petmerge', '펫합성', '', 1250, '3,5'],
  ['mount', '탈것뽑기', '탈것', 600, '2,4'],
  ['mountmerge', '탈것합성', '', 600, '2,4'],
  ['green', '초록물약', '초록,물약', 1, '2,5'],
];

const DEFAULT_SETTINGS = {
  nagTemplate: '📢 [{주차} 주차 자원조사] 미제출 {미제출수}명\n{미제출자}\n\n아래 링크에서 1분이면 끝나요 🙏\n{링크}\n(한 줄 입력 예: 닉네임, 스킬뽑기 346k, 펫알 118k)',
};

const SCHEMA = [
  `CREATE TABLE IF NOT EXISTS items (
     id TEXT PRIMARY KEY, name TEXT NOT NULL, aliases TEXT NOT NULL DEFAULT '',
     coef REAL NOT NULL DEFAULT 1, sort INTEGER NOT NULL DEFAULT 0, days TEXT NOT NULL DEFAULT '')`,
  `CREATE TABLE IF NOT EXISTS settings (key TEXT PRIMARY KEY, value TEXT NOT NULL)`,
  `CREATE TABLE IF NOT EXISTS members (
     name TEXT PRIMARY KEY, active INTEGER NOT NULL DEFAULT 1, sort INTEGER NOT NULL DEFAULT 0)`,
  `CREATE TABLE IF NOT EXISTS submissions (
     week TEXT NOT NULL, member TEXT NOT NULL, vals TEXT NOT NULL, raw TEXT NOT NULL DEFAULT '',
     submitted_at INTEGER NOT NULL, PRIMARY KEY (week, member))`,
];

let ready = false;
async function ensureSchema(db) {
  if (ready) return;
  await db.batch(SCHEMA.map(s => db.prepare(s)));
  // 예전 버전 DB에 days 칸이 없으면 추가 (이미 있으면 오류 → 무시)
  try { await db.prepare(`ALTER TABLE items ADD COLUMN days TEXT NOT NULL DEFAULT ''`).run(); } catch {}
  const { n } = await db.prepare('SELECT COUNT(*) AS n FROM items').first();
  if (n === 0) {
    await db.batch(DEFAULT_ITEMS.map(([id, name, aliases, coef, days], i) =>
      db.prepare('INSERT INTO items (id,name,aliases,coef,sort,days) VALUES (?,?,?,?,?,?)').bind(id, name, aliases, coef, i, days)));
  }
  ready = true;
}

// 한국 시간 기준 그 주 월요일 날짜 "YYYY-MM-DD"
function weekKey(ms = Date.now()) {
  const d = new Date(ms + 9 * 3600e3);
  d.setUTCDate(d.getUTCDate() - ((d.getUTCDay() + 6) % 7));
  return d.toISOString().slice(0, 10);
}

const json = (data, status = 200) =>
  new Response(JSON.stringify(data), { status, headers: { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-store' } });
const fail = (msg, status = 400) => json({ error: msg }, status);

async function getItems(db) {
  const { results } = await db.prepare('SELECT * FROM items ORDER BY sort, name').all();
  return results.map(r => ({ id: r.id, name: r.name, aliases: r.aliases ? r.aliases.split(',').map(s => s.trim()).filter(Boolean) : [], coef: r.coef,
    days: String(r.days || '').split(',').map(Number).filter(d => d >= 1 && d <= 5) }));
}
async function getSettings(db) {
  const { results } = await db.prepare('SELECT key, value FROM settings').all();
  const out = { ...DEFAULT_SETTINGS };
  for (const r of results) if (r.key in DEFAULT_SETTINGS) out[r.key] = r.value;
  return out;
}
async function getMembers(db, all = false) {
  const { results } = await db.prepare(`SELECT name, active FROM members ${all ? '' : 'WHERE active = 1'} ORDER BY sort, name COLLATE NOCASE`).all();
  return all ? results : results.map(r => r.name);
}

function isAdmin(request, env) {
  const pw = request.headers.get('x-admin-password') || '';
  return !!env.ADMIN_PASSWORD && pw === env.ADMIN_PASSWORD;
}

export async function onRequest({ request, env, params }) {
  if (!env.DB) return fail('서버 설정 오류: D1 데이터베이스(DB)가 연결되지 않았어요. README의 3단계를 확인하세요.', 500);
  const db = env.DB;
  await ensureSchema(db);

  const path = (params.route || []).join('/');
  const method = request.method;
  const body = method === 'GET' ? null : await request.json().catch(() => ({}));

  try {
    // ---------- 누구나 ----------
    if (path === 'config' && method === 'GET') {
      return json({ items: await getItems(db), members: await getMembers(db), week: weekKey(), requireCode: !!env.CLAN_CODE, settings: await getSettings(db) });
    }

    if (path === 'week' && method === 'GET') {
      const url = new URL(request.url);
      const week = url.searchParams.get('week') || weekKey();
      if (!/^\d{4}-\d{2}-\d{2}$/.test(week)) return fail('잘못된 주');
      const { results } = await db.prepare('SELECT member, vals, raw, submitted_at FROM submissions WHERE week = ?').bind(week).all();
      const w = await db.prepare('SELECT DISTINCT week FROM submissions ORDER BY week DESC LIMIT 52').all();
      const weeks = [...new Set([weekKey(), ...w.results.map(r => r.week)])].sort().reverse();
      return json({
        week, current: weekKey(), weeks,
        submissions: results.map(r => ({ member: r.member, values: JSON.parse(r.vals), raw: r.raw, submittedAt: r.submitted_at })),
      });
    }

    if (path === 'export' && method === 'GET') {
      const { results } = await db.prepare('SELECT week, member, vals, submitted_at FROM submissions ORDER BY week DESC, member').all();
      return json({ submissions: results.map(r => ({ week: r.week, member: r.member, values: JSON.parse(r.vals), submittedAt: r.submitted_at })) });
    }

    if (path === 'submit' && method === 'POST') {
      if (env.CLAN_CODE && String(body.code || '').trim() !== env.CLAN_CODE) return fail('클랜 코드가 틀렸어요', 403);
      const members = await getMembers(db);
      const member = members.find(m => m.toLowerCase() === String(body.member || '').trim().toLowerCase());
      if (!member) return fail('클랜원 명단에 없는 닉네임이에요. 관리자에게 추가를 요청하세요.');
      const items = await getItems(db);
      const values = {};
      for (const it of items) {
        const v = body.values?.[it.id];
        if (v === undefined || v === null || v === '') continue;
        const n = Number(v);
        if (!Number.isFinite(n) || n < 0 || n > 1e13) return fail(`${it.name} 값이 이상해요`);
        values[it.id] = Math.round(n);
      }
      if (Object.keys(values).length === 0) return fail('입력된 값이 없어요');
      const week = weekKey();
      await db.prepare(`INSERT INTO submissions (week, member, vals, raw, submitted_at) VALUES (?,?,?,?,?)
        ON CONFLICT(week, member) DO UPDATE SET vals = excluded.vals, raw = excluded.raw, submitted_at = excluded.submitted_at`)
        .bind(week, member, JSON.stringify(values), String(body.raw || '').slice(0, 1000), Date.now()).run();
      return json({ ok: true, week, member, values });
    }

    // ---------- 관리자 ----------
    if (path.startsWith('admin/')) {
      if (!env.ADMIN_PASSWORD) return fail('ADMIN_PASSWORD 환경 변수가 설정되지 않았어요', 500);
      if (!isAdmin(request, env)) return fail('관리자 비밀번호가 틀렸어요', 401);
      const sub = path.slice(6);

      if (sub === 'login') return json({ ok: true, members: await getMembers(db, true) });

      if (sub === 'items' && method === 'PUT') {
        const list = Array.isArray(body.items) ? body.items : [];
        const ids = new Set();
        const rows = [];
        for (const [i, it] of list.entries()) {
          const id = String(it.id || '').trim(), name = String(it.name || '').trim();
          const coef = Number(it.coef);
          if (!/^[a-z0-9_-]{1,40}$/i.test(id) || !name) return fail(`${i + 1}번째 항목 이름이 비었어요`);
          if (!Number.isFinite(coef)) return fail(`${name} 계수가 숫자가 아니에요`);
          if (ids.has(id)) return fail('항목 ID 중복');
          ids.add(id);
          const aliases = (Array.isArray(it.aliases) ? it.aliases : String(it.aliases || '').split(',')).map(s => String(s).trim()).filter(Boolean).join(',');
          const days = [...new Set((Array.isArray(it.days) ? it.days : []).map(Number).filter(d => d >= 1 && d <= 5))].sort().join(',');
          rows.push(db.prepare('INSERT INTO items (id,name,aliases,coef,sort,days) VALUES (?,?,?,?,?,?)').bind(id, name, aliases, coef, i, days));
        }
        if (!rows.length) return fail('항목이 최소 1개는 있어야 해요');
        await db.batch([db.prepare('DELETE FROM items'), ...rows]);
        return json({ ok: true, items: await getItems(db) });
      }

      if (sub === 'members' && method === 'PUT') {
        const names = [...new Set((body.members || []).map(s => String(s).trim()).filter(Boolean))];
        if (names.some(n => n.length > 40)) return fail('닉네임이 너무 길어요');
        const stmts = [db.prepare('UPDATE members SET active = 0')];
        names.forEach((n, i) => stmts.push(
          db.prepare('INSERT INTO members (name, active, sort) VALUES (?,1,?) ON CONFLICT(name) DO UPDATE SET active = 1, sort = excluded.sort').bind(n, i)));
        await db.batch(stmts);
        return json({ ok: true, members: await getMembers(db, true) });
      }

      if (sub === 'settings' && method === 'PUT') {
        const stmts = [];
        for (const k of Object.keys(DEFAULT_SETTINGS)) {
          if (typeof body[k] !== 'string') continue;
          stmts.push(db.prepare('INSERT INTO settings (key, value) VALUES (?,?) ON CONFLICT(key) DO UPDATE SET value = excluded.value').bind(k, body[k].slice(0, 2000)));
        }
        if (stmts.length) await db.batch(stmts);
        return json({ ok: true, settings: await getSettings(db) });
      }

      if (sub === 'submission' && method === 'DELETE') {
        await db.prepare('DELETE FROM submissions WHERE week = ? AND member = ?').bind(String(body.week), String(body.member)).run();
        return json({ ok: true });
      }
    }

    return fail('없는 주소예요', 404);
  } catch (e) {
    return fail('서버 오류: ' + (e.message || e), 500);
  }
}
