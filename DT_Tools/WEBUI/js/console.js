(function () {
  const log = document.getElementById('log');
  const inp = document.getElementById('input');
  const statusEl = document.getElementById('status');
  const statusText = document.getElementById('status-text');
  const sugEl = document.getElementById('suggest');
  let seq = 0;
  let hist = [], hIdx = -1;
  let commands = [];
  let matches = [];
  let selIdx = -1;

  const colorMap = {
    red: 'var(--red)',
    orange: 'var(--amber)',
    cyan: 'var(--cyan)',
    white: 'var(--text)',
    green: 'var(--green-bright)'
  };

  function escHtml(s) {
    return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
  }

  function setOnline(ok) {
    if (!statusEl) return;
    statusEl.classList.toggle('online', ok);
    statusEl.classList.toggle('offline', !ok);
    if (statusText) statusText.textContent = ok ? '在线' : '离线';
  }

  async function loadCommands() {
    try {
      const r = await fetch('/api/commands');
      if (!r.ok) throw new Error(String(r.status));
      commands = await r.json();
      if (!Array.isArray(commands)) commands = [];
    } catch {
      commands = [];
    }
  }

  function getPartial() {
    const val = inp.value;
    const m = val.match(/^([\/!]?)(\S*)$/);
    if (!m) return null;
    return { prefix: m[1], partial: m[2].toLowerCase() };
  }

  function filterCommands() {
    const p = getPartial();
    if (p === null) { hideSuggest(); return; }
    const { partial } = p;
    matches = commands.filter(c => {
      if (!partial) return true;
      if ((c.name || '').toLowerCase().startsWith(partial)) return true;
      return (c.aliases || []).some(a => String(a).toLowerCase().startsWith(partial));
    });
    if (matches.length === 0) { hideSuggest(); return; }
    selIdx = 0;
    renderSuggest();
  }

  function renderSuggest() {
    sugEl.innerHTML = '';
    matches.forEach((c, i) => {
      const d = document.createElement('div');
      d.className = 'sug-item' + (i === selIdx ? ' sel' : '');
      d.innerHTML =
        '<span class="sug-name">/' + escHtml(c.name) + '</span>' +
        '<span class="sug-desc">' + escHtml(c.description || c.usage || '') + '</span>';
      d.onmousedown = (e) => { e.preventDefault(); applyMatch(i); };
      sugEl.appendChild(d);
    });
    sugEl.classList.add('open');
  }

  function hideSuggest() {
    sugEl.classList.remove('open');
    matches = [];
    selIdx = -1;
  }

  function applyMatch(idx) {
    const c = matches[idx];
    if (!c) return;
    inp.value = '/' + c.name + ' ';
    hideSuggest();
    inp.focus();
  }

  async function send() {
    const v = inp.value.trim();
    if (!v) return;
    hist.push(v);
    hIdx = hist.length;
    appendLocal('> ' + v, 'var(--cyan)');
    inp.value = '';
    hideSuggest();
    try {
      await fetch('/api/run', { method: 'POST', body: v });
    } catch {
      appendLocal('(发送失败)', 'var(--red)');
    }
  }

  function appendLocal(msg, color) {
    const d = document.createElement('div');
    d.className = 'entry';
    d.style.color = color || 'var(--text)';
    d.textContent = msg;
    log.appendChild(d);
    log.scrollTop = log.scrollHeight;
  }

  async function poll() {
    try {
      const r = await fetch('/api/log?since=' + seq);
      if (!r.ok) throw new Error(String(r.status));
      const data = await r.json();
      setOnline(true);

      // 后端返回数组 [{seq,time,color,msg}]；兼容旧对象形态
      const entries = Array.isArray(data) ? data : (data.entries || []);
      for (const e of entries) {
        if (typeof e.seq === 'number' && e.seq > seq)
          seq = e.seq;
        const d = document.createElement('div');
        d.className = 'entry';
        const col = colorMap[e.color] ||
          (e.level === 'Error' ? 'var(--red)' :
           e.level === 'Warning' ? 'var(--amber)' :
           e.level === 'Debug' ? 'var(--text-dim)' : 'var(--text)');
        const msg = e.msg != null ? e.msg : (e.message || '');
        d.innerHTML =
          '<span class="ts">' + escHtml(e.time || '') + '</span>' +
          '<span style="color:' + col + '">' + escHtml(msg) + '</span>';
        log.appendChild(d);
      }
      if (entries.length)
        log.scrollTop = log.scrollHeight;
    } catch {
      setOnline(false);
    }
  }

  inp.addEventListener('keydown', e => {
    if (sugEl.classList.contains('open') && matches.length) {
      if (e.key === 'ArrowDown') { e.preventDefault(); selIdx = (selIdx + 1) % matches.length; renderSuggest(); return; }
      if (e.key === 'ArrowUp') { e.preventDefault(); selIdx = (selIdx - 1 + matches.length) % matches.length; renderSuggest(); return; }
      if (e.key === 'Tab' || (e.key === 'Enter' && selIdx >= 0 && getPartial())) {
        e.preventDefault();
        applyMatch(selIdx);
        if (e.key === 'Enter') return;
        return;
      }
      if (e.key === 'Escape') { hideSuggest(); return; }
    }
    if (e.key === 'Enter') { e.preventDefault(); send(); return; }
    if (e.key === 'ArrowUp' && !sugEl.classList.contains('open')) {
      e.preventDefault();
      if (hist.length && hIdx > 0) { hIdx--; inp.value = hist[hIdx]; }
      return;
    }
    if (e.key === 'ArrowDown' && !sugEl.classList.contains('open')) {
      e.preventDefault();
      if (hIdx < hist.length - 1) { hIdx++; inp.value = hist[hIdx]; }
      else { hIdx = hist.length; inp.value = ''; }
    }
  });
  inp.addEventListener('input', filterCommands);
  document.getElementById('send').onclick = send;

  loadCommands();
  poll();
  setInterval(poll, 800);
})();
