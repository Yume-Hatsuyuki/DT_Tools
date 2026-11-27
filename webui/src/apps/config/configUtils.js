export function splitDesc(desc) {
  const lines = String(desc || '').split(/\r?\n/);
  let author = '', side = '';
  const body = [];
  for (const line of lines) {
    const a = line.match(/^Author:\s*(.*)$/i);
    if (a) { author = a[1].trim(); continue; }
    const s = line.match(/^Side:\s*(.*)$/i);
    if (s) { side = s[1].trim(); continue; }
    body.push(line);
  }
  while (body.length && !body[0].trim()) body.shift();
  while (body.length && !body[body.length - 1].trim()) body.pop();
  return { author, side, text: body.join('\n') };
}

export function sectionMeta(section) {
  const enabledEntry = (section.entries || []).find(e => e.key === section.enabledKey);
  if (!enabledEntry) return { author: '', side: '' };
  const { author, side } = splitDesc(enabledEntry.description);
  return { author, side };
}

export function normalizeOptions(accepts) {
  if (!accepts || typeof accepts !== 'object' || !Array.isArray(accepts.options)) return null;
  return accepts.options;
}

export function isNumberType(type) {
  const t = (type || '').toLowerCase();
  return t.includes('int') || t.includes('single') || t.includes('double') || t.includes('float');
}

export function sectionMatches(section, query) {
  const q = query.trim().toLowerCase();
  if (!q) return true;
  if (section.section.toLowerCase().includes(q)) return true;
  return (section.entries || []).some(e =>
    (e.key || '').toLowerCase().includes(q) || (e.description || '').toLowerCase().includes(q));
}
