import fs from 'node:fs';
import {notes} from './detailed-deck-planning.mjs';
export const script=JSON.parse(fs.readFileSync(new URL('./presentation-script.json',import.meta.url),'utf8'));
export function formatNote(item){
 const n=notes[item.slide-1];
 return `[예상 ${n[2]}초 · 리허설로 조정]\n핵심: ${item.key}\n\n[화면을 짚는 위치 — 읽지 않음]\n${item.cue}\n\n[말할 내용]\n${item.speech}\n\n[다음 장 연결 / 마무리]\n${item.transition}\n\n[발표자 참고 근거 — 읽지 않음]\n${n[4]}`;
}
export async function applySpeakerScript({presentation:p}){
 if(p.slides.items.length!==script.length)throw new Error('Slide count differs from script');
 const before=p.slides.items.map(s=>JSON.stringify(s.shapes.items.map(x=>x.toSnapshot())));
 const titles=p.slides.items.map(s=>s.shapes.items[0].toSnapshot().text);
 for(let i=0;i<script.length;i++)if(titles[i]!==notes[i][0])throw new Error(`Title mismatch on slide ${i+1}: ${titles[i]}`);
 for(const item of script)p.slides.getItem(item.slide-1).speakerNotes.text=formatNote(item);
 const after=p.slides.items.map(s=>JSON.stringify(s.shapes.items.map(x=>x.toSnapshot())));
 if(JSON.stringify(before)!==JSON.stringify(after))throw new Error('Unexpected visual change');
 return {notesUpdated:script.length,slideTitlesMatched:true,slideShapesUnchanged:true};
}

