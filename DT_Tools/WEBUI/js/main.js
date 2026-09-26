/**
 * 入口与 hash 路由：#/console（默认）· #/config · #/automation。
 * 视图懒挂载（首次激活 mount，之后常驻），切换时 setActive(true/false) 控制各自轮询。
 */

import { mountConsole } from './views/console.js';
import { mountConfig, setActiveConfig } from './views/config.js';
import { mountAutomation, setActiveAutomation } from './views/automation.js';
import { dirtyState } from './views/toolbar.js';

const views = {
  console: {
    panel: 'panel-console',
    mount: mountConsole,
    setActive: null,          // 控制台日志源常驻，无激活/停用
  },
  config: {
    panel: 'panel-config',
    mount: mountConfig,
    setActive: setActiveConfig,
  },
  automation: {
    panel: 'panel-automation',
    mount: mountAutomation,
    setActive: setActiveAutomation,
  },
};

let currentName = null;
const mounted = new Set();

function activate(name) {
  const view = views[name] || views.console;
  name = views[name] ? name : 'console';
  if (name === currentName) return;

  if (!mounted.has(name)) {
    view.mount();
    mounted.add(name);
  }
  if (currentName && views[currentName].setActive)
    views[currentName].setActive(false);
  if (view.setActive)
    view.setActive(true);

  document.querySelectorAll('.tab').forEach(b =>
    b.classList.toggle('active', b.dataset.view === name));
  document.querySelectorAll('.panel').forEach(p =>
    p.classList.toggle('active', p.id === view.panel));

  currentName = name;
}

function currentHash() {
  return (location.hash || '').replace(/^#\/?/, '') || 'console';
}

document.querySelectorAll('.tab').forEach(btn => {
  btn.addEventListener('click', () => {
    location.hash = '#/' + btn.dataset.view;
  });
});

window.addEventListener('hashchange', () => activate(currentHash()));

// 任一面板有未保存改动时，离开页面前提醒
window.addEventListener('beforeunload', (e) => {
  if (dirtyState.config || dirtyState.automation) {
    e.preventDefault();
    e.returnValue = '';
  }
});

activate(currentHash());
