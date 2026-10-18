/**
 * 游戏新闻挂件的语言表。
 * code 与 Steam 新闻 RSS 的 l= 参数一致，须与后端 WebConsole/Api/SteamNewsApi.cs 的 Languages 保持同步。
 * locale：日期格式；ui：挂件自身文字所用的语言（当前提供 zh/en/ja/ko/ru，其余回退英文）。
 */
export const NEWS_LANGUAGES = [
  { code: 'schinese', name: '简体中文', locale: 'zh-CN', ui: 'zh' },
  { code: 'tchinese', name: '繁體中文', locale: 'zh-TW', ui: 'zh' },
  { code: 'english', name: 'English', locale: 'en-US', ui: 'en' },
  { code: 'japanese', name: '日本語', locale: 'ja-JP', ui: 'ja' },
  { code: 'koreana', name: '한국어', locale: 'ko-KR', ui: 'ko' },
  { code: 'russian', name: 'Русский', locale: 'ru-RU', ui: 'ru' },
  { code: 'spanish', name: 'Español', locale: 'es-ES', ui: 'en' },
  { code: 'latam', name: 'Español (Latinoamérica)', locale: 'es-419', ui: 'en' },
  { code: 'french', name: 'Français', locale: 'fr-FR', ui: 'en' },
  { code: 'german', name: 'Deutsch', locale: 'de-DE', ui: 'en' },
  { code: 'portuguese', name: 'Português', locale: 'pt-PT', ui: 'en' },
  { code: 'brazilian', name: 'Português (Brasil)', locale: 'pt-BR', ui: 'en' },
  { code: 'italian', name: 'Italiano', locale: 'it-IT', ui: 'en' },
  { code: 'polish', name: 'Polski', locale: 'pl-PL', ui: 'en' },
  { code: 'turkish', name: 'Türkçe', locale: 'tr-TR', ui: 'en' },
  { code: 'thai', name: 'ไทย', locale: 'th-TH', ui: 'en' },
  { code: 'vietnamese', name: 'Tiếng Việt', locale: 'vi-VN', ui: 'en' },
  { code: 'ukrainian', name: 'Українська', locale: 'uk-UA', ui: 'en' },
  { code: 'indonesian', name: 'Bahasa Indonesia', locale: 'id-ID', ui: 'en' },
];

/** 挂件自身的文字。 */
export const NEWS_UI = {
  zh: {
    title: '游戏新闻', lang: '语言', refresh: '刷新', close: '关闭（可从底部 Dock 再次打开）',
    loading: '加载中…', error: '新闻加载失败，请稍后刷新', empty: '暂无公告',
    fallback: '该语言暂无公告，已显示英文', source: 'Steam 新闻 · 每 1 分钟刷新',
  },
  en: {
    title: 'News', lang: 'Language', refresh: 'Refresh', close: 'Close (reopen from the Dock)',
    loading: 'Loading…', error: 'Failed to load news. Try refreshing later.', empty: 'No news yet',
    fallback: 'No news in this language. Showing English.', source: 'Steam News · refreshes every 1 min',
  },
  ja: {
    title: 'ニュース', lang: '言語', refresh: '更新', close: '閉じる（Dock から再度開けます）',
    loading: '読み込み中…', error: 'ニュースの読み込みに失敗しました', empty: 'お知らせはありません',
    fallback: 'この言語のお知らせはありません。英語を表示しています。', source: 'Steam ニュース · 1 分ごとに更新',
  },
  ko: {
    title: '뉴스', lang: '언어', refresh: '새로고침', close: '닫기 (하단 Dock에서 다시 열 수 있음)',
    loading: '불러오는 중…', error: '뉴스를 불러오지 못했습니다', empty: '공지가 없습니다',
    fallback: '이 언어의 공지가 없어 영어로 표시합니다.', source: 'Steam 뉴스 · 1분마다 갱신',
  },
  ru: {
    title: 'Новости', lang: 'Язык', refresh: 'Обновить', close: 'Закрыть (можно открыть снова из Dock)',
    loading: 'Загрузка…', error: 'Не удалось загрузить новости', empty: 'Новостей пока нет',
    fallback: 'Новостей на этом языке нет. Показан английский.', source: 'Новости Steam · обновляется каждую 1 мин',
  },
};

/** 按代码取语言项；未知代码回退英文。 */
export function newsLangOf(code) {
  return NEWS_LANGUAGES.find((l) => l.code === code) ?? NEWS_LANGUAGES[2];
}

/** 按代码取挂件文字；该语言未提供文字时回退英文。 */
export function newsUiOf(code) {
  return NEWS_UI[newsLangOf(code).ui] ?? NEWS_UI.en;
}

/**
 * 按浏览器语言选择默认的新闻语言。无法匹配时用英文。
 * @param {string} nav navigator.language，如 zh-CN、pt-BR、es-MX。
 * @returns {string} Steam 语言代码。
 */
export function defaultNewsLang(nav = '') {
  const n = String(nav).toLowerCase();
  if (n.startsWith('zh')) return /-(tw|hk|mo)|hant/.test(n) ? 'tchinese' : 'schinese';
  if (n.startsWith('ja')) return 'japanese';
  if (n.startsWith('ko')) return 'koreana';
  if (n.startsWith('ru')) return 'russian';
  if (n.startsWith('pt')) return n.startsWith('pt-br') ? 'brazilian' : 'portuguese';
  if (n.startsWith('es')) return /-(419|mx|ar|co|cl|pe|ve|us)/.test(n) ? 'latam' : 'spanish';
  const byPrefix = {
    fr: 'french', de: 'german', it: 'italian', pl: 'polish', tr: 'turkish',
    th: 'thai', vi: 'vietnamese', uk: 'ukrainian', id: 'indonesian',
  };
  return byPrefix[n.split('-')[0]] ?? 'english';
}
