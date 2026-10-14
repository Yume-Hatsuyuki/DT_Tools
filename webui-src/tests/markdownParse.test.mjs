// 运行方式：cd webui-src && node --test tests/
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { parseMarkdown, MAX_DEPTH, MAX_SOURCE_LENGTH } from '../src/desktop/markdownParse.mjs';

const t = (value) => ({ type: 'text', value });
const first = (src) => parseMarkdown(src)[0];
const inlineOf = (src) => first(src).children;
const typesOf = (nodes) => nodes.map((n) => n.type);

test('标题：ATX 标题与收尾的 #，且 #标签 不是标题', () => {
  assert.deepEqual(parseMarkdown('## 标题 ##'), [{ type: 'heading', level: 2, children: [t('标题')] }]);
  assert.equal(first('#标签').type, 'paragraph');
  assert.equal(first('# #').children.length, 0);
});

test('段落：软换行变空格，硬换行（行尾两个空格）变 br', () => {
  assert.deepEqual(inlineOf('a\nb'), [t('a b')]);
  assert.deepEqual(inlineOf('a  \nb'), [t('a'), { type: 'br' }, t('b')]);
  assert.deepEqual(inlineOf('a\\\nb'), [t('a'), { type: 'br' }, t('b')]);
});

test('强调：加粗、斜体、删除线、行内代码', () => {
  assert.deepEqual(typesOf(inlineOf('**粗** *斜* ~~删~~ `code`')), ['strong', 'text', 'em', 'text', 'del', 'text', 'code']);
  assert.deepEqual(inlineOf('`a`'), [{ type: 'code', value: 'a' }]);
  assert.deepEqual(inlineOf('`` a ` b ``'), [{ type: 'code', value: 'a ` b' }]);
});

test('转义：反斜杠让标记符号变成普通文字', () => {
  assert.deepEqual(inlineOf('\\*不是斜体\\*'), [t('*不是斜体*')]);
});

test('下划线：单词中间的下划线不作为强调', () => {
  assert.deepEqual(inlineOf('foo_bar_baz'), [t('foo_bar_baz')]);
});

test('链接：只放行 http、https、mailto', () => {
  assert.deepEqual(inlineOf('[站点](https://example.com)'), [{ type: 'link', href: 'https://example.com', children: [t('站点')] }]);
  assert.deepEqual(inlineOf('[x](javascript:alert(1))'), [t('x')]);
  assert.deepEqual(inlineOf('[x](//evil.example)'), [t('x')]);
  assert.deepEqual(inlineOf('[m](mailto:a@b.c)'), [{ type: 'link', href: 'mailto:a@b.c', children: [t('m')] }]);
});

test('链接：带标题的写法，标题不进入地址', () => {
  const [link] = inlineOf('[站点](https://example.com "标题")');
  assert.equal(link.href, 'https://example.com');
});

test('自动链接：裸 URL 与尖括号写法，末尾标点不计入地址', () => {
  assert.deepEqual(inlineOf('see https://example.com/a.'), [
    t('see '),
    { type: 'link', href: 'https://example.com/a', children: [t('https://example.com/a')] },
    t('.'),
  ]);
  assert.equal(inlineOf('<https://x.example>')[0].href, 'https://x.example');
});

test('图片：只生成图片节点（不自动加载），危险协议的图片地址为 null', () => {
  assert.deepEqual(inlineOf('![猫](https://e.example/c.png)'), [{ type: 'image', alt: '猫', href: 'https://e.example/c.png' }]);
  assert.equal(inlineOf('![x](javascript:alert(1))')[0].href, null);
});

test('原始 HTML：一律当作文字，不产生任何 html 类型的节点', () => {
  const nodes = parseMarkdown('<img src=x onerror=alert(1)> <script>x</script>\n\n<div>块</div>');
  const all = JSON.stringify(nodes);
  assert.equal(all.includes('"html"'), false);
  assert.equal(nodes.every((n) => n.type === 'paragraph'), true);
  assert.equal(inlineOf('<img src=x>')[0].type, 'text');
});

test('无序列表：嵌套通过缩进表达', () => {
  const list = first('- a\n  - b\n- c');
  assert.equal(list.type, 'list');
  assert.equal(list.ordered, false);
  assert.equal(list.items.length, 2);
  assert.equal(list.items[0].children[1].type, 'list');
  assert.equal(list.items[0].children[1].items[0].children[0].children[0].value, 'b');
});

test('有序列表：保留起始编号', () => {
  const list = first('3. x\n4. y');
  assert.equal(list.ordered, true);
  assert.equal(list.start, 3);
  assert.equal(list.items.length, 2);
});

test('列表：标记种类变化时开启新列表', () => {
  const nodes = parseMarkdown('1. a\n- b');
  assert.deepEqual(typesOf(nodes), ['list', 'list']);
});

test('列表：空行之后仍是同类项时，视为同一个松散列表', () => {
  const list = first('- a\n\n- b');
  assert.equal(list.items.length, 2);
});

test('任务列表：勾选与未勾选', () => {
  const list = first('- [ ] 待办\n- [x] 完成\n- [X] 也完成');
  assert.deepEqual(list.items.map((i) => i.task), ['unchecked', 'checked', 'checked']);
  assert.equal(list.items[0].children[0].children[0].value, '待办');
});

test('分隔线：三个以上的 - * _ 是分隔线，不是列表', () => {
  assert.equal(first('---').type, 'hr');
  assert.equal(first('* * *').type, 'hr');
  assert.equal(first('___').type, 'hr');
});

test('表格：对齐方式、列数不足补空、转义竖线', () => {
  const table = first('| 名称 | 数量 |\n|:---|---:|\n| a | 1 |\n| b |');
  assert.equal(table.type, 'table');
  assert.deepEqual(table.align, ['left', 'right']);
  assert.equal(table.head.length, 2);
  assert.equal(table.rows.length, 2);
  assert.equal(table.rows[1].length, 2);
  assert.deepEqual(table.rows[1][1], []);
  const escaped = first('| a\\|b | c |\n| - | - |\n| x | y |');
  assert.equal(escaped.head[0][0].value, 'a|b');
});

test('表格：列数不一致时不是表格', () => {
  assert.notEqual(first('| a | b |\n| - |\n| x |').type, 'table');
});

test('代码块：保留语言标注，内容不做任何解析', () => {
  const code = first('```js\nconst a = "<b>";\n**不是加粗**\n```');
  assert.deepEqual(code, { type: 'code', lang: 'js', text: 'const a = "<b>";\n**不是加粗**' });
  assert.equal(first('~~~\nx\n~~~').lang, '');
});

test('代码块：没有闭合的围栏延伸到末尾', () => {
  assert.equal(first('```\n内容').text, '内容');
});

test('缩进代码块：4 个空格起始', () => {
  assert.deepEqual(parseMarkdown('    x\n    y'), [{ type: 'code', lang: '', text: 'x\ny' }]);
});

test('引用：嵌套引用与惰性续行', () => {
  const quote = first('> 引用\n> > 嵌套\n继续');
  assert.equal(quote.type, 'quote');
  assert.deepEqual(typesOf(quote.children), ['paragraph', 'quote']);
});

test('引用：内部可以含列表', () => {
  const quote = first('> - 项目');
  assert.equal(quote.children[0].type, 'list');
});

test('嵌套深度有上限：超深的引用不会无限递归', () => {
  const src = '>'.repeat(30) + ' 很深';
  const nodes = parseMarkdown(src);
  let depth = 0;
  let cur = nodes[0];
  while (cur && cur.type === 'quote') {
    depth++;
    cur = cur.children[0];
  }
  assert.ok(depth <= MAX_DEPTH, `深度 ${depth} 超过上限 ${MAX_DEPTH}`);
});

test('CRLF 换行与空输入', () => {
  assert.equal(parseMarkdown('a\r\n\r\nb').length, 2);
  assert.deepEqual(parseMarkdown(''), []);
  assert.deepEqual(parseMarkdown(null), []);
  assert.deepEqual(parseMarkdown(undefined), []);
});

test('超长输入被截断，不会卡住', () => {
  const nodes = parseMarkdown('a'.repeat(MAX_SOURCE_LENGTH + 1000));
  assert.equal(nodes[0].children[0].value.length, MAX_SOURCE_LENGTH);
});

test('综合：一份典型的 Release 说明能完整解析', () => {
  const notes = [
    '## 新功能',
    '',
    '- 新增 **作用面** 过滤（[详情](https://example.com/x)）',
    '  - 子项 `side`',
    '- [x] 配置页支持标签筛选',
    '',
    '| 项 | 状态 |',
    '|:--|--:|',
    '| 登录限速 | ✅ |',
    '',
    '```text',
    'curl -H "Host: x" http://127.0.0.1:19450/',
    '```',
    '',
    '> 注意：修改配置后需重启游戏。',
    '',
    '---',
  ].join('\n');
  const types = typesOf(parseMarkdown(notes));
  assert.deepEqual(types, ['heading', 'list', 'table', 'code', 'quote', 'hr']);
});
