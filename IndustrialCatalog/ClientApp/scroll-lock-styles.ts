import type {Plugin} from 'vite';
// Preserve the scroll-lock rules while removing forced declaration priority.
export function scrollLockStyles():Plugin {
 return {name:'scroll-lock-styles',enforce:'pre',transform(code,id){
  if(!id.includes('react-remove-scroll-bar/')||!id.includes('/component.'))return null;
  return {code:code.replace(/!\s*important/gi,''),map:null};
 }};
}
