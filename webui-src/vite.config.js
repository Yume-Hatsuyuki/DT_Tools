import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';
import Icons from 'unplugin-icons/vite';

// 构建产物直接进 ../DT_Tools/WebUI（BepInEx StaticFiles 白名单服务的目录），
// 不带 hash 文件名——StaticFiles 已对所有静态资源发 Cache-Control: no-cache，
// 插件升级后浏览器强制取新文件，不需要靠 hash 破缓存。
export default defineConfig({
  plugins: [
    vue(),
    Icons({
      compiler: 'vue3',
      defaultClass: 'icon',
    }),
  ],
  // 绝对路径：WebConsole 只从站点根 / 提供服务，跟旧版 index.html/login.html
  // 用 /favicon/... /js/... 的绝对路径写法保持一致。
  base: '/',
  build: {
    outDir: '../DT_Tools/WebUI',
    emptyOutDir: true,
    assetsDir: 'assets',
    rollupOptions: {
      output: {
        entryFileNames: 'js/app.js',
        chunkFileNames: 'js/[name].js',
        assetFileNames: (info) => {
          const name = info.name || '';
          if (name.endsWith('.css')) return 'css/app.css';
          return 'assets/[name][extname]';
        },
      },
    },
  },
});
