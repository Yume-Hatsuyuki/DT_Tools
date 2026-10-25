import { avatarSrc, personalColor } from './tabletAvatars.js';
import { chatBody, groupByRound, orderClues, singleLine } from './tabletFormat.js';
import { API } from '../../api.js';

const W = 760;
const PAD = 20;
const DPR = 2;
const MAX_PART_H = 7000;
const BODY_TOP = 10;

const FONT_UI = '"Inter", "PingFang SC", "Microsoft YaHei", system-ui, -apple-system, sans-serif';
const FONT_MONO = '"Fira Code", "JetBrains Mono", "Cascadia Code", "Consolas", monospace';

const C = {
  pane: '#171e30',
  line: 'rgba(140, 180, 255, 0.09)',
  lineStrong: 'rgba(140, 180, 255, 0.18)',
  text0: '#f2f4f8',
  text1: '#b6bfce',
  text2: '#7b869b',
  amber: '#f5c542',
  red: '#ff5252',
  pink: '#ff97ab',
  pinkLine: 'rgba(255, 151, 171, 0.35)',
  pinkTag: 'rgba(255, 151, 171, 0.5)',
  bubbleText: '#1d1d1d',
  deadBg: '#4b4f5c',
  deadText: '#d0d0d0',
  propBg: '#3a2238',
  propText: '#ffe6ee',
  photoBg: 'rgba(255, 255, 255, 0.06)',
  avatarBg: 'rgba(255, 255, 255, 0.05)',
};

const CHAT = {
  inset: 12,
  vgap: 12,
  avatar: 40,
  avatarGap: 8,
  colGap: 3,
  metaGap: 6,
  metaH: 15,
  colMaxRatio: 0.72,
  bubblePadX: 11,
  bubblePadY: 7,
  bubbleFont: 13.5,
  bubbleLH: 20.25,
  bubbleRadius: 12,
  photoPad: 4,
  photoMax: 260,
  photoRadius: 8,
  stateH: 22,
};

const CLUE = {
  inset: 12,
  vgap: 8,
  padT: 9,
  padR: 11,
  padB: 9,
  padL: 13,
  gap: 5,
  headH: 22,
  tagH: 16,
  sentenceFont: 13.5,
  sentenceLH: 20.25,
  fieldsFont: 12,
  fieldRowH: 18,
  fieldGapX: 12,
  fieldGapY: 4,
  fieldAvatar: 18,
  destroyedH: 14,
};

function loadImage(src) {
  return new Promise((resolve) => {
    const img = new Image();
    img.onload = () => resolve(img);
    img.onerror = () => resolve(null);
    img.src = src;
  });
}

function wrapText(ctx, text, maxWidth) {
  const rows = [];
  for (const para of String(text ?? '').split('\n')) {
    let line = '';
    for (const ch of para) {
      const next = line + ch;
      if (line && ctx.measureText(next).width > maxWidth) {
        rows.push(line);
        line = ch;
      } else {
        line = next;
      }
    }
    rows.push(line);
  }
  return rows.length ? rows : [''];
}

function roundRect(ctx, x, y, w, h, r) {
  ctx.beginPath();
  ctx.moveTo(x + r, y);
  ctx.arcTo(x + w, y, x + w, y + h, r);
  ctx.arcTo(x + w, y + h, x, y + h, r);
  ctx.arcTo(x, y + h, x, y, r);
  ctx.arcTo(x, y, x + w, y, r);
  ctx.closePath();
}

function setFont(ctx, px, weight = 400, mono = false) {
  ctx.font = `${weight} ${px}px ${mono ? FONT_MONO : FONT_UI}`;
}

function drawAvatar(ctx, img, x, y, size, dead) {
  ctx.save();
  if (dead) {
    ctx.globalAlpha = 0.45;
    ctx.filter = 'grayscale(1)';
  }
  roundRect(ctx, x, y, size, size, 10);
  ctx.fillStyle = C.avatarBg;
  ctx.fill();
  ctx.clip();
  if (img) {
    const k = Math.min(size / img.naturalWidth, size / img.naturalHeight);
    const w = img.naturalWidth * k;
    const h = img.naturalHeight * k;
    ctx.drawImage(img, x + (size - w) / 2, y + (size - h) / 2, w, h);
  }
  ctx.restore();
}

function drawSmallAvatar(ctx, img, x, y, size, radius) {
  ctx.save();
  roundRect(ctx, x, y, size, size, radius);
  ctx.fillStyle = C.avatarBg;
  ctx.fill();
  ctx.clip();
  if (img) {
    const k = Math.min(size / img.naturalWidth, size / img.naturalHeight);
    const w = img.naturalWidth * k;
    const h = img.naturalHeight * k;
    ctx.drawImage(img, x + (size - w) / 2, y + (size - h) / 2, w, h);
  }
  ctx.restore();
}

function blank(h) {
  return { h, draw() {} };
}

function paginate(blocks) {
  const parts = [];
  let cur = [];
  let curH = 0;
  for (const b of blocks) {
    if (cur.length && curH + b.h > MAX_PART_H) {
      parts.push({ blocks: cur, h: curH });
      cur = [];
      curH = 0;
    }
    cur.push(b);
    curH += b.h;
  }
  if (cur.length) parts.push({ blocks: cur, h: curH });
  return parts;
}

function paintPart(part, title, partIndex, partCount) {
  const canvas = document.createElement('canvas');
  const headH = partIndex === 0 ? 64 : 36;
  const bodyH = BODY_TOP + part.h;
  const H = headH + bodyH + PAD;
  canvas.width = W * DPR;
  canvas.height = H * DPR;
  const ctx = canvas.getContext('2d');
  ctx.scale(DPR, DPR);
  ctx.fillStyle = C.pane;
  ctx.fillRect(0, 0, W, H);

  setFont(ctx, 18, 600);
  ctx.fillStyle = C.text0;
  ctx.textBaseline = 'top';
  const suffix = partCount > 1 ? `（${partIndex + 1}/${partCount}）` : '';
  ctx.fillText(`${title}${suffix}`, PAD, partIndex === 0 ? 22 : 10);

  let y = headH + BODY_TOP;
  for (const b of part.blocks) {
    b.draw(ctx, y);
    y += b.h;
  }
  return new Promise((resolve) => canvas.toBlob(resolve, 'image/png'));
}

function chatMessageBlock(l, avatarMap, photoMap, measure) {
  const mine = !!l.isMine;
  const dead = !!l.isDead;
  const proposition = l.kind === 'proposition';
  const photo = l.kind === 'photo' ? photoMap.get(l.photoKey) : null;
  const contentW = W - PAD * 2 - CHAT.inset * 2;
  const colMax = contentW * CHAT.colMaxRatio;
  const border = proposition ? 1 : 0;

  let bubbleW;
  let bubbleH;
  let rows = [];
  let photoW = 0;
  let photoH = 0;
  if (photo) {
    const k = Math.min(1, CHAT.photoMax / photo.naturalWidth, CHAT.photoMax / photo.naturalHeight);
    photoW = Math.round(photo.naturalWidth * k);
    photoH = Math.round(photo.naturalHeight * k);
    bubbleW = photoW + CHAT.photoPad * 2;
    bubbleH = photoH + CHAT.photoPad * 2;
  } else {
    const label = l.kind === 'photo' ? '[图片不可用]' : proposition ? singleLine(l.text) : chatBody(l);
    setFont(measure, CHAT.bubbleFont, proposition ? 500 : 400);
    rows = wrapText(measure, label, colMax - CHAT.bubblePadX * 2 - border * 2);
    const widest = Math.max(...rows.map((r) => measure.measureText(r).width));
    bubbleW = Math.min(colMax, Math.ceil(widest) + CHAT.bubblePadX * 2 + border * 2);
    bubbleH = rows.length * CHAT.bubbleLH + CHAT.bubblePadY * 2 + border * 2;
  }

  const nameText = l.name || '';
  setFont(measure, 11, 500);
  const nameW = measure.measureText(nameText).width;
  setFont(measure, 11);
  const deadText = dead ? '已死亡' : '';
  const deadW = dead ? measure.measureText(deadText).width : 0;
  setFont(measure, 10, 400, true);
  const timeText = l.time || '';
  const timeW = measure.measureText(timeText).width;
  const parts = [
    { key: 'name', text: nameText, w: nameW },
    ...(dead ? [{ key: 'dead', text: deadText, w: deadW }] : []),
    { key: 'time', text: timeText, w: timeW },
  ];
  const ordered = mine ? [...parts].reverse() : parts;
  const metaW = parts.reduce((s, p) => s + p.w, 0) + CHAT.metaGap * (parts.length - 1);
  const colW = Math.min(colMax, Math.max(bubbleW, metaW));
  const rowH = Math.max(CHAT.avatar, CHAT.metaH + CHAT.colGap + bubbleH);

  return {
    h: rowH + CHAT.vgap,
    draw(ctx, y) {
      const contentL = PAD + CHAT.inset;
      const contentR = W - PAD - CHAT.inset;
      const avatarX = mine ? contentR - CHAT.avatar : contentL;
      const colR = mine ? avatarX - CHAT.avatarGap : avatarX + CHAT.avatar + CHAT.avatarGap;
      const colL = mine ? colR - colW : colR;

      drawAvatar(ctx, avatarMap.get(l.avatar), avatarX, y, CHAT.avatar, dead);

      let mx = mine ? colR - metaW : colL;
      for (const p of ordered) {
        if (p.key === 'name') {
          setFont(ctx, 11, 500);
          ctx.fillStyle = C.text1;
        } else if (p.key === 'dead') {
          setFont(ctx, 11);
          ctx.fillStyle = C.red;
        } else {
          setFont(ctx, 10, 400, true);
          ctx.fillStyle = C.text2;
        }
        ctx.textBaseline = 'top';
        ctx.textAlign = 'left';
        ctx.fillText(p.text, mx, y);
        mx += p.w + CHAT.metaGap;
      }

      const by = y + CHAT.metaH + CHAT.colGap;
      const bx = mine ? colR - bubbleW : colL;
      if (photo) {
        roundRect(ctx, bx, by, bubbleW, bubbleH, CHAT.bubbleRadius);
        ctx.fillStyle = C.photoBg;
        ctx.fill();
        ctx.save();
        roundRect(ctx, bx + CHAT.photoPad, by + CHAT.photoPad, photoW, photoH, CHAT.photoRadius);
        ctx.clip();
        ctx.drawImage(photo, bx + CHAT.photoPad, by + CHAT.photoPad, photoW, photoH);
        ctx.restore();
        return;
      }

      roundRect(ctx, bx, by, bubbleW, bubbleH, CHAT.bubbleRadius);
      if (proposition) {
        ctx.fillStyle = C.propBg;
        ctx.fill();
        ctx.strokeStyle = C.pink;
        ctx.lineWidth = 1;
        roundRect(ctx, bx + 0.5, by + 0.5, bubbleW - 1, bubbleH - 1, CHAT.bubbleRadius - 0.5);
        ctx.stroke();
      } else {
        ctx.fillStyle = dead ? C.deadBg : personalColor(l.avatar);
        ctx.fill();
      }

      setFont(ctx, CHAT.bubbleFont, proposition ? 500 : 400);
      ctx.fillStyle = proposition ? C.propText : dead ? C.deadText : C.bubbleText;
      ctx.textBaseline = 'top';
      const tx = bx + CHAT.bubblePadX + border;
      const ty = by + CHAT.bubblePadY + border;
      rows.forEach((row, i) => ctx.fillText(row, tx, ty + i * CHAT.bubbleLH));
    },
  };
}

function chatStateBlock(l) {
  return {
    h: CHAT.stateH + CHAT.vgap,
    draw(ctx, y) {
      setFont(ctx, 12, 600);
      ctx.fillStyle = C.amber;
      ctx.textBaseline = 'top';
      ctx.textAlign = 'center';
      ctx.fillText(`—— ${l.text} ——`, W / 2, y + 4);
      ctx.textAlign = 'left';
    },
  };
}

export async function renderChatParts(lines, title, numbering = new Map()) {
  const photoKeys = lines.filter((l) => l.kind === 'photo').map((l) => l.photoKey);
  const avatarKeys = [...new Set(lines.map((l) => l.avatar).filter(Boolean))];
  const [avatarImgs, photoImgs] = await Promise.all([
    Promise.all(avatarKeys.map((k) => loadImage(avatarSrc(k)))),
    Promise.all(photoKeys.map((k) => loadImage(API.tabletPhotoUrl(k)))),
  ]);
  const avatarMap = new Map(avatarKeys.map((k, i) => [k, avatarImgs[i]]));
  const photoMap = new Map(photoKeys.map((k, i) => [k, photoImgs[i]]));
  const measure = document.createElement('canvas').getContext('2d');

  const blocks = [];
  for (const g of groupByRound(lines, numbering)) {
    blocks.push(roundHeaderBlock(g.no));
    for (const l of g.items) {
      blocks.push(l.kind === 'state'
        ? chatStateBlock(l)
        : chatMessageBlock(l, avatarMap, photoMap, measure));
    }
  }
  if (!blocks.length) blocks.push(blank(40));

  const parts = paginate(blocks);
  const out = [];
  for (let i = 0; i < parts.length; i++) {
    out.push(await paintPart(parts[i], title, i, parts.length));
  }
  return out;
}

function drawPill(ctx, x, y, text, color, border) {
  setFont(ctx, 10.5);
  const w = ctx.measureText(text).width + 14;
  roundRect(ctx, x, y, w, CLUE.tagH, CLUE.tagH / 2);
  ctx.strokeStyle = border;
  ctx.lineWidth = 1;
  ctx.stroke();
  ctx.fillStyle = color;
  ctx.textBaseline = 'middle';
  ctx.fillText(text, x + 7, y + CLUE.tagH / 2);
  ctx.textBaseline = 'top';
  return w;
}

function layoutFields(measure, c, maxW) {
  setFont(measure, CLUE.fieldsFont);
  const items = [
    { kind: 'who', text: c.who || '', avatar: c.whoAvatar, w: CLUE.fieldAvatar + 5 + measure.measureText(c.who || '').width },
    { kind: 'text', text: c.where || '', w: measure.measureText(c.where || '').width },
    { kind: 'text', text: c.when || '', w: measure.measureText(c.when || '').width },
  ];
  const rows = [];
  let row = [];
  let rowW = 0;
  for (const it of items) {
    const need = row.length ? it.w + CLUE.fieldGapX : it.w;
    if (row.length && rowW + need > maxW) {
      rows.push(row);
      row = [];
      rowW = 0;
    }
    rowW += row.length ? it.w + CLUE.fieldGapX : it.w;
    row.push(it);
  }
  if (row.length) rows.push(row);
  return rows;
}

function clueCardBlock(c, avatarMap, measure) {
  const cardW = W - PAD * 2 - CLUE.inset * 2;
  const innerW = cardW - CLUE.padL - CLUE.padR - 2;
  const tail = c.destroyed ? '已销毁' : '';
  let sentenceRows = [];
  if (c.isProposition) {
    setFont(measure, CLUE.sentenceFont, 400);
    sentenceRows = wrapText(measure, singleLine(c.sentence || ''), innerW);
  }
  const fieldRows = layoutFields(measure, c, innerW);
  const fieldsH = fieldRows.length * CLUE.fieldRowH + Math.max(0, fieldRows.length - 1) * CLUE.fieldGapY;

  const stack = [CLUE.headH];
  if (sentenceRows.length) stack.push(sentenceRows.length * CLUE.sentenceLH);
  stack.push(fieldsH);
  if (tail) stack.push(CLUE.destroyedH);
  const contentH = stack.reduce((s, v) => s + v, 0) + CLUE.gap * (stack.length - 1);
  const h = CLUE.padT + contentH + CLUE.padB + 2;

  return {
    h: h + CLUE.vgap,
    draw(ctx, y) {
      const x = PAD + CLUE.inset;
      ctx.save();
      if (c.destroyed) ctx.globalAlpha = 0.6;
      roundRect(ctx, x, y, cardW, h, 6);
      ctx.strokeStyle = c.isProposition ? C.pinkLine : C.line;
      ctx.lineWidth = 1;
      ctx.stroke();
      if (c.isProposition) {
        roundRect(ctx, x, y + 8, 3, h - 16, 1.5);
        ctx.fillStyle = C.pink;
        ctx.fill();
      }

      let cy = y + CLUE.padT;
      const cx = x + CLUE.padL;
      if (c.isProposition) {
        drawSmallAvatar(ctx, avatarMap.get(c.ownerAvatar), cx, cy + (CLUE.headH - 22) / 2, 22, 6);
        setFont(ctx, 11.5, 500);
        ctx.fillStyle = C.text0;
        ctx.textBaseline = 'middle';
        const ownerX = cx + 22 + 7;
        ctx.fillText(c.ownerName || '', ownerX, cy + CLUE.headH / 2);
        const tagX = ownerX + ctx.measureText(c.ownerName || '').width + 7;
        drawPill(ctx, tagX, cy + (CLUE.headH - CLUE.tagH) / 2, '命题', C.pink, C.pinkTag);
        ctx.textBaseline = 'top';
      } else {
        drawPill(ctx, cx, cy + (CLUE.headH - CLUE.tagH) / 2, '线索', C.text2, C.lineStrong);
      }
      cy += CLUE.headH + CLUE.gap;

      if (sentenceRows.length) {
        setFont(ctx, CLUE.sentenceFont, 400);
        ctx.fillStyle = C.text0;
        ctx.textBaseline = 'top';
        sentenceRows.forEach((row, i) => ctx.fillText(row, cx, cy + i * CLUE.sentenceLH));
        cy += sentenceRows.length * CLUE.sentenceLH + CLUE.gap;
      }

      fieldRows.forEach((row, ri) => {
        let fx = cx;
        const ry = cy + ri * (CLUE.fieldRowH + CLUE.fieldGapY);
        for (const it of row) {
          if (it.kind === 'who') {
            drawSmallAvatar(ctx, avatarMap.get(it.avatar), fx, ry, CLUE.fieldAvatar, 5);
            setFont(ctx, CLUE.fieldsFont);
            ctx.fillStyle = C.text0;
            ctx.textBaseline = 'middle';
            ctx.fillText(it.text, fx + CLUE.fieldAvatar + 5, ry + CLUE.fieldRowH / 2);
          } else {
            setFont(ctx, CLUE.fieldsFont);
            ctx.fillStyle = C.text1;
            ctx.textBaseline = 'middle';
            ctx.fillText(it.text, fx, ry + CLUE.fieldRowH / 2);
          }
          ctx.textBaseline = 'top';
          fx += it.w + CLUE.fieldGapX;
        }
      });
      cy += fieldsH + CLUE.gap;

      if (tail) {
        setFont(ctx, 10.5);
        ctx.fillStyle = C.red;
        ctx.textBaseline = 'top';
        ctx.fillText(tail, cx, cy);
      }
      ctx.restore();
    },
  };
}

function roundHeaderBlock(no) {
  return {
    h: 34,
    draw(ctx, y) {
      setFont(ctx, 12, 600);
      ctx.fillStyle = C.amber;
      ctx.textBaseline = 'top';
      ctx.textAlign = 'center';
      ctx.fillText(`—— 第 ${no} 局 ——`, W / 2, y + 6);
      ctx.textAlign = 'left';
    },
  };
}

export async function renderClueParts(clues, title, numbering = new Map()) {
  const ordered = orderClues(clues);
  const avatarKeys = [...new Set(ordered.flatMap((c) => [c.ownerAvatar, c.whoAvatar]).filter(Boolean))];
  const avatarImgs = await Promise.all(avatarKeys.map((k) => loadImage(avatarSrc(k))));
  const avatarMap = new Map(avatarKeys.map((k, i) => [k, avatarImgs[i]]));
  const measure = document.createElement('canvas').getContext('2d');

  const blocks = [];
  for (const g of groupByRound(ordered, numbering)) {
    blocks.push(roundHeaderBlock(g.no));
    for (const c of g.items) blocks.push(clueCardBlock(c, avatarMap, measure));
  }
  if (!blocks.length) {
    blocks.push({
      h: 60,
      draw(ctx, y) {
        setFont(ctx, 14);
        ctx.fillStyle = C.text2;
        ctx.textBaseline = 'top';
        ctx.fillText('（无内容）', PAD, y + 20);
      },
    });
  }

  const parts = paginate(blocks);
  const out = [];
  for (let i = 0; i < parts.length; i++) {
    out.push(await paintPart(parts[i], title, i, parts.length));
  }
  return out;
}

export function downloadBlob(blob, filename) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 4000);
}

export function downloadText(text, filename) {
  downloadBlob(new Blob([`\ufeff${text}`], { type: 'text/plain;charset=utf-8' }), filename);
}

export async function imageBlobToPng(blob) {
  const url = URL.createObjectURL(blob);
  try {
    const img = await loadImage(url);
    if (!img) throw new Error('无法读取图片');
    const canvas = document.createElement('canvas');
    canvas.width = img.naturalWidth;
    canvas.height = img.naturalHeight;
    canvas.getContext('2d').drawImage(img, 0, 0);
    return await new Promise((resolve) => canvas.toBlob(resolve, 'image/png'));
  } finally {
    URL.revokeObjectURL(url);
  }
}

function blobToBase64(blob) {
  return blob.arrayBuffer().then((buf) => {
    const bytes = new Uint8Array(buf);
    let bin = '';
    const CHUNK = 0x8000;
    for (let i = 0; i < bytes.length; i += CHUNK) {
      bin += String.fromCharCode.apply(null, bytes.subarray(i, i + CHUNK));
    }
    return btoa(bin);
  });
}

export async function fileToJpegBase64(file, limit) {
  const url = URL.createObjectURL(file);
  try {
    const img = await loadImage(url);
    if (!img) throw new Error('无法读取该图片');
    let max = 1280;
    for (let attempt = 0; attempt < 8; attempt++) {
      const scale = Math.min(1, max / Math.max(img.naturalWidth, img.naturalHeight));
      const w = Math.max(1, Math.round(img.naturalWidth * scale));
      const h = Math.max(1, Math.round(img.naturalHeight * scale));
      const canvas = document.createElement('canvas');
      canvas.width = w;
      canvas.height = h;
      const ctx = canvas.getContext('2d');
      ctx.fillStyle = '#ffffff';
      ctx.fillRect(0, 0, w, h);
      ctx.drawImage(img, 0, 0, w, h);
      for (const q of [0.9, 0.8, 0.7, 0.6, 0.5]) {
        const blob = await new Promise((resolve) => canvas.toBlob(resolve, 'image/jpeg', q));
        if (blob && blob.size <= limit) return blobToBase64(blob);
      }
      max = Math.round(max * 0.75);
    }
    throw new Error('图片过大，无法压缩到上限以内');
  } finally {
    URL.revokeObjectURL(url);
  }
}
