import { parseAmount, parseLine, fmt, score, scored } from './parse.js';

const $ = s => document.querySelector(s);
const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const store = {
  get: k => { try { return localStorage.getItem(k) || ''; } catch { return ''; } },
  set: (k, v) => { try { localStorage.setItem(k, v); } catch {} },
};

let cfg = { items: [], members: [], week: '', requireCode: false };
let weekData = null;       // 현재 보고 있는 주
let thisWeek = null;       // 이번 주 (제출 탭 미리 채우기용)
let mode = 'form';
let adminPw = '';
try { adminPw = sessionStorage.getItem('adminPw') || ''; } catch {}

async function api(path, opts = {}) {
  const headers = { 'content-type': 'application/json' };
  if (adminPw) headers['x-admin-password'] = adminPw;
  const res = await fetch('/api/' + path, { ...opts, headers, body: opts.body ? JSON.stringify(opts.body) : undefined });
  const data = await res.json().catch(() => ({ error: '응답을 읽지 못했어요 (' + res.status + ')' }));
  if (!res.ok) throw new Error(data.error || res.status);
  return data;
}
function msg(el, text, ok) { el.textContent = text; el.className = 'msg ' + (ok ? 'ok' : 'err'); }

// ---------------- 탭 ----------------
document.querySelectorAll('.tab').forEach(b => b.onclick = () => showTab(b.dataset.tab));
function showTab(t) {
  document.querySelectorAll('.tab').forEach(b => b.classList.toggle('active', b.dataset.tab === t));
  document.querySelectorAll('.panel').forEach(p => p.classList.toggle('hidden', p.id !== 'tab-' + t));
  if (t === 'status') loadWeek($('#weekSel').value || cfg.week);
  location.hash = t;
}

// ---------------- 제출 ----------------
document.querySelectorAll('.mode').forEach(b => b.onclick = () => {
  mode = b.dataset.mode;
  document.querySelectorAll('.mode').forEach(x => x.classList.toggle('active', x === b));
  $('#formMode').classList.toggle('hidden', mode !== 'form');
  $('#lineMode').classList.toggle('hidden', mode !== 'line');
  updatePreview();
});

function renderForm() {
  const sel = $('#memberSel');
  const last = store.get('member');
  sel.innerHTML = '<option value="">— 선택 —</option>' +
    cfg.members.map(m => `<option ${m === last ? 'selected' : ''}>${esc(m)}</option>`).join('');
  $('#itemFields').innerHTML = cfg.items.map(it => `
    <div class="item-field">
      <label for="f-${it.id}">${esc(it.name)}</label>
      <input id="f-${it.id}" data-id="${it.id}" inputmode="decimal" placeholder="예: 346k">
      <div class="parsed" id="p-${it.id}"></div>
    </div>`).join('');
  $('#itemFields').querySelectorAll('input').forEach(inp => inp.oninput = updatePreview);
  sel.onchange = () => { fillFromThisWeek(); updatePreview(); };
  fillFromThisWeek();
}

// 이번 주에 이미 냈으면 그 값으로 채움 (수정 제출 가능)
function fillFromThisWeek() {
  const name = $('#memberSel').value;
  const prev = thisWeek?.submissions.find(s => s.member === name);
  $('#prevHint').textContent = prev ? `이번 주에 이미 제출했어요 (${new Date(prev.submittedAt).toLocaleString('ko-KR')}). 다시 내면 덮어써요.` : '';
  cfg.items.forEach(it => { $('#f-' + it.id).value = prev && prev.values[it.id] != null ? fmt(prev.values[it.id]) : ''; });
}

function collect() {
  if (mode === 'form') {
    const values = {}, bad = [];
    cfg.items.forEach(it => {
      const raw = $('#f-' + it.id).value;
      const p = $('#p-' + it.id);
      if (raw.trim() === '') { p.textContent = ''; return; }
      const n = parseAmount(raw);
      if (n == null) { bad.push(it.name); p.textContent = '숫자를 읽지 못했어요'; p.className = 'parsed bad'; }
      else { values[it.id] = n; p.textContent = '= ' + n.toLocaleString('ko-KR'); p.className = 'parsed'; }
    });
    const member = $('#memberSel').value;
    const raw = member + ', ' + cfg.items.filter(it => it.id in values).map(it => `${it.name} ${$('#f-' + it.id).value.trim()}`).join(', ');
    return { member, values, raw, warnings: bad.map(n => `${n} 숫자를 읽지 못했어요`) };
  }
  const text = $('#lineInput').value;
  const r = parseLine(text, cfg.items, cfg.members);
  return { ...r, raw: text.trim() };
}

$('#lineInput').oninput = updatePreview;
function updatePreview() {
  const c = collect();
  const n = Object.keys(c.values).length;
  $('#scorePreview').textContent = n ? `예상 점수 ${fmt(score(c.values, cfg.items))}` : '';
  if (mode === 'line') {
    const text = $('#lineInput').value.trim();
    if (!text) { $('#preview').innerHTML = ''; return; }
    $('#preview').innerHTML = `
      <table class="preview-table">
        <tr><td>닉네임</td><td>${c.member ? esc(c.member) : '<span class="msg err">?</span>'}</td></tr>
        ${cfg.items.map(it => `<tr><td>${esc(it.name)}</td><td>${it.id in c.values ? c.values[it.id].toLocaleString('ko-KR') : '<span class="hint">(없음 → 0)</span>'}</td></tr>`).join('')}
      </table>
      ${c.warnings.length ? `<ul class="warn-list">${c.warnings.map(w => `<li>${esc(w)}</li>`).join('')}</ul>` : ''}`;
  }
}

$('#submitBtn').onclick = async () => {
  const out = $('#submitMsg');
  const c = collect();
  if (!c.member) return msg(out, '닉네임을 확인해주세요', false);
  if (c.warnings.length) return msg(out, '빨간 경고를 먼저 고쳐주세요', false);
  if (!Object.keys(c.values).length) return msg(out, '값을 하나 이상 넣어주세요', false);
  const missing = cfg.items.filter(it => !(it.id in c.values)).map(it => it.name);
  if (missing.length && !confirm(`${missing.join(', ')} 은(는) 0으로 저장돼요. 제출할까요?`)) return;
  const code = $('#clanCode').value.trim();
  $('#submitBtn').disabled = true;
  try {
    const r = await api('submit', { method: 'POST', body: { member: c.member, values: c.values, raw: c.raw, code } });
    store.set('member', r.member);
    if (code) store.set('clanCode', code);
    msg(out, `✅ ${r.member} 님 ${r.week} 주차 제출 완료! 점수 ${fmt(score(r.values, cfg.items))}`, true);
    thisWeek = await api('week?week=' + cfg.week);
    if (mode === 'line') $('#lineInput').value = '';
    else { $('#memberSel').value = r.member; fillFromThisWeek(); }
    updatePreview();
  } catch (e) { msg(out, e.message, false); }
  $('#submitBtn').disabled = false;
};

// ---------------- 현황 ----------------
const DOW = ['일', '월', '화', '수', '목', '금', '토'];
// 조사 주(월요일) → 클랜전 Day n 날짜 (Day 1 = 다음날 화요일)
function warDate(week, day) {
  const d = new Date(week + 'T00:00:00Z');
  d.setUTCDate(d.getUTCDate() + day);
  return { md: `${d.getUTCMonth() + 1}/${d.getUTCDate()}`, dow: DOW[d.getUTCDay()] };
}
const pts = n => Math.round(n).toLocaleString('ko-KR');
let sortKey = 'score', sortDir = -1;

$('#weekSel').onchange = e => loadWeek(e.target.value);
async function loadWeek(week) {
  try { weekData = await api('week?week=' + week); } catch (e) { $('#rankTable').innerHTML = `<tr><td class="msg err">${esc(e.message)}</td></tr>`; return; }
  $('#weekSel').innerHTML = weekData.weeks.map(w => `<option value="${w}" ${w === weekData.week ? 'selected' : ''}>${w}${w === weekData.current ? ' (이번 주)' : ''}</option>`).join('');
  const d1 = warDate(weekData.week, 1), d6 = warDate(weekData.week, 6);
  $('#warRange').textContent = `클랜전 ${d1.md}(${d1.dow}) ~ ${d6.md}(${d6.dow})`;
  renderStatus();
}

function missingMembers() {
  const done = new Set(weekData.submissions.map(s => s.member));
  return cfg.members.filter(m => !done.has(m));
}

function nagMessage() {
  const missing = missingMembers();
  return (cfg.settings?.nagTemplate || '')
    .replaceAll('{주차}', weekData.week)
    .replaceAll('{미제출자}', missing.join(', ') || '없음 🎉')
    .replaceAll('{미제출수}', missing.length)
    .replaceAll('{전체수}', cfg.members.length)
    .replaceAll('{링크}', location.origin);
}

// 명단 전원 + 명단 밖 제출자를 한 표로
function sheetRows() {
  const bySub = new Map(weekData.submissions.map(s => [s.member, s]));
  const names = [...cfg.members, ...weekData.submissions.map(s => s.member).filter(m => !cfg.members.includes(m))];
  const list = names.map(m => {
    const s = bySub.get(m);
    return { member: m, sub: s, values: s?.values || {}, score: s ? score(s.values, cfg.items) : 0, roster: cfg.members.includes(m) };
  });
  const val = r => sortKey === 'score' ? r.score : sortKey === 'member' ? r.member.toLowerCase()
    : sortKey === 'time' ? (r.sub?.submittedAt || 0) : (r.values[sortKey] ?? -1);
  list.sort((a, b) => {
    if (!!a.sub !== !!b.sub && sortKey !== 'member') return a.sub ? -1 : 1; // 미제출은 아래로
    const x = val(a), y = val(b);
    return (x > y ? 1 : x < y ? -1 : 0) * sortDir;
  });
  return list;
}

// 일자별 표: 자원 총량 × 계수를 쓰는 날 수로 나눔
function dayStats() {
  const totals = {};
  cfg.items.forEach(it => { totals[it.id] = weekData.submissions.reduce((a, s) => a + (Number(s.values[it.id]) || 0), 0); });
  const days = [1, 2, 3, 4, 5].map(d => {
    const cells = cfg.items.map(it => it.days.includes(d) ? totals[it.id] * it.coef / it.days.length : null);
    return { d, cells, sum: cells.reduce((a, c) => a + (c || 0), 0) };
  });
  return { totals, days };
}

function renderStatus() {
  const missing = missingMembers();
  const subCount = weekData.submissions.filter(s => cfg.members.includes(s.member)).length;
  $('#missingCount').textContent = `${missing.length}명`;
  $('#doneCount').textContent = `${subCount} / ${cfg.members.length}명 제출`;
  $('#missingList').innerHTML = missing.length
    ? missing.map(m => `<span class="chip">${esc(m)}</span>`).join('')
    : '<span class="done-all">🎉 전원 제출 완료</span>';
  $('#nagText').value = nagMessage();

  // 일자별
  const { totals, days } = dayStats();
  const si = cfg.items.filter(scored), ci = cfg.items.filter(it => !scored(it));
  $('#dayTable').innerHTML = `
    <thead><tr><th>클랜전</th><th>날짜</th>${si.map(it => `<th>${esc(it.name)}<small>×${it.coef}</small></th>`).join('')}<th>합계</th></tr></thead>
    <tbody>
      <tr class="total"><td>수량</td><td></td>${si.map(it => `<td>${fmt(totals[it.id])}</td>`).join('')}<td></td></tr>
      ${days.map(r => { const w = warDate(weekData.week, r.d); return `
      <tr><td>Day ${r.d}</td><td>${w.md} (${w.dow})</td>
        ${r.cells.map((c, j) => scored(cfg.items[j]) ? (c == null ? '<td class="na">·</td>' : `<td>${pts(c)}</td>`) : '').join('')}
        <td class="score">${pts(r.sum)}</td></tr>`; }).join('')}
      <tr class="total"><td>합계</td><td></td>${si.map(it => `<td>${pts(totals[it.id] * it.coef)}</td>`).join('')}
        <td class="score">${pts(days.reduce((a, r) => a + r.sum, 0))}</td></tr>
    </tbody>`;
  $('#countOnly').innerHTML = ci.length ? `<h3>집계만 (점수 제외)</h3><div class="totals">${ci.map(it =>
    `<div class="total-box"><span>${esc(it.name)} 클랜 합계</span><b>${totals[it.id].toLocaleString('ko-KR')}</b><small>${fmt(totals[it.id])}</small></div>`).join('')}</div>` : '';

  // 취합표
  const admin = !!adminPw;
  const arrow = k => sortKey === k ? (sortDir < 0 ? ' ▼' : ' ▲') : '';
  const list = sheetRows();
  $('#rankTable').innerHTML = `
    <thead><tr><th>#</th><th data-sort="member">닉네임${arrow('member')}</th><th data-sort="score">점수${arrow('score')}</th>
      ${cfg.items.map(it => `<th data-sort="${it.id}">${esc(it.name)}${arrow(it.id)}<small>${scored(it) ? '×' + it.coef : '집계만'}</small></th>`).join('')}
      <th data-sort="time">제출${arrow('time')}</th>${admin ? '<th></th>' : ''}</tr></thead>
    <tbody>${list.map((r, i) => r.sub ? `
      <tr class="${r.roster ? '' : 'stale'}" title="${esc(r.sub.raw)}">
        <td>${i + 1}</td><td>${esc(r.member)}${r.roster ? '' : ' <small>(명단 외)</small>'}</td>
        <td class="score">${pts(r.score)}</td>
        ${cfg.items.map(it => `<td>${(r.values[it.id] ?? 0).toLocaleString('ko-KR')}</td>`).join('')}
        <td>${new Date(r.sub.submittedAt).toLocaleString('ko-KR', { month: 'numeric', day: 'numeric', weekday: 'short', hour: '2-digit', minute: '2-digit' })}</td>
        ${admin ? `<td><button class="icon-btn" data-del="${esc(r.member)}">삭제</button></td>` : ''}
      </tr>` : `
      <tr class="missing"><td>-</td><td>${esc(r.member)}</td><td>미제출</td>${cfg.items.map(() => '<td></td>').join('')}<td></td>${admin ? '<td></td>' : ''}</tr>`).join('')
      || `<tr><td colspan="${cfg.items.length + 4}" class="hint">클랜원 명단이 비어 있어요 (관리자 탭)</td></tr>`}
    </tbody>
    <tfoot><tr class="total"><td></td><td>합계</td><td class="score">${pts(list.reduce((a, r) => a + r.score, 0))}</td>
      ${cfg.items.map(it => `<td>${totals[it.id].toLocaleString('ko-KR')}</td>`).join('')}<td></td>${admin ? '<td></td>' : ''}</tr></tfoot>`;
  $('#rankTable').querySelectorAll('[data-sort]').forEach(th => th.onclick = () => {
    const k = th.dataset.sort;
    if (sortKey === k) sortDir = -sortDir; else { sortKey = k; sortDir = k === 'member' ? 1 : -1; }
    renderStatus();
  });
  $('#rankTable').querySelectorAll('[data-del]').forEach(b => b.onclick = async () => {
    if (!confirm(`${b.dataset.del} 님의 ${weekData.week} 제출을 삭제할까요?`)) return;
    try { await api('admin/submission', { method: 'DELETE', body: { week: weekData.week, member: b.dataset.del } }); loadWeek(weekData.week); }
    catch (e) { alert(e.message); }
  });
}

async function copy(text, btn) {
  try { await navigator.clipboard.writeText(text); }
  catch {
    // 일부 휴대폰 브라우저 대비
    const ta = document.createElement('textarea'); ta.value = text; document.body.appendChild(ta); ta.select();
    const ok = document.execCommand('copy'); ta.remove();
    if (!ok) { prompt('길게 눌러 복사하세요', text); return; }
  }
  const t = btn.textContent; btn.textContent = '복사됨 ✓'; setTimeout(() => btn.textContent = t, 1500);
}
$('#copyNag').onclick = e => copy($('#nagText').value, e.target);

function sheetMatrix() {
  const head = ['순위', '닉네임', '상태', '점수', ...cfg.items.map(it => it.name), '제출시각'];
  const body = sheetRows().map((r, i) => [r.sub ? i + 1 : '', r.member, r.sub ? '제출' : '미제출', r.sub ? Math.round(r.score) : '',
    ...cfg.items.map(it => r.sub ? (r.values[it.id] ?? 0) : ''), r.sub ? new Date(r.sub.submittedAt).toLocaleString('ko-KR') : '']);
  const { days, totals } = dayStats();
  const si = cfg.items.map((it, j) => [it, j]).filter(([it]) => scored(it));
  const sumRow = ['', '합계', '', Math.round(body.reduce((a, r) => a + (Number(r[3]) || 0), 0)), ...cfg.items.map(it => totals[it.id]), ''];
  const dayRows = [[], ['클랜전', '날짜', '', '합계', ...si.map(([it]) => it.name)],
    ...days.map(r => { const w = warDate(weekData.week, r.d); return [`Day ${r.d}`, `${w.md}(${w.dow})`, '', Math.round(r.sum), ...si.map(([, j]) => r.cells[j] == null ? '' : Math.round(r.cells[j]))]; })];
  return [head, ...body, sumRow, ...dayRows];
}
$('#copyTable').onclick = e => copy(sheetMatrix().map(r => r.join('\t')).join('\n'), e.target);

function downloadCsv(name, rows) {
  const q = v => /[",\n]/.test(String(v)) ? `"${String(v).replace(/"/g, '""')}"` : v;
  // 엑셀에서 한글이 안 깨지게 BOM 추가
  const blob = new Blob(['﻿' + rows.map(r => r.map(q).join(',')).join('\r\n')], { type: 'text/csv;charset=utf-8' });
  const a = document.createElement('a'); a.href = URL.createObjectURL(blob); a.download = name;
  document.body.appendChild(a); a.click(); a.remove();
  setTimeout(() => URL.revokeObjectURL(a.href), 1000);
}
$('#csvWeek').onclick = () => downloadCsv(`자원조사_${weekData.week}.csv`, sheetMatrix());
$('#csvAll').onclick = async () => {
  try {
    const { submissions } = await api('export');
    const rows = [['주차', '닉네임', '점수', ...cfg.items.map(it => it.name), '제출시각'],
      ...submissions.map(s => [s.week, s.member, Math.round(score(s.values, cfg.items)), ...cfg.items.map(it => s.values[it.id] ?? 0), new Date(s.submittedAt).toLocaleString('ko-KR')])];
    downloadCsv('자원조사_전체.csv', rows);
  } catch (e) { alert(e.message); }
};

// ---------------- 관리자 ----------------
$('#loginBox').onsubmit = async e => {
  e.preventDefault();
  adminPw = $('#adminPw').value;
  await adminLogin(true);
};
async function adminLogin(manual) {
  try {
    const r = await api('admin/login', { method: 'POST' });
    try { sessionStorage.setItem('adminPw', adminPw); } catch {}
    $('#loginBox').classList.add('hidden');
    $('#adminBox').classList.remove('hidden');
    renderItemEditor();
    $('#nagTemplate').value = cfg.settings?.nagTemplate || '';
    $('#memberText').value = r.members.filter(m => m.active).map(m => m.name).join('\n');
    countMembers();
  } catch (e) {
    adminPw = '';
    try { sessionStorage.removeItem('adminPw'); } catch {}
    if (manual) msg($('#loginMsg'), e.message, false);
  }
}
$('#logout').onclick = () => {
  adminPw = '';
  try { sessionStorage.removeItem('adminPw'); } catch {}
  $('#adminBox').classList.add('hidden');
  $('#loginBox').classList.remove('hidden');
};

let editItems = [];
function renderItemEditor() {
  editItems = cfg.items.map(it => ({ ...it, days: [...it.days], aliases: it.aliases.join(', '), isNew: false }));
  drawItems();
}
function drawItems() {
  $('#itemTable').innerHTML = `
    <thead><tr><th>이름</th><th>다른 이름</th><th>계수</th><th>쓰는 날</th><th>ID</th><th></th></tr></thead>
    <tbody>${editItems.map((it, i) => `
      <tr>
        <td><input data-i="${i}" data-k="name" value="${esc(it.name)}"></td>
        <td><input data-i="${i}" data-k="aliases" value="${esc(it.aliases)}" placeholder="예: 스킬, 스뽑"></td>
        <td><input class="coef" data-i="${i}" data-k="coef" value="${esc(it.coef)}" inputmode="decimal"></td>
        <td><div class="days">${[1, 2, 3, 4, 5].map(d => `<label>D${d}<input type="checkbox" data-i="${i}" data-day="${d}" ${it.days.includes(d) ? 'checked' : ''}></label>`).join('')}</div></td>
        <td><input class="id" data-i="${i}" data-k="id" value="${esc(it.id)}" ${it.isNew ? '' : 'readonly'}></td>
        <td>
          <button class="icon-btn" data-up="${i}" ${i === 0 ? 'disabled' : ''}>▲</button>
          <button class="icon-btn" data-rm="${i}">✕</button>
        </td>
      </tr>`).join('')}</tbody>`;
  const t = $('#itemTable');
  t.querySelectorAll('input[data-k]').forEach(inp => inp.oninput = () => { editItems[inp.dataset.i][inp.dataset.k] = inp.value; });
  t.querySelectorAll('input[data-day]').forEach(cb => cb.onchange = () => {
    const it = editItems[cb.dataset.i], d = +cb.dataset.day;
    it.days = cb.checked ? [...new Set([...it.days, d])].sort() : it.days.filter(x => x !== d);
  });
  t.querySelectorAll('[data-up]').forEach(b => b.onclick = () => { const i = +b.dataset.up; [editItems[i - 1], editItems[i]] = [editItems[i], editItems[i - 1]]; drawItems(); });
  t.querySelectorAll('[data-rm]').forEach(b => b.onclick = () => {
    if (!confirm(`"${editItems[b.dataset.rm].name}" 항목을 지울까요? (저장해야 반영)`)) return;
    editItems.splice(+b.dataset.rm, 1); drawItems();
  });
}
$('#addItem').onclick = () => {
  editItems.push({ id: 'item' + Date.now().toString(36), name: '', aliases: '', coef: 1, days: [], isNew: true });
  drawItems();
};
$('#saveItems').onclick = async () => {
  try {
    const r = await api('admin/items', { method: 'PUT', body: { items: editItems.map(it => ({ ...it, coef: Number(it.coef) })) } });
    cfg.items = r.items;
    renderItemEditor(); renderForm(); updatePreview();
    msg($('#itemMsg'), '저장했어요', true);
  } catch (e) { msg($('#itemMsg'), e.message, false); }
};

$('#saveNag').onclick = async () => {
  try {
    const r = await api('admin/settings', { method: 'PUT', body: { nagTemplate: $('#nagTemplate').value } });
    cfg.settings = r.settings;
    msg($('#nagMsg'), '저장했어요', true);
  } catch (e) { msg($('#nagMsg'), e.message, false); }
};

function countMembers() {
  const n = new Set($('#memberText').value.split('\n').map(s => s.trim()).filter(Boolean)).size;
  $('#memberCount').textContent = `${n}명`;
}
$('#memberText').oninput = countMembers;
$('#saveMembers').onclick = async () => {
  try {
    const r = await api('admin/members', { method: 'PUT', body: { members: $('#memberText').value.split('\n') } });
    cfg.members = r.members.filter(m => m.active).map(m => m.name);
    renderForm();
    msg($('#memberMsg'), `저장했어요 (${cfg.members.length}명)`, true);
  } catch (e) { msg($('#memberMsg'), e.message, false); }
};

// ---------------- 시작 ----------------
(async function init() {
  try {
    cfg = await api('config');
    thisWeek = await api('week?week=' + cfg.week);
  } catch (e) {
    document.querySelector('main').innerHTML = `<div class="card msg err">불러오기 실패: ${esc(e.message)}</div>`;
    return;
  }
  $('#weekLabel').textContent = cfg.week;
  if (cfg.requireCode) { $('#codeRow').classList.remove('hidden'); $('#clanCode').value = store.get('clanCode'); }
  renderForm();
  if (adminPw) adminLogin(false);
  const t = location.hash.slice(1);
  if (['submit', 'status', 'admin'].includes(t)) showTab(t);
})();
