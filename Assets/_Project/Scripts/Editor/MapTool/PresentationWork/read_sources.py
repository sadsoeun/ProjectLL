from pathlib import Path
from pypdf import PdfReader
from html.parser import HTMLParser
import json
root=Path('C:/Users/PC/UnityStudy/Docs');out=Path(__file__).parent/'sources';out.mkdir(exist_ok=True)
for rel in ['deliverables/Leaf_and_Leap_프로젝트_소개.pdf','level-design-report.pdf','map-tool-design.pdf','SceneStructure_SceneFlow.pdf']:
 r=PdfReader(root/rel);txt='\n'.join(f'\nPAGE {i+1}\n'+(p.extract_text() or '') for i,p in enumerate(r.pages))
 (out/(Path(rel).stem+'.txt')).write_text(txt,encoding='utf-8');print(rel,len(r.pages),'pages',len(txt),'chars')
class Text(HTMLParser):
 def __init__(self):super().__init__();self.result=[];self.skip=False
 def handle_starttag(self,t,a):
  if t in ['style','script']:self.skip=True
  if t in ['p','div','section','article','h1','h2','h3','tr','li','br','pre']:self.result.append('\n')
 def handle_endtag(self,t):
  if t in ['style','script']:self.skip=False
 def handle_data(self,d):
  if not self.skip:self.result.append(d)
for p in (root/'chat-history').glob('*.html'):
 t=Text();t.feed(p.read_text(encoding='utf-8-sig'));txt=''.join(t.result);(out/(p.stem+'.txt')).write_text(txt,encoding='utf-8');print(p.name,len(txt),'chars')
