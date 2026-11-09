// 숫자·한 줄 입력 해석기 (브라우저 전용, 서버는 숫자만 검증)

const UNIT = { k: 1e3, m: 1e6, b: 1e9, '천': 1e3, '만': 1e4, '억': 1e8 };

// 공백 제거 + 소문자 (항목 이름 비교용)
export function norm(s) {
  return String(s || '').replace(/\s+/g, '').toLowerCase();
}

// "346k" "2.7K" "1,840개" "1.2m" "3만" → 숫자. 해석 불가면 null
export function parseAmount(text) {
  let s = String(text ?? '').trim().replace(/,/g, '').replace(/\s+/g, '');
  if (s === '') return null;
  s = s.replace(/(개|회|번|ea)$/i, '');
  const m = s.match(/^(\d+(?:\.\d+)?)(k|m|b|천|만|억)?$/i);
  if (!m) return null;
  const n = parseFloat(m[1]) * (m[2] ? UNIT[m[2].toLowerCase()] : 1);
  return Math.round(n);
}

// 보기 좋은 숫자: 346000 → "346k", 2700 → "2.7k"
export function fmt(n) {
  if (n == null || isNaN(n)) return '-';
  const a = Math.abs(n);
  if (a >= 1e9) return +(n / 1e9).toFixed(2) + 'b';
  if (a >= 1e6) return +(n / 1e6).toFixed(2) + 'm';
  if (a >= 1e4) return +(n / 1e3).toFixed(1) + 'k';
  return n.toLocaleString('ko-KR');
}

// items: [{id, name, aliases:[...]}]  members: ["daye", ...]
// "daye, 스킬포인트 346k, 펫포인트 118k" → { member, values:{id:n}, warnings:[] }
export function parseLine(text, items, members) {
  const parts = String(text || '').split(/[,，\n]+/).map(s => s.trim()).filter(Boolean);
  const out = { member: null, values: {}, warnings: [] };

  // 항목 이름 후보 (긴 것부터 비교해서 "스킬포인트"가 "스킬"보다 먼저 맞도록)
  const keys = [];
  for (const it of items) {
    for (const k of [it.name, ...(it.aliases || [])]) {
      const nk = norm(k);
      if (nk) keys.push({ key: nk, id: it.id, name: it.name });
    }
  }
  keys.sort((a, b) => b.key.length - a.key.length);

  parts.forEach((part, idx) => {
    const np = norm(part);
    const hit = keys.find(k => np.startsWith(k.key));
    if (hit) {
      const amount = parseAmount(np.slice(hit.key.length).replace(/^[:=]/, ''));
      if (amount == null) out.warnings.push(`"${part}" 숫자를 읽지 못했어요`);
      else {
        if (hit.id in out.values) out.warnings.push(`${hit.name} 이(가) 두 번 나와서 뒤의 값을 썼어요`);
        out.values[hit.id] = amount;
      }
      return;
    }
    if (idx === 0) {
      // 첫 칸은 닉네임
      const found = members.find(m => norm(m) === np);
      if (found) out.member = found;
      else out.warnings.push(`닉네임 "${part}" 이(가) 클랜원 명단에 없어요`);
      return;
    }
    out.warnings.push(`"${part}" 은(는) 어떤 항목인지 모르겠어요`);
  });
  return out;
}

// 쓰는 날(days)이 없는 항목은 '집계만' → 점수에서 뺌
export const scored = it => Array.isArray(it.days) && it.days.length > 0;

export function score(values, items) {
  let s = 0;
  for (const it of items.filter(scored)) s += (Number(values?.[it.id]) || 0) * (Number(it.coef) || 0);
  return s;
}
