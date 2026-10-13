import {technicalDefaults,technicalFields,technicalGroupVisible,technicalLines} from '../src/lib/technical-label';
import {uploadFile} from '../src/lib/upload-file';
const form=document.querySelector<HTMLFormElement>('#warranty-admin-form');
if(form){
 const field=(key:string)=>form.elements.namedItem('tl_'+key) as HTMLInputElement;
 const read=()=>technicalDefaults(Object.fromEntries([['mode',field('mode').value],['ceMark',field('ceMark').checked],['ceDocument',field('ceDocument').value],...technicalFields.map(([k])=>[k,field(k).value])]));
 const preview=document.getElementById('label-live-preview')!;
 const refresh=()=>{for(const el of form.querySelectorAll<HTMLElement>('[data-technical-group]'))el.hidden=!technicalGroupVisible(el.dataset.technicalGroup!,field('mode').value);preview.replaceChildren();const title=document.createElement('strong');title.textContent='Etikette görünecek bilgiler';preview.append(title);const grid=document.createElement('div');grid.className='technical-label-fields';for(const line of technicalLines(read())){const span=document.createElement('span');span.textContent=line;grid.append(span);}preview.append(grid);if(field('ceMark').checked){const mark=document.createElement('img');mark.src='/ce-mark.svg';mark.alt='CE';mark.width=40;preview.append(mark);}const status=document.getElementById('label-ce-status')!;status.replaceChildren();if(field('ceDocument').value){const a=document.createElement('a');a.href=field('ceDocument').value;a.target='_blank';a.rel='noreferrer';a.textContent='CE belgesini görüntüle';status.append(a);}};
 const write=(raw:any)=>{const d=technicalDefaults(raw);for(const [k,v] of Object.entries(d)){const el=field(k);if(k==='ceMark')el.checked=!!v;else el.value=String(v);}refresh();};
 form.addEventListener('input',e=>{if((e.target as HTMLInputElement).name?.startsWith('tl_'))refresh();});
 form.addEventListener('reset',()=>setTimeout(()=>write({}),0));
 const input=document.getElementById('label-ce-file') as HTMLInputElement;
 input.addEventListener('change',async()=>{const file=input.files?.[0];input.value='';if(!file)return;const status=document.getElementById('label-ce-status')!;const save=form.querySelector<HTMLButtonElement>('button[type=submit]')!;save.disabled=true;input.disabled=true;
  try{if(file.size>20*1048576)throw Error('CE belgesi en fazla 20 MB olabilir.');const signal=new AbortController();field('ceDocument').value=await uploadFile(file,'ce',signal.signal,p=>status.textContent='CE belgesi yükleniyor: %'+p);refresh();}
  catch(e){status.textContent=e instanceof Error?e.message:'CE belgesi yüklenemedi.';}
  finally{save.disabled=false;input.disabled=false;}
 });
 document.getElementById('label-ce-remove')!.addEventListener('click',()=>{field('ceDocument').value='';refresh();});
 (window as any).TechnicalLabelEditor={read,write,refresh};refresh();
}
