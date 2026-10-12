import {cascadeStyles} from './cascade-styles';
import {scrollLockStyles} from './scroll-lock-styles';
import {defineConfig} from 'vite';import react from '@vitejs/plugin-react';import {fileURLToPath,URL} from 'node:url';export default defineConfig({plugins:[scrollLockStyles(),react(),cascadeStyles()],optimizeDeps:{exclude:['react-remove-scroll-bar']},resolve:{alias:{'@':fileURLToPath(new URL('./src',import.meta.url))}},build:{outDir:'../wwwroot',emptyOutDir:true}});
