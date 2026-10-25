import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';
import Icons from 'unplugin-icons/vite';

export default defineConfig(({ mode }) => ({
  plugins: [
    vue(),
    Icons({
      compiler: 'vue3',
      defaultClass: 'icon',
    }),
  ],
  base: '/',
  server: mode === 'development'
    ? {
        proxy: {
          '/api': {
            target: process.env.DT_API_TARGET || 'http://192.168.88.100:19450',
            changeOrigin: true,
            ws: true,
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
