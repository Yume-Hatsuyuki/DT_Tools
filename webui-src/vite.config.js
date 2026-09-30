import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';
import Icons from 'unplugin-icons/vite';

// 构建产物直接进 ../DT_Tools/WebUI（BepInEx StaticFiles 白名单服务的目录），
// 不带 hash 文件名——StaticFiles 已对所有静态资源发 Cache-Control: no-cache，
// 插件升级后浏览器强制取新文件，不需要靠 hash 破缓存。
export default defineConfig(({ mode }) => ({
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
  // 开发联调（npm run dev）：/api 代理到游戏机（默认 192.168.88.100:19450，
  // 可用环境变量 DT_API_TARGET 覆盖），本机浏览器即可调试真机后端；
  // 仅 dev server 生效，npm run build 的产物完全不受影响。
  server: mode === 'development'
    ? {
        proxy: {
          '/api': {
            target: process.env.DT_API_TARGET || 'http://192.168.88.100:19450',
            changeOrigin: true,
            ws: true,   // /api/log/ws 升级请求也要代理：真机会立刻回 501，前端据此降级轮询
            // 关键：剥掉 Origin/Referer。插件 Router 以 Origin/Referer 与 Host 判定同源，
            // 代理转发后 Host 已改写为游戏机而 Origin 仍是 localhost——写请求会被
            // 当成跨站 403（表现为 dev 里所有命令 "✕ Forbidden"）。无 Origin 的
            // 请求插件按"非浏览器客户端"放行，与 curl 直调一致。
            configure: (proxy) => {
              proxy.on('proxyReq', (proxyReq) => {
                proxyReq.removeHeader('origin');
                proxyReq.removeHeader('referer');
              });
            },
          },
        },
      }
    : undefined,
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
}));
