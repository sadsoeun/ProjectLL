import fs from 'node:fs/promises';
import {notes} from './detailed-deck.mjs';
import {script} from './speaker-script.mjs';
const escape=s=>s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
const seconds=notes.reduce((sum,n)=>sum+n[2],0);
const time=`${Math.floor(seconds/60)}분 ${seconds%60}초`;
if(script.length!==22||script.some((s,i)=>s.slide!==i+1))throw Error('Missing or unordered script');
const intro=`LEAF & LEAP — 발표용 구어체 대본\n22장 슬라이드 순서 기준 · 2026-11-14\n발표 배분 초안: 약 ${time}, 시연 영상 2분 03초 별도. 실제 시간은 리허설로 조정하세요.\n\n사용 방법\n‘말할 내용’과 ‘연결 멘트’가 실제 발표 문장입니다. 화면 안내와 핵심 문장은 준비용입니다.\n문장을 모두 외우기보다 각 장의 핵심을 기억하고, 표의 항목과 숫자는 필요한 것만 짚으세요.\n18장은 Astra와 진행한 UI 작업, 20장은 Astra·Claude·MCP의 역할을 구분합니다.\n영상은 22장에서 재생하고, ‘영상 후’ 문장은 재생이 끝난 뒤 말합니다.\n`;
const text=intro+script.map(s=>`\n${'='.repeat(62)}\n${String(s.slide).padStart(2,'0')}. ${notes[s.slide-1][0]}\n예상 ${notes[s.slide-1][2]}초\n\n[핵심]\n${s.key}\n\n[화면 안내 — 읽지 않음]\n${s.cue}\n\n[말할 내용]\n${s.speech}\n\n[연결 멘트${s.slide===22?' / 영상 후 마무리':''}]\n${s.transition}\n`).join('');
const paragraphs=s=>s.split('\n\n').map(p=>`<p>${escape(p)}</p>`).join('');
const html=`<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>LEAF & LEAP 발표 대본</title>
<style>*{box-sizing:border-box}html{scroll-behavior:smooth}body{margin:0;background:#101722;color:#f5ead2;font:19px/1.85 'Noto Sans KR','Noto Sans CJK KR','Malgun Gothic',sans-serif}main{max-width:960px;margin:auto;padding:46px 32px 100px}h1{font-size:36px;line-height:1.4}h2{font-size:28px;line-height:1.5;margin:0 0 12px}h3{font-size:18px;color:#deb66c;margin:24px 0 6px}p{margin:0 0 20px}a{color:#deb66c;text-decoration:none}header{border-bottom:1px solid #3c4858;padding-bottom:25px}.help,.cue,.timing{color:#b4c0cf;font-size:16px}.key{color:#deb66c;font-size:20px;font-weight:700}.toc{display:grid;grid-template-columns:1fr 1fr;gap:5px 25px;padding:24px 0;font-size:15px}section{padding:45px 0;border-top:1px solid #3c4858;scroll-margin-top:20px}.number{color:#deb66c}.transition{border-left:3px solid #deb66c;padding-left:18px}.cue{margin:14px 0 28px}footer{font-size:15px;color:#b4c0cf}@media(max-width:650px){main{padding:28px 20px}h1{font-size:28px}h2{font-size:24px}.toc{grid-template-columns:1fr}}@media print{body{background:#fff;color:#111;font-size:12pt;line-height:1.7}main{max-width:none;padding:0}.toc{display:none}h1{font-size:24pt}h2{font-size:19pt}section{break-before:page;border:0;padding-top:0}.help,.cue,.timing,footer{color:#555}.key,.number,h3,a{color:#665122}.transition{border-color:#665122}p{orphans:3;widows:3}}
</style><main><header><h1>LEAF &amp; LEAP<br>발표용 구어체 대본</h1><p class="key">설계 의도 → AI와 구현한 방식 → 결과를 보고 판단한 점</p><p class="help">22장 순서에 맞춘 대본입니다. 발표 배분 초안은 약 ${time}, 영상은 별도 2분 03초입니다. 실제 시간은 리허설로 조정하세요.</p><p class="help">화면 안내는 읽지 않습니다. 본문과 연결 멘트를 말하되, 각 장의 핵심을 중심으로 자연스럽게 풀어 설명하세요.</p></header><nav class="toc" aria-label="슬라이드 목차">${script.map(s=>`<a href="#slide-${s.slide}">${String(s.slide).padStart(2,'0')}. ${escape(notes[s.slide-1][0])}</a>`).join('')}</nav>${script.map(s=>`<section id="slide-${s.slide}"><h2><span class="number">${String(s.slide).padStart(2,'0')}.</span> ${escape(notes[s.slide-1][0])}</h2><div class="timing">예상 ${notes[s.slide-1][2]}초 · 리허설로 조정</div><p class="key">${escape(s.key)}</p><div class="cue">화면 안내 · ${escape(s.cue)}</div>${paragraphs(s.speech)}<h3>${s.slide===22?'영상 후 마무리':'다음 장으로 연결'}</h3><div class="transition">${escape(s.transition)}</div></section>`).join('')}<footer>최종 슬라이드 순서와 대조 · 2026-11-14 · 확인된 구현과 설계 의도를 구분했습니다.</footer></main></html>`;
await fs.writeFile(new URL('./LEAF_AND_LEAP_발표용_구어체대본.txt',import.meta.url),'\ufeff'+text,'utf8');
await fs.writeFile(new URL('./LEAF_AND_LEAP_발표용_구어체대본.html',import.meta.url),html,'utf8');
// Keep the source deck notes and existing companion report consistent with this script.
for(const item of script)notes[item.slide-1][3]=item.speech+'\n\n'+item.transition;
const path=new URL('./detailed-deck.mjs',import.meta.url);
let moduleText=await fs.readFile(path,'utf8');
const start=moduleText.indexOf('export const notes=');
const end=moduleText.indexOf('\nfunction text(',start);
if(start<0||end<0)throw Error('Cannot locate deck notes');
moduleText=moduleText.slice(0,start)+'export const notes='+JSON.stringify(notes,null,2)+';\n'+moduleText.slice(end);
await fs.writeFile(path,moduleText,'utf8');
console.log(JSON.stringify({slides:script.length,estimatedSeconds:seconds,spokenCharacters:script.reduce((sum,s)=>sum+s.speech.length+s.transition.length,0),outputs:['LEAF_AND_LEAP_발표용_구어체대본.html','LEAF_AND_LEAP_발표용_구어체대본.txt']}));
