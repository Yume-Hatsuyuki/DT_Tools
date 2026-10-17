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

/**
 * 线索栏的排序：按局分组，较早的局在前（与聊天一致）；局内按游戏内时间从早到晚，
 * 与游戏 UI_GameTablet.RefreshClueList 的 orderby 完全一致：
 * 时间 → 命题在前 → 对象玩家编号 → 房间编号；仍相同的按到达顺序。
 * 同一局的线索在结果中连续出现，供 groupByRound 分段。
 */
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

/**
 * 局的显示编号：按局号从小到大编为第 1、2… 局。传入的线索决定编号范围，界面与导出都用同一份全量线索计算。
 * @returns {Map<number, number>} 局号 → 编号。
 */
export function roundNumbering(clues) {
  const rounds = [...new Set(clues.map((c) => c.round))].sort((a, b) => a - b);
  return new Map(rounds.map((r, i) => [r, i + 1]));
}

/** 线索的稳定标识。局内 key 唯一，跨局可能重复，所以带上局号。 */
export function clueId(c) {
  return `${c.round}|${c.key}`;
}

/**
 * 把已排序的条目按连续的同一局分组（聊天与线索通用，条目需带 round 字段）。
 * @returns {Array<{ round: number, no: number, items: object[] }>} no 为 numbering 中的编号。
 */
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

/** 把文本压成一行：换行与其周围的空白合并为一个空格（命题句在游戏内可能含换行）。 */
export function singleLine(text) {
  return String(text ?? '').replace(/\s*\n\s*/g, ' ').trim();
}

/** 聊天条目的正文（不含名字与时间）。命题保持单行。 */
export function chatBody(line) {
  if (line.kind === 'photo') return '[图片]';
  if (line.kind === 'state') return `—— ${line.text} ——`;
  if (line.kind === 'proposition') return `【命题】${singleLine(line.text)}`;
  return line.text || '';
}

/** 聊天记录的纯文本。每行一条，附本机时间与发言人。 */
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

/** 线索的纯文本。命题写出提交者与句子，线索写出对象、地点与时间。 */
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

/** 选择列表里一条线索的简短预览。 */
export function clueSummary(c) {
  if (c.isProposition) return `${c.ownerName}：${singleLine(c.sentence)}`;
  return `${c.who} · ${c.where} · ${c.when}`;
}

/** 选择列表里一条聊天的简短预览。 */
export function chatSummary(l) {
  if (l.kind === 'state') return chatBody(l);
  const body = chatBody(l);
  return `${l.name}：${body.length > 40 ? `${body.slice(0, 40)}…` : body}`;
}
