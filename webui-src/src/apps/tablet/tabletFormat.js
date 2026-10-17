/**
 * 平板电脑的纯数据整理与纯文本导出。不依赖 DOM 与 Vue，便于单元测试。
 */

function pad(n) {
  return String(n).padStart(2, '0');
}

/** 导出时间戳，形如 2026-10-17 12:00:00。 */
export function stampOf(date = new Date()) {
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} `
    + `${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
}

/** 文件名用的紧凑时间戳，形如 20261017_120000。 */
export function fileStampOf(date = new Date()) {
  return `${date.getFullYear()}${pad(date.getMonth() + 1)}${pad(date.getDate())}_`
    + `${pad(date.getHours())}${pad(date.getMinutes())}${pad(date.getSeconds())}`;
}

/** 线索栏的排序：命题在前（保持到达顺序），线索按游戏内时间从晚到早。 */
export function orderClues(clues) {
  const propositions = clues.filter((c) => c.isProposition);
  const plain = clues.filter((c) => !c.isProposition)
    .map((c, i) => ({ c, i }))
    .sort((a, b) => (b.c.time - a.c.time) || (a.i - b.i))
    .map((x) => x.c);
  return [...propositions, ...plain];
}

/** 聊天条目的正文（不含名字与时间）。 */
export function chatBody(line) {
  if (line.kind === 'photo') return '[图片]';
  if (line.kind === 'state') return `—— ${line.text} ——`;
  if (line.kind === 'proposition') return `【命题】${line.text}`;
  return line.text || '';
}

/** 聊天记录的纯文本。每行一条，附本机时间与发言人。 */
export function formatChatText(lines, exportedAt = new Date()) {
  const out = [`【聊天记录】导出于 ${stampOf(exportedAt)}，共 ${lines.length} 条`, ''];
  for (const l of lines) {
    if (l.kind === 'state') {
      out.push(`[${l.time}] ${chatBody(l)}`);
      continue;
    }
    const dead = l.isDead ? '（已死亡）' : '';
    out.push(`[${l.time}] ${l.name}${dead}：${chatBody(l)}`);
  }
  return `${out.join('\n')}\n`;
}

/** 线索的纯文本。命题写出提交者与句子，线索写出对象、地点与时间。 */
export function formatClueText(clues, exportedAt = new Date()) {
  const out = [`【线索】导出于 ${stampOf(exportedAt)}，共 ${clues.length} 条`, ''];
  for (const c of clues) {
    const fields = `对象：${c.who}　地点：${c.where}　时间：${c.when}`;
    const tail = c.destroyed ? '（已销毁）' : '';
    if (c.isProposition) {
      out.push(`[命题] ${c.ownerName}：${c.sentence || ''}${tail}`);
      out.push(`    ${fields}`);
    } else {
      out.push(`[线索] ${fields}${tail}`);
    }
  }
  return `${out.join('\n')}\n`;
}

/** 选择列表里一条线索的简短预览。 */
export function clueSummary(c) {
  if (c.isProposition) return `${c.ownerName}：${c.sentence || ''}`;
  return `${c.who} · ${c.where} · ${c.when}`;
}

/** 选择列表里一条聊天的简短预览。 */
export function chatSummary(l) {
  if (l.kind === 'state') return chatBody(l);
  const body = chatBody(l);
  return `${l.name}：${body.length > 40 ? `${body.slice(0, 40)}…` : body}`;
}
