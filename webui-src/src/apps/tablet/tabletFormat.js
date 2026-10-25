
function pad(n) {
  return String(n).padStart(2, '0');
}

export function stampOf(date = new Date()) {
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} `
    + `${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
}

export function fileStampOf(date = new Date()) {
  return `${date.getFullYear()}${pad(date.getMonth() + 1)}${pad(date.getDate())}_`
    + `${pad(date.getHours())}${pad(date.getMinutes())}${pad(date.getSeconds())}`;
}

export function orderClues(clues) {
  const rounds = [...new Set(clues.map((c) => c.round))].sort((a, b) => a - b);
  return rounds.flatMap((r) => orderWithinRound(clues.filter((c) => c.round === r)));
}

function orderWithinRound(clues) {
  return clues
    .map((c, i) => ({ c, i }))
    .sort((a, b) => (a.c.time - b.c.time)
      || (Number(!a.c.isProposition) - Number(!b.c.isProposition))
      || ((a.c.targetId ?? 0) - (b.c.targetId ?? 0))
      || ((a.c.roomId ?? 0) - (b.c.roomId ?? 0))
      || (a.i - b.i))
    .map((x) => x.c);
}

export function roundNumbering(clues) {
  const rounds = [...new Set(clues.map((c) => c.round))].sort((a, b) => a - b);
  return new Map(rounds.map((r, i) => [r, i + 1]));
}

export function clueId(c) {
  return `${c.round}|${c.key}`;
}

export function groupByRound(items, numbering) {
  const groups = [];
  for (const it of items) {
    const last = groups[groups.length - 1];
    if (last && last.round === it.round) {
      last.items.push(it);
    } else {
      groups.push({ round: it.round, no: numbering.get(it.round) ?? 0, items: [it] });
    }
  }
  return groups;
}

export function singleLine(text) {
  return String(text ?? '').replace(/\s*\n\s*/g, ' ').trim();
}

export function chatBody(line) {
  if (line.kind === 'photo') return '[图片]';
  if (line.kind === 'state') return `—— ${line.text} ——`;
  if (line.kind === 'proposition') return `【命题】${singleLine(line.text)}`;
  return line.text || '';
}

export function formatChatText(lines, exportedAt = new Date(), numbering = new Map()) {
  const out = [`【聊天记录】导出于 ${stampOf(exportedAt)}，共 ${lines.length} 条`, ''];
  for (const g of groupByRound(lines, numbering)) {
    out.push(`—— 第 ${g.no} 局 ——`);
    for (const l of g.items) {
      if (l.kind === 'state') {
        out.push(`[${l.time}] ${chatBody(l)}`);
        continue;
      }
      const dead = l.isDead ? '（已死亡）' : '';
      out.push(`[${l.time}] ${l.name}${dead}：${chatBody(l)}`);
    }
    out.push('');
  }
  while (out.length && out[out.length - 1] === '') out.pop();
  return `${out.join('\n')}\n`;
}

export function formatClueText(clues, exportedAt = new Date(), numbering = new Map()) {
  const out = [`【线索】导出于 ${stampOf(exportedAt)}，共 ${clues.length} 条`, ''];
  for (const g of groupByRound(clues, numbering)) {
    out.push(`—— 第 ${g.no} 局 ——`);
    for (const c of g.items) {
      const fields = `对象：${c.who}　地点：${c.where}　时间：${c.when}`;
      const tail = c.destroyed ? '（已销毁）' : '';
      if (c.isProposition) {
        out.push(`[命题] ${c.ownerName}：${singleLine(c.sentence)}${tail}　${fields}`);
      } else {
        out.push(`[线索] ${fields}${tail}`);
      }
    }
    out.push('');
  }
  while (out.length && out[out.length - 1] === '') out.pop();
  return `${out.join('\n')}\n`;
}

export function clueSummary(c) {
  if (c.isProposition) return `${c.ownerName}：${singleLine(c.sentence)}`;
  return `${c.who} · ${c.where} · ${c.when}`;
}

export function chatSummary(l) {
  if (l.kind === 'state') return chatBody(l);
  const body = chatBody(l);
  return `${l.name}：${body.length > 40 ? `${body.slice(0, 40)}…` : body}`;
}
