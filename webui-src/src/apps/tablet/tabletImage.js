/**
 * 平板电脑的图片导出（Canvas 绘制）与照片压缩。依赖 DOM，只在浏览器中使用。
 */
import { avatarSrc, personalColor } from './tabletAvatars.js';
import { chatBody, orderClues } from './tabletFormat.js';
import { API } from '../../api.js';

const W = 760;
const PAD = 20;
const DPR = 2;
/** 单张导出图的高度上限（CSS 像素）。超过则分页导出，避免超出浏览器画布上限。 */
const MAX_PART_H = 7000;
const FONT = '"PingFang SC","Microsoft YaHei","Noto Sans CJK SC",system-ui,sans-serif';
const C = {
  bg: '#121724',
  title: '#f2f4f8',
  sub: '#7b869b',
  name: '#b6bfce',
  text: '#1d1d1d',
  deadText: '#d0d0d0',
  deadBg: '#4b4f5c',
  state: '#f5c542',
  propBg: '#3a2238',
  propBorder: '#ff97ab',
  propText: '#ffe6ee',
  card: '#1e2840',
  cardLine: '#2b3a5a',
  body: '#dfe5f0',
  destroyed: '#ff8a8a',
};

/** 预加载图片；失败返回 null（绘制时用占位）。 */
function loadImage(src) {
  return new Promise((resolve) => {
    const img = new Image();
    img.onload = () => resolve(img);
    img.onerror = () => resolve(null);
    img.src = src;
  });
}

/** 按字符宽度换行（中英文通用）。换行符强制分段。 */
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

function drawAvatar(ctx, img, x, y, size, dead) {
  ctx.save();
  ctx.globalAlpha = dead ? 0.45 : 1;
  roundRect(ctx, x, y, size, size, 10);
  ctx.fillStyle = 'rgba(255,255,255,0.06)';
  ctx.fill();
  ctx.clip();
  if (img) ctx.drawImage(img, x, y, size, size);
  ctx.restore();
}

function font(ctx, px, weight = 400) {
  ctx.font = `${weight} ${px}px ${FONT}`;
}

function blank(h) {
  return { h, draw() {} };
}

/** 把若干块按高度切成多段，每段一张图。 */
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
  const H = headH + part.h + PAD;
  canvas.width = W * DPR;
  canvas.height = H * DPR;
  const ctx = canvas.getContext('2d');
  ctx.scale(DPR, DPR);
  ctx.fillStyle = C.bg;
  ctx.fillRect(0, 0, W, H);

  font(ctx, 18, 600);
  ctx.fillStyle = C.title;
  ctx.textBaseline = 'top';
  const suffix = partCount > 1 ? `（${partIndex + 1}/${partCount}）` : '';
  ctx.fillText(`${title}${suffix}`, PAD, partIndex === 0 ? 22 : 10);

  let y = headH;
  for (const b of part.blocks) {
    b.draw(ctx, y);
    y += b.h;
  }
  return new Promise((resolve) => canvas.toBlob(resolve, 'image/png'));
}

/** 聊天记录分段渲染为 PNG Blob 列表。左右气泡与游戏一致：头像与名字在两侧，颜色取人物色。 */
export async function renderChatParts(lines, title) {
  const photoKeys = lines.filter((l) => l.kind === 'photo').map((l) => l.photoKey);
  const avatarKeys = [...new Set(lines.map((l) => l.avatar).filter(Boolean))];
  const [avatarImgs, photoImgs] = await Promise.all([
    Promise.all(avatarKeys.map((k) => loadImage(avatarSrc(k)))),
    Promise.all(photoKeys.map((k) => loadImage(API.tabletPhotoUrl(k)))),
  ]);
  const avatarMap = new Map(avatarKeys.map((k, i) => [k, avatarImgs[i]]));
  const photoMap = new Map(photoKeys.map((k, i) => [k, photoImgs[i]]));

  const measure = document.createElement('canvas').getContext('2d');
  const AV = 46;
  const GAP = 10;
  const BUBBLE_MAX = 500;
  const TEXT_PX = 15;
  const LINE_H = 22;
  const BP = 10;
  const blocks = [];

  for (const l of lines) {
    if (l.kind === 'state') {
      blocks.push({
        h: 40,
        draw(ctx, y) {
          font(ctx, 14, 600);
          ctx.fillStyle = C.state;
          ctx.textBaseline = 'middle';
          ctx.textAlign = 'center';
          ctx.fillText(`—— ${l.text} ——`, W / 2, y + 20);
          ctx.textAlign = 'left';
          ctx.textBaseline = 'top';
        },
      });
      continue;
    }

    const mine = !!l.isMine;
    const dead = !!l.isDead;
    const proposition = l.kind === 'proposition';
    const img = l.kind === 'photo' ? photoMap.get(l.photoKey) : null;

    let bubbleW;
    let bubbleH;
    let rows = [];
    if (img) {
      const maxImgW = 300;
      const scale = Math.min(1, maxImgW / img.naturalWidth);
      bubbleW = Math.max(120, Math.round(img.naturalWidth * scale)) + BP * 2;
      bubbleH = Math.max(60, Math.round(img.naturalHeight * scale)) + BP * 2;
    } else {
      font(measure, TEXT_PX);
      const label = l.kind === 'photo' ? '[图片不可用]' : chatBody(l);
      rows = wrapText(measure, label, BUBBLE_MAX - BP * 2);
      const widest = Math.max(...rows.map((r) => measure.measureText(r).width));
      bubbleW = Math.min(BUBBLE_MAX, Math.ceil(widest) + BP * 2 + 2);
      bubbleH = rows.length * LINE_H + BP * 2;
    }

    const nameH = 20;
    const h = nameH + bubbleH + 16;
    blocks.push({
      h,
      draw(ctx, y) {
        const avatarX = mine ? W - PAD - AV : PAD;
        const bubbleX = mine ? avatarX - GAP - bubbleW : avatarX + AV + GAP;
        const textAlign = mine ? 'right' : 'left';
        const nameX = mine ? W - PAD - AV - GAP : bubbleX;

        font(ctx, 12, 400);
        ctx.fillStyle = C.name;
        ctx.textBaseline = 'top';
        ctx.textAlign = textAlign;
        ctx.fillText(`${l.name}${dead ? '（已死亡）' : ''}  ${l.time}`, nameX, y);
        ctx.textAlign = 'left';

        drawAvatar(ctx, avatarMap.get(l.avatar), avatarX, y + nameH, AV, dead);

        const by = y + nameH;
        roundRect(ctx, bubbleX, by, bubbleW, bubbleH, 12);
        if (proposition) {
          ctx.fillStyle = C.propBg;
          ctx.fill();
          ctx.strokeStyle = C.propBorder;
          ctx.lineWidth = 1.2;
          ctx.stroke();
        } else {
          ctx.fillStyle = dead ? C.deadBg : personalColor(l.avatar);
          ctx.fill();
        }

        if (img) {
          const iw = bubbleW - BP * 2;
          const ih = bubbleH - BP * 2;
          ctx.save();
          roundRect(ctx, bubbleX + BP, by + BP, iw, ih, 8);
          ctx.clip();
          ctx.drawImage(img, bubbleX + BP, by + BP, iw, ih);
          ctx.restore();
          return;
        }

        font(ctx, TEXT_PX, proposition ? 500 : 400);
        ctx.fillStyle = proposition ? C.propText : dead ? C.deadText : C.text;
        ctx.textBaseline = 'top';
        rows.forEach((row, i) => ctx.fillText(row, bubbleX + BP, by + BP + i * LINE_H));
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

/** 线索分段渲染为 PNG Blob 列表。 */
export async function renderClueParts(clues, title) {
  const measure = document.createElement('canvas').getContext('2d');
  const ordered = orderClues(clues);
  const INNER = W - PAD * 2 - 28;
  const blocks = [];

  for (const c of ordered) {
    const headline = c.isProposition ? `命题 · ${c.ownerName}` : '线索';
    font(measure, 15, 500);
    const sentenceRows = c.isProposition ? wrapText(measure, c.sentence || '', INNER) : [];
    const fields = `对象：${c.who}　地点：${c.where}　时间：${c.when}`;
    font(measure, 13);
    const fieldRows = wrapText(measure, fields, INNER);
    const tail = c.destroyed ? '（已销毁）' : '';
    const h = 14 + 22 + sentenceRows.length * 22 + (sentenceRows.length ? 6 : 0)
      + fieldRows.length * 20 + (tail ? 20 : 0) + 14 + 10;

    blocks.push({
      h,
      draw(ctx, y) {
        const x = PAD;
        const w = W - PAD * 2;
        roundRect(ctx, x, y, w, h - 10, 10);
        ctx.fillStyle = C.card;
        ctx.fill();
        if (c.isProposition) {
          ctx.fillStyle = C.propBorder;
          ctx.fillRect(x, y + 10, 3, h - 30);
        }
        let cy = y + 14;
        font(ctx, 13, 600);
        ctx.fillStyle = c.isProposition ? C.propBorder : C.sub;
        ctx.textBaseline = 'top';
        ctx.fillText(headline, x + 16, cy);
        cy += 22;
        if (sentenceRows.length) {
          font(ctx, 15, 500);
          ctx.fillStyle = C.title;
          sentenceRows.forEach((row, i) => ctx.fillText(row, x + 16, cy + i * 22));
          cy += sentenceRows.length * 22 + 6;
        }
        font(ctx, 13);
        ctx.fillStyle = C.body;
        fieldRows.forEach((row, i) => ctx.fillText(row, x + 16, cy + i * 20));
        cy += fieldRows.length * 20;
        if (tail) {
          ctx.fillStyle = C.destroyed;
          ctx.fillText(tail.slice(1, -1), x + 16, cy);
        }
      },
    });
  }

  if (!blocks.length) blocks.push({ h: 60, draw(ctx, y) { font(ctx, 14); ctx.fillStyle = C.sub; ctx.fillText('（无内容）', PAD, y + 20); } });

  const parts = paginate(blocks);
  const out = [];
  for (let i = 0; i < parts.length; i++) {
    out.push(await paintPart(parts[i], title, i, parts.length));
  }
  return out;
}

/** 触发浏览器下载。 */
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

/** 文本下载，带 UTF-8 BOM，避免 Windows 记事本乱码。 */
export function downloadText(text, filename) {
  downloadBlob(new Blob([`\ufeff${text}`], { type: 'text/plain;charset=utf-8' }), filename);
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

/**
 * 把用户选择的图片压缩为不超过 limit 字节的 JPEG，返回 base64。
 * 先降质量，仍超标则逐步缩小尺寸；仍失败则抛错。
 */
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
