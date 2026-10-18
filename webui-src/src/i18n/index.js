import { computed, ref } from 'vue';
import { buildMessages } from './messages.js';

/**
 * 界面语言（WebUI 本地状态，存 localStorage，不进后端配置）。
 *
 * - 偏好：'auto'（跟随浏览器，默认）或某个语言代码。
 * - 语言集合与游戏的 9 种语言一致（JsonData 的 CHS…RUS）。
 * - steam：新闻 RSS 的 l= 参数，须与后端 SteamNewsApi.Languages 保持一致。
 * - t(key, params) 是响应式的：在模板或计算属性里调用，切换语言后会自动刷新。
 */
export const LOCALES = [
  { code: 'zh-CN', name: '简体中文', game: 'CHS', steam: 'schinese', intl: 'zh-CN' },
  { code: 'zh-TW', name: '繁體中文', game: 'CHT', steam: 'tchinese', intl: 'zh-TW' },
  { code: 'en', name: 'English', game: 'ENG', steam: 'english', intl: 'en-US' },
  { code: 'es', name: 'Español', game: 'ESL', steam: 'spanish', intl: 'es-ES' },
  { code: 'fr', name: 'Français', game: 'FRA', steam: 'french', intl: 'fr-FR' },
  { code: 'ja', name: '日本語', game: 'JPN', steam: 'japanese', intl: 'ja-JP' },
  { code: 'ko', name: '한국어', game: 'KOR', steam: 'koreana', intl: 'ko-KR' },
  { code: 'pt-BR', name: 'Português (Brasil)', game: 'PTB', steam: 'brazilian', intl: 'pt-BR' },
  { code: 'ru', name: 'Русский', game: 'RUS', steam: 'russian', intl: 'ru-RU' },
];

const STORE_KEY = 'dt_locale_v1';
const FALLBACK = 'en';
const SOURCE = 'zh-CN';
const MESSAGES = buildMessages();

/** 把浏览器语言标签映射到本地支持的语言；无法匹配时返回 null。 */
export function matchLocale(tag) {
  const n = String(tag || '').toLowerCase();
  if (!n) return null;
  if (n.startsWith('zh')) return /-(tw|hk|mo)|hant/.test(n) ? 'zh-TW' : 'zh-CN';
  if (n.startsWith('en')) return 'en';
  if (n.startsWith('es')) return 'es';
  if (n.startsWith('fr')) return 'fr';
  if (n.startsWith('ja')) return 'ja';
  if (n.startsWith('ko')) return 'ko';
  if (n.startsWith('pt')) return 'pt-BR';
  if (n.startsWith('ru')) return 'ru';
  return null;
}

/** 依次尝试 navigator.languages，取第一个能匹配的语言；都不行则英文。 */
export function detectBrowserLocale(list = (typeof navigator !== 'undefined' ? navigator.languages || [navigator.language] : [])) {
  for (const tag of list) {
    const hit = matchLocale(tag);
    if (hit) return hit;
  }
  return FALLBACK;
}

function readPref() {
  try {
    const v = localStorage.getItem(STORE_KEY);
    if (v === 'auto' || LOCALES.some((l) => l.code === v)) return v;
  } catch { /* 隐私模式忽略 */ }
  return 'auto';
}

const pref = ref(readPref());

/** 实际生效的语言代码（偏好为 auto 时取浏览器语言）。 */
export const locale = computed(() => (pref.value === 'auto' ? detectBrowserLocale() : pref.value));

/** 当前语言的记录（名称、游戏代码、Steam 代码、Intl 标签）。 */
export const localeInfo = computed(() => LOCALES.find((l) => l.code === locale.value) || LOCALES[2]);

/** 设置语言偏好：'auto' 或语言代码。 */
export function setLocalePref(value) {
  if (value !== 'auto' && !LOCALES.some((l) => l.code === value)) return;
  pref.value = value;
  try { localStorage.setItem(STORE_KEY, value); } catch { /* 隐私模式忽略 */ }
  if (typeof document !== 'undefined') document.documentElement.lang = locale.value;
}

export function localePref() {
  return pref.value;
}

/** 翻译。找不到时依次回退：当前语言 → 英文 → 中文源文 → 键名本身。 */
export function t(key, params) {
  const L = locale.value;
  const raw = MESSAGES[L]?.[key] ?? MESSAGES[FALLBACK]?.[key] ?? MESSAGES[SOURCE]?.[key] ?? key;
  if (!params) return raw;
  return raw.replace(/\{(\w+)\}/g, (_, name) => (name in params ? String(params[name]) : `{${name}}`));
}

/** 当前语言的 Intl 标签（日期、数字格式）。 */
export function intlLocale() {
  return localeInfo.value.intl;
}

if (typeof document !== 'undefined') document.documentElement.lang = locale.value;
