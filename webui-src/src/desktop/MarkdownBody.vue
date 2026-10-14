<script>
import { h } from 'vue';
import { parseMarkdown } from './markdownParse.mjs';

/**
 * 更新说明的渲染组件。解析器输出语法树，这里用 h() 逐节点构建 VNode：
 * 文本由 Vue 转义，原始 HTML 只会显示为文字，所以不使用 v-html。
 * 链接的协议已由解析器限定为 http、https、mailto；统一加 target=_blank 与 rel=noopener noreferrer，
 * 防止新标签页通过 window.opener 操纵本页面。图片不自动加载（避免外部请求），只显示为链接。
 */

const ALIGN_OK = new Set(['left', 'center', 'right']);

function renderInline(nodes) {
  return nodes.map((n) => {
    switch (n.type) {
      case 'text':
        return n.value;
      case 'code':
        return h('code', { class: 'md-code' }, n.value);
      case 'strong':
        return h('strong', null, renderInline(n.children));
      case 'em':
        return h('em', null, renderInline(n.children));
      case 'del':
        return h('del', null, renderInline(n.children));
      case 'br':
        return h('br');
      case 'link':
        return h('a', { href: n.href, target: '_blank', rel: 'noopener noreferrer' }, renderInline(n.children));
      case 'image': {
        const label = '图片：' + (n.alt || n.href || '');
        if (!n.href) return label;
        return h('a', { class: 'md-img', href: n.href, target: '_blank', rel: 'noopener noreferrer' }, label);
      }
      default:
        return '';
    }
  });
}

function renderBlock(b) {
  switch (b.type) {
    case 'heading':
      return h('h' + b.level, { class: 'md-h md-h' + b.level }, renderInline(b.children));
    case 'paragraph':
      return h('p', { class: 'md-p' }, renderInline(b.children));
    case 'code':
      return h('pre', { class: 'md-pre' }, [
        h('code', { class: 'md-code-block', 'data-lang': b.lang || null }, b.text),
      ]);
    case 'quote':
      return h('blockquote', { class: 'md-quote' }, b.children.map(renderBlock));
    case 'list': {
      const hasTask = b.items.some((it) => it.task);
      const props = { class: 'md-list' + (hasTask ? ' md-tasks' : '') };
      if (b.ordered && b.start !== 1) props.start = b.start;
      const items = b.items.map((it) => {
        const kids = it.children.map(renderBlock);
        if (!it.task) return h('li', null, kids);
        const box = h('input', { type: 'checkbox', class: 'md-check', checked: it.task === 'checked', disabled: true });
        // 复选框与首段同行：段落是块级元素，单独放在前面会把文字挤到下一行
        const first = it.children[0];
        if (first && first.type === 'paragraph') {
          kids[0] = h('p', { class: 'md-p' }, [box, ...renderInline(first.children)]);
        } else {
          kids.unshift(box);
        }
        return h('li', { class: 'md-task' }, kids);
      });
      return h(b.ordered ? 'ol' : 'ul', props, items);
    }
    case 'table': {
      const cellProps = (idx) => {
        const a = b.align[idx];
        return a && ALIGN_OK.has(a) ? { style: { textAlign: a } } : {};
      };
      const head = h('thead', null, [
        h('tr', null, b.head.map((c, idx) => h('th', cellProps(idx), renderInline(c)))),
      ]);
      const body = h('tbody', null, b.rows.map((r) =>
        h('tr', null, r.map((c, idx) => h('td', cellProps(idx), renderInline(c)))),
      ));
      return h('div', { class: 'md-table-wrap' }, [h('table', { class: 'md-table' }, [head, body])]);
    }
    case 'hr':
      return h('hr', { class: 'md-hr' });
    default:
      return null;
  }
}

export default {
  name: 'MarkdownBody',
  props: {
    source: { type: String, default: '' },
  },
  render() {
    return h('div', { class: 'md-body' }, parseMarkdown(this.source).map(renderBlock));
  },
};
</script>

<style>
/* 样式全部挂在 .md-body 下，避免影响页面其它部分 */
.md-body { color: var(--text-1); font-size: 12px; line-height: 1.7; word-break: break-word; }
.md-body .md-h { color: var(--text-0); font-weight: 700; margin: 8px 0 4px; line-height: 1.4; }
.md-body .md-h1 { font-size: 15px; }
.md-body .md-h2 { font-size: 14px; }
.md-body .md-h3 { font-size: 13px; }
.md-body .md-h4, .md-body .md-h5, .md-body .md-h6 { font-size: 12px; }
.md-body .md-p { margin: 3px 0; }
.md-body .md-list { margin: 3px 0; padding-left: 20px; }
.md-body .md-list li { margin: 2px 0; }
.md-body li.md-task { list-style: none; margin-left: -18px; }
.md-body .md-check { accent-color: var(--shell-glow); margin: 0 6px 0 0; vertical-align: -1px; }
.md-body .md-quote {
  margin: 4px 0;
  padding: 2px 10px;
  border-left: 3px solid var(--line);
  color: var(--text-2);
}
.md-body .md-pre {
  margin: 6px 0;
  padding: 8px 10px;
  background: var(--surface-1, rgba(0, 0, 0, 0.3));
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  overflow-x: auto;
}
.md-body .md-code-block { font-family: monospace; font-size: 11.5px; color: var(--text-0); white-space: pre; }
.md-body .md-code {
  font-family: monospace;
  font-size: 11.5px;
  color: var(--text-0);
  background: var(--surface-1, rgba(255, 255, 255, 0.06));
  padding: 1px 4px;
  border-radius: 3px;
}
.md-body .md-table-wrap { overflow-x: auto; margin: 6px 0; }
.md-body .md-table { border-collapse: collapse; width: 100%; font-size: 11.5px; }
.md-body .md-table th,
.md-body .md-table td { border: 1px solid var(--line); padding: 4px 8px; vertical-align: top; }
.md-body .md-table th { background: var(--surface-1, rgba(255, 255, 255, 0.05)); color: var(--text-0); font-weight: 600; }
.md-body .md-hr { border: 0; border-top: 1px solid var(--line); margin: 8px 0; }
.md-body a { color: var(--shell-glow); text-decoration: none; word-break: break-all; }
.md-body a:hover { text-decoration: underline; }
.md-body strong { color: var(--text-0); }
.md-body del { color: var(--text-2); }
</style>
