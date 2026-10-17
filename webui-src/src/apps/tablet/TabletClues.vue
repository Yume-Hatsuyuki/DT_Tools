<script setup>
import { avatarSrc } from './tabletAvatars.js';

/**
 * 平板电脑的线索栏。命题在前（显示提交者与句子），线索按时间从晚到早（显示对象、地点、时间）。
 * 数据已由 orderClues 排好序。
 */
defineProps({
  clues: { type: Array, default: () => [] },
});
</script>

<template>
  <div class="clue-list">
    <div v-if="!clues.length" class="empty">暂无线索与命题</div>
    <div
      v-for="c in clues"
      :key="c.key"
      class="card"
      :class="{ proposition: c.isProposition, destroyed: c.destroyed }"
    >
      <div v-if="c.isProposition" class="head">
        <img class="avatar-sm" :src="avatarSrc(c.ownerAvatar)" alt="" draggable="false" />
        <span class="owner">{{ c.ownerName }}</span>
        <span class="tag hot">命题</span>
      </div>
      <div v-else class="head">
        <span class="tag">线索</span>
      </div>
      <p v-if="c.isProposition" class="sentence">{{ c.sentence }}</p>
      <div class="fields">
        <span class="who"><img class="avatar-xs" :src="avatarSrc(c.whoAvatar)" alt="" draggable="false" />{{ c.who }}</span>
        <span>{{ c.where }}</span>
        <span>{{ c.when }}</span>
      </div>
      <span v-if="c.destroyed" class="destroyed-tag">已销毁</span>
    </div>
  </div>
</template>

<style scoped>
.clue-list { display: flex; flex-direction: column; gap: 8px; padding: 10px 12px; }
.empty { text-align: center; font-size: 12px; color: var(--text-2); padding: 30px 0; }
.card {
  position: relative;
  background: var(--surface-1);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  padding: 9px 11px 9px 13px;
  display: flex;
  flex-direction: column;
  gap: 5px;
}
.card.proposition { border-color: rgba(255, 151, 171, 0.35); }
.card.proposition::before {
  content: "";
  position: absolute;
  left: 0; top: 8px; bottom: 8px;
  width: 3px;
  border-radius: 2px;
  background: #ff97ab;
}
.card.destroyed { opacity: 0.6; }
.head { display: flex; align-items: center; gap: 7px; font-size: 11.5px; color: var(--text-2); }
.avatar-sm { width: 22px; height: 22px; border-radius: 6px; object-fit: contain; background: rgba(255, 255, 255, 0.05); }
.owner { color: var(--text-0); font-weight: 500; }
.tag { font-size: 10.5px; color: var(--text-2); border: 1px solid var(--line-strong); border-radius: 999px; padding: 0 7px; }
.tag.hot { color: #ff97ab; border-color: rgba(255, 151, 171, 0.5); }
.sentence { margin: 0; font-size: 13.5px; line-height: 1.5; color: var(--text-0); word-break: break-word; }
.fields { display: flex; flex-wrap: wrap; gap: 4px 12px; font-size: 12px; color: var(--text-1); align-items: center; }
.who { display: inline-flex; align-items: center; gap: 5px; color: var(--text-0); }
.avatar-xs { width: 18px; height: 18px; border-radius: 5px; object-fit: contain; background: rgba(255, 255, 255, 0.05); }
.destroyed-tag { align-self: flex-start; font-size: 10.5px; color: var(--accent-red); }
</style>
