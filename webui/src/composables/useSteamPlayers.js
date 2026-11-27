import { onMounted, onUnmounted, ref } from 'vue';
import { API, createPoller } from '../api.js';

export function useSteamPlayers() {
  const count = ref(null);
  const ok = ref(false);
  let poller = null;

  onMounted(() => {
    poller = createPoller(async () => {
      const d = await API.steamPlayers();
      if (!d.ok || typeof d.players !== 'number') {
        ok.value = false;
        return;
      }
      count.value = d.players;
      ok.value = true;
    }, 60000);
    poller.start();
  });

  onUnmounted(() => poller && poller.stop());

  return { count, ok };
}
