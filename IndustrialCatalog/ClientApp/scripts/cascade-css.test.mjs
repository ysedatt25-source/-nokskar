import assert from 'node:assert/strict';
import test from 'node:test';
import postcss from 'postcss';
import {normalizeCss} from './cascade-css.mjs';

function values(css,property){
 const result=[];
 postcss.parse(normalizeCss(css)).walkDecls(property,decl=>result.push(decl.value));
 return result;
}
test('preserves browser fallback values',()=>{
 assert.deepEqual(values('.a{height:100vh;height:100dvh}','height'),['100vh','100dvh']);
 assert.deepEqual(values('.a{display:block;display:grid}','display'),['block','grid']);
});
test('preserves longhand declarations separated by a shorthand reset',()=>{
 assert.deepEqual(values('.a{margin-left:2px;margin:0;margin-left:2px}','margin-left'),['2px','2px']);
});
test('removes adjacent exact duplicates',()=>{
 assert.deepEqual(values('.a{width:2px;width:2px}','width'),['2px']);
});
test('keeps distinct responsive and layer contexts',()=>{
 assert.deepEqual(values('@media(max-width:600px){.a{width:2px}}@media(min-width:601px){.a{width:3px}}','width'),['2px','3px']);
 assert.deepEqual(values('@layer site{.a{width:2px}}@layer layout{.a{width:3px}}','width'),['2px','3px']);
});

test('rejects forced declaration priority before creating a bundle',()=>{
 const forced='!'+ 'important';
 assert.throws(()=>normalizeCss('.example{color:red '+forced+'}'),/Forced CSS priority is prohibited/);
});
