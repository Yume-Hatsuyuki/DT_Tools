import { createApp } from 'vue';
import App from './App.vue';
import { dirtyState } from './composables/useConfigToolbar.js';
import './styles/tokens.css';

createApp(App).mount('#app');

// 关闭页面/刷新前，若 Config 或 Automation 有未保存改动给出提醒
// （浏览器原生 confirm 文案不可自定义，这是规范行为不是疏漏）。
window.addEventListener('beforeunload', (e) => {
  if (dirtyState.config || dirtyState.automation) {
    e.preventDefault();
    e.returnValue = '';
  }
});
