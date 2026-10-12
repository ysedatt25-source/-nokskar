import type {Plugin} from 'vite';
import {normalizeCss} from './scripts/cascade-css.mjs';
export function cascadeStyles():Plugin {
 return {name:'site-cascade',enforce:'post',generateBundle(_,bundle){
  for(const asset of Object.values(bundle))if(asset.type==='asset'&&asset.fileName.endsWith('.css'))asset.source=normalizeCss(String(asset.source));
 }};
}
