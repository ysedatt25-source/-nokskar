import {scrollLockStyles} from './scroll-lock-styles';
import {defineConfig} from 'vite';
import react from '@vitejs/plugin-react';
import {fileURLToPath,URL} from 'node:url';
export default defineConfig({
 plugins:[scrollLockStyles(),react()],
 optimizeDeps:{exclude:['react-remove-scroll-bar']},
 resolve:{alias:{'@':fileURLToPath(new URL('./src',import.meta.url))}},
 build:{outDir:'../wwwroot',emptyOutDir:true,chunkSizeWarningLimit:450,rollupOptions:{output:{manualChunks(id){
   if(!id.includes('node_modules'))return;
   if(id.includes('lucide-react'))return 'icons';
   if(id.includes('@radix-ui')||id.includes('radix-ui'))return 'radix';
   if(id.includes('/react/')||id.includes('/react-dom/')||id.includes('scheduler'))return 'react-vendor';
   return 'vendor';
 }}}}
});
