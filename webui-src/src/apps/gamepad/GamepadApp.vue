<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue';
import { API, createPoller } from '../../api.js';

/**
 * 手柄应用：配置手柄按钮映射（普通 / 光标两层），并实时显示手柄读数。
 * 手柄只控制游戏，不操作 WebUI；这里只负责配置。映射保存后即时生效。
 */
const enabled = ref(false);
const running = ref(false);
const connected = ref(false);
const focused = ref(false);
const cursorMode = ref(false);
const stick = ref({ lx: 0, ly: 0, rx: 0, ry: 0 });
const triggers = ref({ lt: 0, rt: 0 });
const pressed = ref([]);
const heldOutputs = ref(0);
const toggle = ref('Back');
const bindings = ref([]);
const buttons = ref([]);
const actions = ref([]);
const keys = ref([]);
const mouse = ref([]);
const error = ref('');
const busy = ref(false);
const toast = ref({ text: '', error: false });
let toastTimer = null;

const poller = createPoller(reload, 250);

async function reload() {
  const j = await API.gamepadState();
  if (j.unauthorized) return;
  if (!j.ok) { error.value = j.error || '读取失败'; return; }
  error.value = '';
  const d = j.data || {};
  enabled.value = !!d.enabled;
  running.value = !!d.running;
  connected.value = !!d.connected;
  focused.value = !!d.focused;
  cursorMode.value = !!d.cursorMode;
  stick.value = d.stick || { lx: 0, ly: 0, rx: 0, ry: 0 };
  triggers.value = d.triggers || { lt: 0, rt: 0 };
  pressed.value = d.pressed || [];
  heldOutputs.value = d.heldOutputs || 0;
  toggle.value = d.toggle || 'Back';
  buttons.value = d.buttons || [];
  actions.value = d.actions || [];
  keys.value = d.keys || [];
  mouse.value = d.mouse || [];
  // 只在映射真的变化时替换，避免轮询把正在打开的下拉框刷掉
  const next = d.bindings || [];
  if (JSON.stringify(next) !== JSON.stringify(bindings.value)) bindings.value = next;
}

function flash(text, isError = false) {
  toast.value = { text, error: isError };
  clearTimeout(toastTimer);
  if (text) toastTimer = setTimeout(() => { toast.value = { text: '', error: false }; }, 2600);
}

async function act(fn, okText) {
  if (busy.value) return;
  busy.value = true;
  try {
    const j = await fn();
    if (j.unauthorized) return;
    if (!j.ok) { flash(j.data?.message || j.error || '操作失败', true); return; }
    flash(j.data?.message || okText || '');
    await reload();
  } finally {
    busy.value = false;
  }
}

const setBinding = (row, layer, value) => act(() => API.gamepadBind(layer, row.button, value));
const setToggle = (value) => act(() => API.gamepadToggle(value), '已保存');
const resetAll = () => {
  if (!confirm('恢复默认映射？当前的自定义按键会被覆盖。')) return;
  act(() => API.gamepadReset(), '已恢复默认映射');
};

// 下拉框分组：不输出 / 游戏动作（读玩家实际键位）/ 固定键 / 鼠标
const outputOptions = computed(() => [
  { group: '不输出', items: [{ value: 'none', label: '（无）' }] },
  { group: '游戏动作（读取玩家在游戏里设置的键位）', items: actions.value.map(a => ({ value: a.token, label: a.label })) },
  { group: '固定键', items: keys.value.map(k => ({ value: k.token, label: k.label })) },
  { group: '鼠标', items: mouse.value.map(m => ({ value: m.token, label: m.label })) },
]);

// 光标层多一项「跟随普通模式」：未单独设置的按钮显示普通层的输出（虚线框），选它可恢复跟随
const cursorOptions = computed(() => [
  { group: '跟随', items: [{ value: 'inherit', label: '（跟随普通模式）' }] },
  ...outputOptions.value,
]);

const labelOf = computed(() => {
  const map = {};
  for (const b of buttons.value) map[b.id] = b.label;
  return map;
});

const statusChips = computed(() => [
  { text: enabled.value ? '功能已启用' : '功能未启用（请在「DT 配置」中开启「手柄」）', on: enabled.value },
  { text: connected.value ? '手柄已连接' : '未检测到手柄', on: connected.value },
  { text: focused.value ? '游戏窗口在前台' : '游戏窗口不在前台（不注入）', on: focused.value },
  { text: cursorMode.value ? '光标模式' : '普通模式', on: cursorMode.value },
]);

const dotStyle = (x, y) => ({ left: `${50 + x * 40}%`, top: `${50 - y * 40}%` });

onMounted(() => {
  reload();
  poller.start();
});
onUnmounted(() => poller.stop());
</script>

<template>
  <div class="gp-app">
    <div class="status-row">
      <span v-for="c in statusChips" :key="c.text" class="chip" :class="{ on: c.on }">{{ c.text }}</span>
    </div>

    <div class="live">
      <div class="sticks">
        <div class="stick-box">
          <div class="stick-ring"></div>
          <div class="stick-dot" :style="dotStyle(stick.lx, stick.ly)"></div>
          <span class="stick-tag">左摇杆</span>
        </div>
        <div class="stick-box">
          <div class="stick-ring"></div>
          <div class="stick-dot" :style="dotStyle(stick.rx, stick.ry)"></div>
          <span class="stick-tag">右摇杆</span>
        </div>
        <div class="trig">
          <div class="trig-row"><span>LT</span><div class="trig-bar"><i :style="{ width: (triggers.lt * 100) + '%' }"></i></div></div>
          <div class="trig-row"><span>RT</span><div class="trig-bar"><i :style="{ width: (triggers.rt * 100) + '%' }"></i></div></div>
        </div>
      </div>
      <div class="pressed">
        <span class="pressed-title">当前按下</span>
        <div class="pressed-list">
          <span v-if="!pressed.length" class="muted">无</span>
          <span v-for="p in pressed" :key="p" class="chip on">{{ labelOf[p] || p }}</span>
        </div>
        <span class="pressed-foot">同时输出中的键：{{ heldOutputs }}</span>
      </div>
    </div>

    <div class="toggle-row">
      <span class="label">切换光标模式的按钮</span>
      <select class="sel" :value="toggle" :disabled="busy" @change="setToggle($event.target.value)">
        <option v-for="b in buttons" :key="b.id" :value="b.id">{{ b.label }}</option>
      </select>
    </div>

    <div class="table">
      <div class="thead">
        <span>按钮</span><span>普通模式</span><span>光标模式（菜单）</span>
      </div>
      <div v-for="row in bindings" :key="row.button" class="trow">
        <span class="btn-name">{{ row.label }}</span>
        <select class="sel" :value="row.normal" :disabled="busy" @change="setBinding(row, 'normal', $event.target.value)">
          <optgroup v-for="g in outputOptions" :key="g.group" :label="g.group">
            <option v-for="o in g.items" :key="o.value" :value="o.value">{{ o.label }}</option>
          </optgroup>
        </select>
        <select class="sel" :class="{ inherited: row.cursorInherited }" :title="row.cursorInherited ? '未单独设置，跟随普通模式' : ''" :value="row.cursor" :disabled="busy" @change="setBinding(row, 'cursor', $event.target.value)">
          <optgroup v-for="g in cursorOptions" :key="g.group" :label="g.group">
            <option v-for="o in g.items" :key="o.value" :value="o.value">{{ o.label }}</option>
          </optgroup>
        </select>
      </div>
    </div>

    <div class="foot">
      <button class="tb-btn" :disabled="busy" @click="resetAll">恢复默认映射</button>
      <span class="hint">手柄只控制游戏，本应用只负责配置。游戏动作读取玩家在游戏里设置的实际键位；Steam Input 保持默认即可。</span>
      <span class="hint">光标模式里虚线框的按钮未单独设置，跟随普通模式；选「（跟随普通模式）」可恢复，选「（无）」则关闭该按钮。</span>
    </div>

    <div v-if="error" class="err">{{ error }}</div>
    <div v-if="toast.text" class="toast" :class="{ error: toast.error }">{{ toast.text }}</div>
  </div>
</template>

<style scoped>
.gp-app { position: relative; display: flex; flex-direction: column; gap: 10px; height: 100%; padding: 12px 14px; box-sizing: border-box; background: var(--surface-0); color: var(--text-1); overflow-x: hidden; overflow-y: auto; }
/* 子块不参与压缩：窗口变矮时由 .gp-app 出现滚动条，而不是把 overflow:hidden 的映射表裁掉 */
.gp-app > * { flex-shrink: 0; }
.status-row { display: flex; flex-wrap: wrap; gap: 6px; }
.chip { font-size: 11px; color: var(--text-2); border: 1px solid var(--line); border-radius: 999px; padding: 2px 9px; white-space: nowrap; }
.chip.on { color: var(--accent-cyan); border-color: var(--accent-cyan-dim); }
.muted { color: var(--text-2); font-size: 12px; }
.live { display: flex; gap: 16px; align-items: stretch; padding: 10px 12px; border: 1px solid var(--line); border-radius: var(--radius-sm); background: var(--surface-1); flex-wrap: wrap; }
.sticks { display: flex; gap: 14px; align-items: center; }
.stick-box { position: relative; width: 84px; height: 84px; }
.stick-ring { position: absolute; inset: 0; border: 1px solid var(--line-strong); border-radius: 50%; }
.stick-dot { position: absolute; width: 12px; height: 12px; margin: -6px 0 0 -6px; border-radius: 50%; background: var(--accent-cyan); box-shadow: 0 0 6px rgba(0, 229, 255, 0.5); }
.stick-tag { position: absolute; left: 0; right: 0; bottom: -18px; text-align: center; font-size: 11px; color: var(--text-2); }
.trig { display: flex; flex-direction: column; gap: 8px; min-width: 130px; }
.trig-row { display: flex; align-items: center; gap: 8px; font-size: 11px; color: var(--text-2); }
.trig-bar { flex: 1; height: 6px; border-radius: 3px; background: var(--line-strong); overflow: hidden; }
.trig-bar i { display: block; height: 100%; background: var(--accent-cyan); }
.pressed { display: flex; flex-direction: column; gap: 6px; flex: 1; min-width: 180px; }
.pressed-title { font-size: 11px; color: var(--text-2); }
.pressed-list { display: flex; flex-wrap: wrap; gap: 6px; min-height: 24px; }
.pressed-foot { font-size: 10.5px; color: var(--text-2); margin-top: auto; }
.toggle-row { display: flex; align-items: center; gap: 10px; }
.label { font-size: 12px; color: var(--text-1); }
.sel { background: var(--surface-0); border: 1px solid var(--line); border-radius: var(--radius-sm); color: var(--text-0); font-size: 12px; padding: 4px 6px; min-width: 0; }
.sel:disabled { opacity: 0.5; }
.sel.inherited { border-style: dashed; color: var(--text-2); }
.table { border: 1px solid var(--line); border-radius: var(--radius-sm); overflow: hidden; }
.thead, .trow { display: grid; grid-template-columns: 110px 1fr 1fr; gap: 8px; align-items: center; padding: 6px 10px; }
.thead { background: var(--surface-2); font-size: 11px; color: var(--text-2); }
.trow { border-top: 1px solid var(--line); background: var(--surface-1); }
.trow .sel { width: 100%; }
.btn-name { font-size: 12.5px; color: var(--text-0); }
.foot { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
.hint { font-size: 11px; color: var(--text-2); }
.tb-btn { background: var(--surface-2); border: 1px solid var(--line); border-radius: var(--radius-sm); color: var(--text-1); font-size: 11.5px; padding: 6px 10px; cursor: pointer; }
.tb-btn:disabled { opacity: 0.45; cursor: default; }
.err { font-size: 12px; color: var(--accent-red); }
.toast { position: absolute; top: 12px; right: 14px; background: var(--surface-2); border: 1px solid var(--accent-cyan-dim); border-radius: var(--radius-sm); padding: 8px 12px; font-size: 12px; color: var(--text-0); box-shadow: 0 8px 24px rgba(0, 0, 0, 0.4); max-width: 70%; }
.toast.error { border-color: var(--accent-red); color: var(--accent-red); }
</style>
