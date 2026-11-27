import { createApp } from 'vue';
import App from './App.vue';
import { dirtyState } from './composables/useConfigToolbar.js';
import './styles/tokens.css';

createApp(App).mount('#app');

window.addEventListener('beforeunload', (e) => {
  if (dirtyState.config || dirtyState.automation) {
    e.preventDefault();
    e.returnValue = '';
  }
});
