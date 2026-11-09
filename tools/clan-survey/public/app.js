import { parseAmount, parseLine, fmt, score } from './parse.js';

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
$('#weekSel').onchange = e => loadWeek(e.target.value);
async function loadWeek(week) {
  try { weekData = await api('week?week=' + week); } catch (e) { $('#rankTable').innerHTML = `<tr><td class="msg err">${esc(e.message)}</td></tr>`; return; }
  $('#weekSel').innerHTML = weekData.weeks.map(w => `<option value="${w}" ${w === weekData.week ? 'selected' : ''}>${w}${w === weekData.current ? ' (이번 주)' : ''}</option>`).join('');
  renderStatus();
}

function rows() {
  return weekData.submissions
    .map(s => ({ ...s, score: score(s.values, cfg.items), roster: cfg.members.includes(s.member) }))
    .sort((a, b) => b.score - a.score);
}

function renderStatus() {
  const subs = rows();
  const done = new Set(subs.map(s => s.member));
  const missing = cfg.members.filter(m => !done.has(m));
  $('#missingCount').textContent = `${missing.length}명`;
  $('#doneCount').textContent = `${subs.filter(s => s.roster).length} / ${cfg.members.length}명 제출`;
  $('#missingList').innerHTML = missing.length
    ? missing.map(m => `<span class="chip">${esc(m)}</span>`).join('')
    : '<span class="done-all">🎉 전원 제출 완료</span>';
  const admin = !!adminPw;
  $('#rankTable').innerHTML = `
    <thead><tr><th>#</th><th>닉네임</th><th>점수</th>
      ${cfg.items.map(it => `<th>${esc(it.name)}<small>×${it.coef}</small></th>`).join('')}
      <th>제출</th>${admin ? '<th></th>' : ''}</tr></thead>
    <tbody>${subs.map((s, i) => `
      <tr class="${s.roster ? '' : 'stale'}" title="${esc(s.raw)}">
        <td>${i + 1}</td><td>${esc(s.member)}${s.roster ? '' : ' <small>(명단 외)</small>'}</td>
        <td class="score">${fmt(s.score)}</td>
        ${cfg.items.map(it => `<td>${fmt(s.values[it.id] ?? 0)}</td>`).join('')}
        <td>${new Date(s.submittedAt).toLocaleDateString('ko-KR', { month: 'numeric', day: 'numeric', weekday: 'short' })}</td>
        ${admin ? `<td><button class="icon-btn" data-del="${esc(s.member)}">삭제</button></td>` : ''}
      </tr>`).join('') || `<tr><td colspan="${cfg.items.length + 4}" class="hint">아직 제출이 없어요</td></tr>`}
    </tbody>`;
  $('#rankTable').querySelectorAll('[data-del]').forEach(b => b.onclick = async () => {
    if (!confirm(`${b.dataset.del} 님의 ${weekData.week} 제출을 삭제할까요?`)) return;
    try { await api('admin/submission', { method: 'DELETE', body: { week: weekData.week, member: b.dataset.del } }); loadWeek(weekData.week); }
    catch (e) { alert(e.message); }
  });
}

async function copy(text, btn) {
  try { await navigator.clipboard.writeText(text); } catch { prompt('복사해서 쓰세요', text); return; }
  const t = btn.textContent; btn.textContent = '복사됨 ✓'; setTimeout(() => btn.textContent = t, 1500);
}
$('#copyMissing').onclick = e => {
  const done = new Set(weekData.submissions.map(s => s.member));
  const missing = cfg.members.filter(m => !done.has(m));
  copy(`[${weekData.week} 주차 자원 조사 미제출] ${missing.join(', ') || '없음'}`, e.target);
};
$('#copyTable').onclick = e => {
  const head = ['순위', '닉네임', '점수', ...cfg.items.map(it => it.name)];
  const body = rows().map((s, i) => [i + 1, s.member, Math.round(s.score * 100) / 100, ...cfg.items.map(it => s.values[it.id] ?? 0)]);
  copy([head, ...body].map(r => r.join('\t')).join('\n'), e.target);
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
  editItems = cfg.items.map(it => ({ ...it, aliases: it.aliases.join(', '), isNew: false }));
  drawItems();
}
function drawItems() {
  $('#itemTable').innerHTML = `
    <thead><tr><th>이름</th><th>다른 이름</th><th>계수</th><th>ID</th><th></th></tr></thead>
    <tbody>${editItems.map((it, i) => `
      <tr>
        <td><input data-i="${i}" data-k="name" value="${esc(it.name)}"></td>
        <td><input data-i="${i}" data-k="aliases" value="${esc(it.aliases)}" placeholder="예: 스킬, 스뽑"></td>
        <td><input class="coef" data-i="${i}" data-k="coef" value="${esc(it.coef)}" inputmode="decimal"></td>
        <td><input class="id" data-i="${i}" data-k="id" value="${esc(it.id)}" ${it.isNew ? '' : 'readonly'}></td>
        <td>
          <button class="icon-btn" data-up="${i}" ${i === 0 ? 'disabled' : ''}>▲</button>
          <button class="icon-btn" data-rm="${i}">✕</button>
        </td>
      </tr>`).join('')}</tbody>`;
  const t = $('#itemTable');
  t.querySelectorAll('input').forEach(inp => inp.oninput = () => { editItems[inp.dataset.i][inp.dataset.k] = inp.value; });
  t.querySelectorAll('[data-up]').forEach(b => b.onclick = () => { const i = +b.dataset.up; [editItems[i - 1], editItems[i]] = [editItems[i], editItems[i - 1]]; drawItems(); });
  t.querySelectorAll('[data-rm]').forEach(b => b.onclick = () => {
    if (!confirm(`"${editItems[b.dataset.rm].name}" 항목을 지울까요? (저장해야 반영)`)) return;
    editItems.splice(+b.dataset.rm, 1); drawItems();
  });
}
$('#addItem').onclick = () => {
  editItems.push({ id: 'item' + Date.now().toString(36), name: '', aliases: '', coef: 1, isNew: true });
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
