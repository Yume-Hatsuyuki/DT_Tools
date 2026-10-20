#!/usr/bin/env node
// nextmusic-dl.mjs
// 用法: node nextmusic-dl.mjs <songId> [ip] [音质]

import { mkdir, writeFile } from 'node:fs/promises';
import { extname } from 'node:path';

const BASE = 'https://nextmusic.toubiec.cn/api';
const UA = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/130 Safari/537.36';
const DEFAULT_IP = '1.1.1.1'; // 最终兜底

// 音质等级枚举（值均通过接口实测验证）
const QualityLevel = Object.freeze({
  STANDARD: 'standard',
  HIGHER:   'higher',
  EXHIGH:   'exhigh',
  LOSSLESS: 'lossless',
  HIRES:    'hires',
  SKY:      'sky',
  JYEFFECT: 'jyeffect',
  JYMASTER: 'jymaster',
  DOLBY:    'dolby',
});

const LABEL_TO_LEVEL = {
  '标准音质':   QualityLevel.STANDARD,
  '较高音质':   QualityLevel.HIGHER,
  '极高音质':   QualityLevel.EXHIGH,
  '无损音质':   QualityLevel.LOSSLESS,
  'Hi-Res音质': QualityLevel.HIRES,
  '沉浸环绕声': QualityLevel.SKY,
  '高清环绕声': QualityLevel.JYEFFECT,
  '超清母带':   QualityLevel.JYMASTER,
  '杜比全景声': QualityLevel.DOLBY,
};

const ALL_LEVELS = Object.values(QualityLevel);
const toLevel = s =>
  LABEL_TO_LEVEL[s] ||
  (ALL_LEVELS.includes(s) ? s : QualityLevel.STANDARD);

// --- 新增：通过 ipinfo.io 获取真实公网 IP，失败时回落到默认 IP ---
async function getPublicIP() {
  try {
    const res = await fetch('https://ipinfo.io/json', {
      headers: { 'user-agent': UA, accept: 'application/json' },
    });
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    const data = await res.json();
    const ip = data?.ip?.trim();
    if (ip) {
      console.log(`🌐 从 ipinfo.io 获取到公网 IP: ${ip}`);
      return ip;
    }
    throw new Error('响应中未找到 ip 字段');
  } catch (err) {
    console.warn(`⚠️ 获取公网 IP 失败 (${err.message})，将使用默认 IP: ${DEFAULT_IP}`);
    return DEFAULT_IP;
  }
}

async function postJson(path, payload) {
  const res = await fetch(BASE + path, {
    method: 'POST',
    headers: {
      accept: '*/*',
      'content-type': 'application/json',
      'user-agent': UA,
    },
    body: JSON.stringify(payload),
  });
  const text = await res.text();
  let json;
  try {
    json = JSON.parse(text);
  } catch {
    throw new Error(`${path} 非 JSON: ${text.slice(0, 200)}`);
  }
  if (json.code !== 200) {
    throw new Error(`${path} code=${json.code}: ${json.message || text.slice(0, 200)}`);
  }
  return json.data;
}

const safe = s => (s || '').replace(/[\\/:*?"<>|\r\n\t]/g, '_').trim().slice(0, 100);

async function main() {
  const args = process.argv.slice(2);
  const id = args[0];
  if (!id) throw new Error('用法: node nextmusic-dl.mjs <songId> [ip] [音质]');

  // 优先级：命令行参数 > ipinfo.io 动态获取 > 默认兜底
  const ip = args[1] || await getPublicIP();
  const level = toLevel(args[2]);

  await mkdir('downloads', { recursive: true });

  // 1) 元信息
  const info = await postJson('/getSongInfo', { id, ip });
  console.log(`🎵 ${info.name} - ${info.singer} [${info.duration}]`);

  // 2) 取直链（拿到后立刻下，约 25 分钟内有效）
  const song = await postJson('/getSongUrl', { id, ip, level, timestamp: Date.now() });
  if (!song.url) throw new Error('未返回播放地址: ' + JSON.stringify(song));
  const note = song.level === level ? '' : `（请求 ${level} 被降级）`;
  console.log(`🔗 实际=${song.level} ${song.br}bps ${song.size}B ${note}`);

  // 3) 下载
  const res = await fetch(song.url, { headers: { 'user-agent': UA }, redirect: 'follow' });
  if (!res.ok) {
    const b = await res.text().catch(() => '');
    throw new Error(`下载失败 HTTP ${res.status}: ${b.slice(0, 300)}`);
  }
  const buf = Buffer.from(await res.arrayBuffer());
  const ext = extname(new URL(song.url).pathname) || '.mp3';
  const file = `downloads/${safe(info.name)} - ${safe(info.singer)}.${song.level}${ext}`;
  await writeFile(file, buf);
  console.log(`✅ ${file} (${buf.length}B)`);
}

main().catch(e => {
  console.error('❌', e.message);
  process.exitCode = 1;
});