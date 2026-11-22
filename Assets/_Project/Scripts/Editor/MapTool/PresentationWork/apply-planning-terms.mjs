import fs from 'node:fs';
export const changes=JSON.parse(fs.readFileSync(new URL('./planning-terms-changes.json',import.meta.url),'utf8'));
export async function applyPlainLanguage({presentation:p}){
 const records=[];
 for(const {slide,replacements} of changes){
   const s=p.slides.getItem(slide-1),map=new Map(replacements),seen=new Set();
   const targets=[];
   for(const shape of s.shapes.items){const old=shape.toSnapshot().text;if(map.has(old)){targets.push([shape,old]);seen.add(old);}}
   for(const table of s.tables.items){const snap=table.toSnapshot();for(let r=0;r<snap.rows;r++)for(let c=0;c<snap.cols;c++){const cell=table.getCell(r,c),old=String(cell.text);if(map.has(old)){targets.push([cell,old]);seen.add(old);}}}
   const missing=[...map.keys()].filter(k=>!seen.has(k));
   if(missing.length)throw new Error(`Slide ${slide} missing original text: ${JSON.stringify(missing)}`);
   for(const [target,old] of targets)target.text.set(map.get(old));
   records.push({slide,replaced:targets.length});
 }
 return {slides:p.slides.items.length,changes:records};
}


