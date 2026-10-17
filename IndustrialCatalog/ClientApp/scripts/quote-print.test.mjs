import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import vm from 'node:vm';
const source=readFileSync(new URL('../public/quote-document.js',import.meta.url),'utf8');
const printSource=source.slice(source.indexOf('async function printDocument(){'),source.indexOf('if(window.parent!==window)'));
function setup({blocked=false,fail=false}={}){
 const opened=[],revoked=[],buttons=[{disabled:false},{disabled:false}],status={textContent:'',replaceChildren(...children){this.children=children;}},file={name:'QA.pdf',type:'application/pdf'},viewer={closed:false,document:{body:{}},location:{replace(url){opened.push(url);}},focus(){},close(){this.closed=true;}};
 let generated=0,openBeforeGeneration=false,created;
 const context={busy:false,document:{querySelectorAll:()=>buttons},$:()=>status,el:(tag,cls,text)=>({tag,textContent:text}),window:{open(){openBeforeGeneration=generated===0;return blocked?null:viewer;}},exportFile:async()=>{generated++;if(fail)throw Error('PDF hazırlanamadı');return file;},URL:{createObjectURL(value){created=value;return 'blob:qa-pdf';},revokeObjectURL(url){revoked.push(url);}},setTimeout:()=>{}};
 vm.createContext(context);vm.runInContext(printSource,context);
 return{context,opened,revoked,buttons,status,viewer,file,get generated(){return generated;},get created(){return created;},get openBeforeGeneration(){return openBeforeGeneration;}};
}
const normal=setup();await normal.context.printDocument();
assert.equal(normal.created,normal.file,'Printing must use the same exported PDF file, without HTML pagination');
assert.deepEqual(normal.opened,['blob:qa-pdf']);assert.equal(normal.openBeforeGeneration,true,'Open viewer during the user gesture');
assert.equal(normal.viewer.opener,null);assert.ok(normal.buttons.every(b=>!b.disabled));assert.equal(normal.context.busy,false);
const blocked=setup({blocked:true});await blocked.context.printDocument();const retry=blocked.status.children[1];
assert.equal(retry.href,'blob:qa-pdf');assert.equal(retry.target,'_blank');assert.equal(retry.rel,'noopener');assert.equal('download' in retry,false,'Blocked popup must offer a viewer link, not force a download');
const failed=setup({fail:true});await failed.context.printDocument();assert.equal(failed.viewer.closed,true);assert.equal(failed.status.textContent,'PDF hazırlanamadı');assert.ok(failed.buttons.every(b=>!b.disabled));assert.equal(failed.context.busy,false);
const busy=setup();busy.context.busy=true;await busy.context.printDocument();assert.equal(busy.generated,0);
console.log('Quote print: PASS (same PDF file, popup fallback, no forced download, failure recovery, duplicate prevention).');
