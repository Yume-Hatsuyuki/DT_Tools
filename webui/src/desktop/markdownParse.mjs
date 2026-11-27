
export const MAX_SOURCE_LENGTH = 200000;

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
  if (marker[0] === '`' && info.includes('`')) return null;
  return { char: marker[0], len: marker.length, indent: m[1].length, lang: info.split(/\s+/)[0] || '' };
}

function isFenceClose(line, open) {
  const m = line.match(/^ {0,3}(`{3,}|~{3,})[ \t]*$/);
  return !!m && m[1][0] === open.char && m[1].length >= open.len;
}

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

    const fence = fenceOpen(line);
    if (fence) {
      const body = [];
      const strip = new RegExp('^ {0,' + fence.indent + '}');
      i++;
      while (i < n && !isFenceClose(lines[i], fence)) {
        body.push(lines[i].replace(strip, ''));
        i++;
      }
      i++;
      out.push({ type: 'code', lang: fence.lang, text: body.join('\n') });
      continue;
    }

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

    const atx = line.match(ATX_RE);
    if (atx) {
      const text = (atx[2] || '').replace(/[ \t]+#+[ \t]*$/, '').replace(/^#+[ \t]*$/, '');
      out.push({ type: 'heading', level: atx[1].length, children: parseInline(text.trim()) });
      i++;
      continue;
    }

    if (HR_RE.test(line)) {
      out.push({ type: 'hr' });
      i++;
      continue;
    }

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

    const bm = line.match(BULLET_RE);
    const om = bm ? null : line.match(ORDERED_RE);
    if (bm || om) {
      const ordered = !!om;
      const start = ordered ? parseInt(om[2], 10) : 1;
      const delim = ordered ? om[3] : bm[2];
      const items = [];
      while (i < n) {
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

function nestOrFlatten(lines, depth) {
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

function safeHref(u) {
  const s = (u || '').trim();
  return SAFE_HREF_RE.test(s) ? s : null;
}

function pushLink(out, href, children) {
  if (href) out.push({ type: 'link', href, children });
  else out.push(...children);
}

function leftOk(src, i, c) {
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

function trimCodeSpan(s) {
  if (s.length >= 2 && s[0] === ' ' && s[s.length - 1] === ' ' && /[^ ]/.test(s)) return s.slice(1, -1);
  return s;
}

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
