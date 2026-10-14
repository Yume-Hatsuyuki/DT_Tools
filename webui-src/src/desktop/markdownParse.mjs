/**
 * 更新说明的 Markdown 解析器（GFM 子集）。输出语法树，不输出 HTML。
 * 渲染端用 Vue 的 h() 从语法树构建节点，文本经 Vue 转义，所以原始 HTML 只会显示为文字。
 * 解析规则按 CommonMark / GFM 的常见行为实现；没有实现的语法（如脚注、定义列表）按普通文本处理。
 */

/** 源文本长度上限：超过部分直接截断，避免超大的更新说明阻塞界面。 */
export const MAX_SOURCE_LENGTH = 200000;

/** 块级嵌套（引用、列表）的最大深度；超过后内容降级为段落文本，防止递归过深。 */
export const MAX_DEPTH = 6;

const FENCE_RE = /^( {0,3})(`{3,}|~{3,})(.*)$/;
const ATX_RE = /^ {0,3}(#{1,6})(?:[ \t]+(.*?))?[ \t]*$/;
const HR_RE = /^ {0,3}([-*_])(?:[ \t]*\1){2,}[ \t]*$/;
const QUOTE_RE = /^ {0,3}> ?(.*)$/;
const BULLET_RE = /^( {0,3})([-*+])( +|$)(.*)$/;
const ORDERED_RE = /^( {0,3})(\d{1,9})([.)])( +|$)(.*)$/;
const TASK_RE = /^\[([ xX])\](?:[ \t]+|$)(.*)$/;
const TABLE_DELIM_CELL_RE = /^:?-+:?$/;
const SAFE_HREF_RE = /^(https?:\/\/|mailto:)[^\s<>"]+$/i;
const ESCAPABLE_RE = /[!-\/:-@\[-`{-~]/;

/**
 * 解析 Markdown 源文本，返回块级节点数组。
 * @param {string} source 更新说明原文，可以为 null。
 * @returns {Array<object>} 块级语法树。
 */
export function parseMarkdown(source) {
  let text = typeof source === 'string' ? source : '';
  if (text.length > MAX_SOURCE_LENGTH) text = text.slice(0, MAX_SOURCE_LENGTH);
  const lines = text.replace(/\r\n?/g, '\n').replace(/\t/g, '    ').split('\n');
  return parseBlocks(lines, 0);
}

function isBlank(line) {
  return line.trim() === '';
}

function indentOf(line) {
  return line.match(/^ */)[0].length;
}

function fenceOpen(line) {
  const m = line.match(FENCE_RE);
  if (!m) return null;
  const marker = m[2];
  const info = m[3].trim();
  // 反引号围栏的信息串里不能再出现反引号，否则它是行内代码
  if (marker[0] === '`' && info.includes('`')) return null;
  return { char: marker[0], len: marker.length, indent: m[1].length, lang: info.split(/\s+/)[0] || '' };
}

function isFenceClose(line, open) {
  const m = line.match(/^ {0,3}(`{3,}|~{3,})[ \t]*$/);
  return !!m && m[1][0] === open.char && m[1].length >= open.len;
}

/**
 * 判断一行是否会打断段落（或作为惰性续行的边界）。
 * CommonMark 规定：只有从 1 开始的有序列表才能打断段落。
 */
function startsBlock(line) {
  if (isBlank(line)) return true;
  if (fenceOpen(line)) return true;
  if (ATX_RE.test(line) || HR_RE.test(line) || QUOTE_RE.test(line)) return true;
  if (BULLET_RE.test(line)) return true;
  const o = line.match(ORDERED_RE);
  return !!o && o[2] === '1';
}

function parseBlocks(lines, depth) {
  const out = [];
  const n = lines.length;
  let i = 0;
  while (i < n) {
    const line = lines[i];
    if (isBlank(line)) {
      i++;
      continue;
    }

    // 围栏代码块：内容原样保留，不做任何 Markdown 解析
    const fence = fenceOpen(line);
    if (fence) {
      const body = [];
      const strip = new RegExp('^ {0,' + fence.indent + '}');
      i++;
      while (i < n && !isFenceClose(lines[i], fence)) {
        body.push(lines[i].replace(strip, ''));
        i++;
      }
      i++; // 跳过关闭行；没有关闭行时已经到达末尾
      out.push({ type: 'code', lang: fence.lang, text: body.join('\n') });
      continue;
    }

    // 缩进代码块（4 个空格）：只在块开始处生效，列表内的缩进由列表处理
    if (/^ {4}/.test(line)) {
      const body = [];
      while (i < n && (isBlank(lines[i]) || /^ {4}/.test(lines[i]))) {
        body.push(lines[i].replace(/^ {4}/, ''));
        i++;
      }
      while (body.length && isBlank(body[body.length - 1])) body.pop();
      out.push({ type: 'code', lang: '', text: body.join('\n') });
      continue;
    }

    // ATX 标题：# 后必须有空格，否则 #标签 只是普通文字
    const atx = line.match(ATX_RE);
    if (atx) {
      const text = (atx[2] || '').replace(/[ \t]+#+[ \t]*$/, '').replace(/^#+[ \t]*$/, '');
      out.push({ type: 'heading', level: atx[1].length, children: parseInline(text.trim()) });
      i++;
      continue;
    }

    // 分隔线：必须先于列表判断，否则 "* * *" 会被当成列表
    if (HR_RE.test(line)) {
      out.push({ type: 'hr' });
      i++;
      continue;
    }

    // 引用：连续的 > 行，加上紧随其后的惰性续行
    if (QUOTE_RE.test(line)) {
      const inner = [];
      while (i < n && QUOTE_RE.test(lines[i])) {
        inner.push(lines[i].replace(QUOTE_RE, '$1'));
        i++;
      }
      while (i < n && !startsBlock(lines[i])) {
        inner.push(lines[i]);
        i++;
      }
      out.push({ type: 'quote', children: nestOrFlatten(inner, depth) });
      continue;
    }

    // 表格：表头行 + 分隔行（列数必须一致），之后的行直到空行或块起始为止
    if (line.includes('|') && i + 1 < n && isTableDelim(lines[i + 1])) {
      const head = splitRow(line);
      const delim = splitRow(lines[i + 1]);
      if (head.length === delim.length) {
        const align = delim.map(parseAlign);
        i += 2;
        const rows = [];
        while (i < n && !isBlank(lines[i]) && !startsBlock(lines[i])) {
          const cells = splitRow(lines[i]);
          rows.push(head.map((_, k) => (k < cells.length ? cells[k] : '')).map(parseInline));
          i++;
        }
        out.push({ type: 'table', align, head: head.map(parseInline), rows });
        continue;
      }
    }

    // 列表（无序或有序）
    const bm = line.match(BULLET_RE);
    const om = bm ? null : line.match(ORDERED_RE);
    if (bm || om) {
      const ordered = !!om;
      const start = ordered ? parseInt(om[2], 10) : 1;
      const delim = ordered ? om[3] : bm[2];
      const items = [];
      while (i < n) {
        // 项与项之间允许空行（松散列表）；空行之后必须紧跟同类标记才继续
        let k = i;
        while (k < n && isBlank(lines[k])) k++;
        if (k >= n) break;
        const m = matchItem(lines[k], ordered, delim);
        if (!m) break;
        i = k;
        const contentIndent = m.indent + m.marker.length + m.space;
        const itemLines = [m.content];
        i++;
        while (i < n) {
          const l = lines[i];
          if (isBlank(l)) {
            let k2 = i;
            while (k2 < n && isBlank(lines[k2])) k2++;
            if (k2 < n && indentOf(lines[k2]) >= contentIndent) {
              for (; i < k2; i++) itemLines.push('');
              continue;
            }
            break;
          }
          if (indentOf(l) >= contentIndent) {
            itemLines.push(l.slice(contentIndent));
            i++;
            continue;
          }
          // 同一列表的下一项（如 "4. y"）不是续行，即使它不能打断段落
          if (matchItem(l, ordered, delim)) break;
          if (!startsBlock(l)) {
            itemLines.push(l.trim());
            i++;
            continue;
          }
          break;
        }
        let task = null;
        const tm = itemLines[0].match(TASK_RE);
        if (tm) {
          task = tm[1] === ' ' ? 'unchecked' : 'checked';
          itemLines[0] = tm[2];
        }
        items.push({ task, children: nestOrFlatten(itemLines, depth) });
      }
      out.push({ type: 'list', ordered, start, items });
      continue;
    }

    // 段落：收集到空行或块起始为止；去掉行首空格，保留行尾空格（硬换行依赖它）
    const para = [line];
    i++;
    while (i < n && !startsBlock(lines[i])) {
      para.push(lines[i]);
      i++;
    }
    out.push({ type: 'paragraph', children: parseInline(para.map((s) => s.replace(/^ +/, '')).join('\n')) });
  }
  return out;
}

/** 嵌套内容：未超过深度上限时递归解析块级结构；超过则降级为一段纯文字。 */
function nestOrFlatten(lines, depth) {
  // 子内容的解析深度为 depth + 1；达到上限后不再创建容器，改为段落文字
  if (depth + 1 >= MAX_DEPTH) {
    const text = lines.map((s) => s.trim()).filter((s) => s !== '').join(' ');
    return [{ type: 'paragraph', children: parseInline(text) }];
  }
  return parseBlocks(lines, depth + 1);
}

function matchItem(line, ordered, delim) {
  if (ordered) {
    const m = line.match(ORDERED_RE);
    if (!m || m[3] !== delim) return null;
    return { indent: m[1].length, marker: m[2] + m[3], space: spaceWidth(m[4]), content: m[5] };
  }
  const m = line.match(BULLET_RE);
  if (!m || m[2] !== delim) return null;
  return { indent: m[1].length, marker: m[2], space: spaceWidth(m[3]), content: m[4] };
}

/** 标记后的空格数：超过 4 个按 CommonMark 视为内容缩进，只算 1 个。 */
function spaceWidth(spaces) {
  if (!spaces) return 1;
  return spaces.length > 4 ? 1 : spaces.length;
}

function isTableDelim(line) {
  if (!line.includes('|')) return false;
  const cells = splitRow(line);
  return cells.length > 0 && cells.every((c) => TABLE_DELIM_CELL_RE.test(c));
}

function parseAlign(cell) {
  const left = cell.startsWith(':');
  const right = cell.endsWith(':');
  if (left && right) return 'center';
  if (right) return 'right';
  if (left) return 'left';
  return null;
}

/** 按未转义的竖线切分表格行；\| 表示单元格内的竖线本身。 */
function splitRow(line) {
  let s = line.trim();
  if (s.startsWith('|')) s = s.slice(1);
  if (s.endsWith('|') && !s.endsWith('\\|')) s = s.slice(0, -1);
  const cells = [];
  let cur = '';
  for (let k = 0; k < s.length; k++) {
    const c = s[k];
    if (c === '\\' && s[k + 1] === '|') {
      cur += '|';
      k++;
      continue;
    }
    if (c === '|') {
      cells.push(cur.trim());
      cur = '';
      continue;
    }
    cur += c;
  }
  cells.push(cur.trim());
  return cells;
}

/** 只放行 http、https、mailto，其它协议（如 javascript:）一律不生成链接。 */
function safeHref(u) {
  const s = (u || '').trim();
  return SAFE_HREF_RE.test(s) ? s : null;
}

function pushLink(out, href, children) {
  if (href) out.push({ type: 'link', href, children });
  else out.push(...children);
}

function leftOk(src, i, c) {
  // 下划线出现在单词中间（如 foo_bar）时不作为强调标记
  if (c === '_' && i > 0 && /[A-Za-z0-9]/.test(src[i - 1])) return false;
  return true;
}

function findDelim(src, from, delim) {
  let k = from;
  while (k < src.length) {
    if (src[k] === '\\') {
      k += 2;
      continue;
    }
    if (src.startsWith(delim, k) && k > from && !/\s/.test(src[k - 1])) return k;
    k++;
  }
  return -1;
}

function findSingle(src, from, ch) {
  let k = from;
  while (k < src.length) {
    if (src[k] === '\\') {
      k += 2;
      continue;
    }
    if (src[k] === ch && k > from && !/\s/.test(src[k - 1]) && src[k + 1] !== ch) {
      if (!(ch === '_' && /[A-Za-z0-9]/.test(src[k + 1] || ''))) return k;
    }
    k++;
  }
  return -1;
}

/** 寻找与开头等长的反引号串，返回其起始位置；找不到返回 -1。 */
function findBacktickRun(src, from, n) {
  let k = from;
  while (k < src.length) {
    if (src[k] === '`') {
      let r = 0;
      while (src[k + r] === '`') r++;
      if (r === n) return k;
      k += r;
    } else {
      k++;
    }
  }
  return -1;
}

/** CommonMark：行内代码首尾各有一个空格且内容不全是空格时，去掉这一对空格。 */
function trimCodeSpan(s) {
  if (s.length >= 2 && s[0] === ' ' && s[s.length - 1] === ' ' && /[^ ]/.test(s)) return s.slice(1, -1);
  return s;
}

/** 解析 [文本](地址 "标题")，支持嵌套方括号与括号；失败返回 null。 */
function parseLinkAt(src, start) {
  if (src[start] !== '[') return null;
  let depth = 0;
  let k = start;
  for (; k < src.length; k++) {
    const ch = src[k];
    if (ch === '\\') {
      k++;
      continue;
    }
    if (ch === '[') {
      depth++;
    } else if (ch === ']') {
      depth--;
      if (depth === 0) break;
    }
  }
  if (k >= src.length || src[k + 1] !== '(') return null;
  const label = src.slice(start + 1, k);
  let p = k + 2;
  let pd = 1;
  let dest = '';
  for (; p < src.length; p++) {
    const ch = src[p];
    if (ch === '\\' && p + 1 < src.length) {
      dest += ch + src[p + 1];
      p++;
      continue;
    }
    if (ch === '(') {
      pd++;
    } else if (ch === ')') {
      pd--;
      if (pd === 0) break;
    }
    dest += ch;
  }
  if (p >= src.length) return null;
  let target = dest.trim();
  if (target.startsWith('<') && target.endsWith('>')) target = target.slice(1, -1);
  const m = target.match(/^(\S+)(?:\s+(?:"[^"]*"|'[^']*'))?$/);
  if (!m) return null;
  return { label, href: safeHref(m[1]), end: p + 1 };
}

/**
 * 解析行内语法。顺序敏感：转义 > 代码 > 换行 > 图片 > 链接 > 自动链接 > 删除线 > 加粗 > 斜体。
 * @param {string} src 单个块内的文本（段落可能含换行）。
 * @returns {Array<object>} 行内节点数组。
 */
function parseInline(src) {
  const out = [];
  let buf = '';
  const flush = () => {
    if (buf) {
      out.push({ type: 'text', value: buf });
      buf = '';
    }
  };
  const n = src.length;
  let i = 0;
  while (i < n) {
    const c = src[i];

    if (c === '\\') {
      const nx = src[i + 1];
      if (nx === '\n') {
        flush();
        out.push({ type: 'br' });
        i += 2;
        continue;
      }
      if (nx !== undefined && ESCAPABLE_RE.test(nx)) {
        buf += nx;
        i += 2;
        continue;
      }
      buf += c;
      i++;
      continue;
    }

    if (c === '`') {
      let run = 0;
      while (src[i + run] === '`') run++;
      const close = findBacktickRun(src, i + run, run);
      if (close >= 0) {
        flush();
        out.push({ type: 'code', value: trimCodeSpan(src.slice(i + run, close)) });
        i = close + run;
        continue;
      }
      buf += src.slice(i, i + run);
      i += run;
      continue;
    }

    if (c === '\n') {
      // 行尾两个以上空格才是硬换行；否则按空格处理
      if (/ {2}$/.test(buf)) {
        buf = buf.replace(/ +$/, '');
        flush();
        out.push({ type: 'br' });
      } else {
        buf = buf.replace(/ +$/, '') + ' ';
      }
      i++;
      continue;
    }

    if (c === '!' && src[i + 1] === '[') {
      const link = parseLinkAt(src, i + 1);
      if (link) {
        flush();
        out.push({ type: 'image', alt: link.label, href: link.href });
        i = link.end;
        continue;
      }
    }

    if (c === '[') {
      const link = parseLinkAt(src, i);
      if (link) {
        flush();
        pushLink(out, link.href, parseInline(link.label));
        i = link.end;
        continue;
      }
    }

    if (c === '<') {
      const m = src.slice(i).match(/^<(https?:\/\/[^\s<>]+)>/i);
      if (m) {
        flush();
        pushLink(out, safeHref(m[1]), [{ type: 'text', value: m[1] }]);
        i += m[0].length;
        continue;
      }
    }

    // 裸链接（GFM 自动链接）：只在单词边界处识别，末尾的标点不算进链接
    if ((c === 'h' || c === 'H') && (i === 0 || /[\s(\[*_~>]/.test(src[i - 1]))) {
      const m = src.slice(i).match(/^https?:\/\/[^\s<]*[^\s<.,:;"')\]!?*_~]/i);
      if (m) {
        flush();
        pushLink(out, safeHref(m[0]), [{ type: 'text', value: m[0] }]);
        i += m[0].length;
        continue;
      }
    }

    if (c === '~' && src[i + 1] === '~') {
      const close = findDelim(src, i + 2, '~~');
      if (close > i + 2) {
        flush();
        out.push({ type: 'del', children: parseInline(src.slice(i + 2, close)) });
        i = close + 2;
        continue;
      }
    }

    if ((c === '*' || c === '_') && src[i + 1] === c && leftOk(src, i, c)) {
      const close = findDelim(src, i + 2, c + c);
      if (close > i + 2 && !/\s/.test(src[i + 2])) {
        flush();
        out.push({ type: 'strong', children: parseInline(src.slice(i + 2, close)) });
        i = close + 2;
        continue;
      }
    }

    if ((c === '*' || c === '_') && leftOk(src, i, c)) {
      const close = findSingle(src, i + 1, c);
      if (close > i + 1 && !/\s/.test(src[i + 1])) {
        flush();
        out.push({ type: 'em', children: parseInline(src.slice(i + 1, close)) });
        i = close + 1;
        continue;
      }
    }

    buf += c;
    i++;
  }
  flush();
  return out;
}
