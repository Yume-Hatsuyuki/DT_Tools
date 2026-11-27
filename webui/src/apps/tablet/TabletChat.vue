<script setup>
import { computed } from 'vue';
import { avatarSrc, personalColor } from './tabletAvatars.js';
import { groupByRound } from './tabletFormat.js';
import { API } from '../../api.js';
import { t } from '../../i18n/index.js';

const props = defineProps({
  lines: { type: Array, default: () => [] },
  numbering: { type: Map, default: () => new Map() },
});
const groups = computed(() => groupByRound(props.lines, props.numbering));
const emit = defineEmits(['copy-photo', 'save-photo', 'open-photo']);

function bubbleStyle(l) {
  if (l.kind === 'proposition' || l.isDead) return undefined;
  return { background: personalColor(l.avatar) };
}

function photoUrl(key) {
  return API.tabletPhotoUrl(key);
}
</script>

<template>
  <div class="chat-list">
    <template v-for="g in groups" :key="g.round">
      <div class="round-sep">—— 第 {{ g.no }} 局 ——</div>
      <template v-for="l in g.items" :key="l.seq">
      <div v-if="l.kind === 'state'" class="state-line">—— {{ l.text }} ——</div>
      <div v-else class="msg" :class="{ mine: l.isMine }">
        <img class="avatar" :class="{ dead: l.isDead }" :src="avatarSrc(l.avatar)" alt="" draggable="false" />
        <div class="col">
          <div class="meta">
            <span class="name">{{ l.name }}</span>
            <span v-if="l.isDead" class="dead-tag">已死亡</span>
            <span class="time">{{ l.time }}</span>
          </div>
          <div v-if="l.kind === 'photo'" class="bubble photo" :class="{ dead: l.isDead }">
            <button
              type="button"
              class="photo-open"
              :title="t('tablet.photo.open')"
              :aria-label="t('tablet.photo.open')"
              @click="emit('open-photo', l)"
            >
              <img :src="photoUrl(l.photoKey)" alt="" loading="lazy" draggable="false" />
            </button>
            <div class="photo-actions">
              <button type="button" @click="emit('copy-photo', l)">复制</button>
              <button type="button" @click="emit('save-photo', l)">另存为</button>
            </div>
          </div>
          <div
            v-else
            class="bubble"
            :class="[l.kind, { dead: l.isDead }]"
            :style="bubbleStyle(l)"
          >{{ l.text }}</div>
        </div>
      </div>
      </template>
    </template>
  </div>
</template>

<style scoped>
.round-sep { text-align: center; font-size: 12px; font-weight: 600; color: var(--accent-amber); padding: 4px 0; letter-spacing: 0.02em; }
.chat-list { display: flex; flex-direction: column; gap: 12px; padding: 10px 12px; }
.state-line { text-align: center; font-size: 12px; font-weight: 600; color: var(--accent-amber); padding: 4px 0; letter-spacing: 0.02em; }
.msg { display: flex; align-items: flex-start; gap: 8px; }
.msg.mine { flex-direction: row-reverse; }
.avatar { width: 40px; height: 40px; flex: none; border-radius: 10px; background: rgba(255, 255, 255, 0.05); object-fit: contain; }
.avatar.dead { opacity: 0.45; filter: grayscale(1); }
.col { display: flex; flex-direction: column; gap: 3px; min-width: 0; max-width: 72%; }
.msg.mine .col { align-items: flex-end; }
.meta { display: flex; align-items: baseline; gap: 6px; font-size: 11px; color: var(--text-2); }
.msg.mine .meta { flex-direction: row-reverse; }
.name { color: var(--text-1); font-weight: 500; }
.dead-tag { color: var(--accent-red); }
.time { font-family: var(--font-mono); font-size: 10px; }
.bubble {
  padding: 7px 11px;
  border-radius: 12px;
  background: #ffffff;
  color: #1d1d1d;
  font-size: 13.5px;
  line-height: 1.5;
  white-space: pre-wrap;
  word-break: break-word;
  max-width: 100%;
}
.bubble.dead { background: #4b4f5c; color: #d0d0d0; }
.bubble.proposition {
  background: #3a2238;
  color: #ffe6ee;
  border: 1px solid #ff97ab;
  font-weight: 500;
}
.bubble.photo { padding: 4px; background: rgba(255, 255, 255, 0.06); }
.bubble.photo img { display: block; max-width: 260px; max-height: 260px; border-radius: 8px; }
.photo-open { display: block; padding: 0; margin: 0; border: 0; background: none; border-radius: 8px; cursor: zoom-in; }
.photo-open:focus-visible { outline: 2px solid var(--accent-cyan); outline-offset: 2px; }
.photo-actions { display: flex; justify-content: flex-end; gap: 6px; padding: 4px 2px 2px; }
.photo-actions button {
  background: rgba(0, 0, 0, 0.28);
  border: 1px solid var(--line);
  border-radius: 4px;
  color: var(--text-1);
  font-size: 11px;
  padding: 2px 8px;
  cursor: pointer;
}
.photo-actions button:hover { color: var(--text-0); border-color: var(--line-strong); }
</style>
