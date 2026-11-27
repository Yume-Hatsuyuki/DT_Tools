import { computed, ref } from 'vue';
import { buildMessages } from './messages.js';

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
  } catch { }
  return 'auto';
}

const pref = ref(readPref());

export const locale = computed(() => (pref.value === 'auto' ? detectBrowserLocale() : pref.value));

export const localeInfo = computed(() => LOCALES.find((l) => l.code === locale.value) || LOCALES[2]);

export function setLocalePref(value) {
  if (value !== 'auto' && !LOCALES.some((l) => l.code === value)) return;
  pref.value = value;
  try { localStorage.setItem(STORE_KEY, value); } catch { }
  if (typeof document !== 'undefined') document.documentElement.lang = locale.value;
}

export function localePref() {
  return pref.value;
}

export function t(key, params) {
  const L = locale.value;
  const raw = MESSAGES[L]?.[key] ?? MESSAGES[FALLBACK]?.[key] ?? MESSAGES[SOURCE]?.[key] ?? key;
  if (!params) return raw;
  return raw.replace(/\{(\w+)\}/g, (_, name) => (name in params ? String(params[name]) : `{${name}}`));
}

export function intlLocale() {
  return localeInfo.value.intl;
}

if (typeof document !== 'undefined') document.documentElement.lang = locale.value;
