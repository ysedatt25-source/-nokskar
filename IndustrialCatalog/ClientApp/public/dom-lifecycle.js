/* One DOM subscription hub. Each owner receives only its requested records. */
(()=>{
 'use strict';if(window.InokskarDOM)return;
 const subscriptions=new Set();const NativeObserver=window.MutationObserver;
 class Observer{
  constructor(callback){this.callback=callback;this.targets=new Map();this.records=[];}
  observe(target,options){if(!(target instanceof Node))throw new TypeError('A DOM node is required.');const settings={...options};if(settings.attributeFilter||settings.attributeOldValue)settings.attributes??=true;if(settings.characterDataOldValue)settings.characterData??=true;if(!settings.childList&&!settings.attributes&&!settings.characterData)throw new TypeError('A mutation type is required.');this.targets.set(target,settings);subscriptions.add(this);}
  disconnect(){this.targets.clear();this.records=[];subscriptions.delete(this);}
  takeRecords(){const records=this.records;this.records=[];return records;}
 }
 const observer=new NativeObserver(records=>{
  for(const owner of [...subscriptions]){
   owner.records.push(...records.filter(record=>[...owner.targets].some(([target,options])=>{
    if(record.target!==target&&!(options.subtree&&target.contains(record.target)))return false;
    if(record.type==='childList')return !!options.childList;
    if(record.type==='characterData')return !!options.characterData;
    return !!options.attributes&&(!options.attributeFilter||options.attributeFilter.includes(record.attributeName));
   })));
   const selected=owner.takeRecords();if(selected.length)try{owner.callback(selected,owner);}catch(error){queueMicrotask(()=>{throw error;});}
  }
 });
 observer.observe(document.documentElement,{childList:true,subtree:true,attributes:true,characterData:true,attributeOldValue:true,characterDataOldValue:true});
 window.InokskarDOM=Object.freeze({Observer});
})();
