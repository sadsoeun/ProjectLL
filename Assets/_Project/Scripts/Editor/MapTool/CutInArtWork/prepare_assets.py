from pathlib import Path
import shutil,uuid,re,json
from PIL import Image
work=Path(__file__).parent
assets=work/'Art';assets.mkdir(exist_ok=True)
generated=Path('C:/Users/PC/.codex/generated_images/01a19252-8ac5-7110-b2a9-856eb2b4ca42')
files={'CounterPortrait':'exec-fee0ce4b-baab-4798-8ca2-d533e5b27c09.png','NowKo':'exec-ef1cc7e9-cbd9-4b4c-bc5b-e6eaa00e5e6c.png'}
meta=Path('C:/Users/PC/UnityStudy/MyGame/Assets/_Project/Resources/HudArt/Portrait.png.meta').read_text(encoding='utf-8')
report={}
for name,source in files.items():
 target=assets/(name+'.png');shutil.copy2(generated/source,target)
 with Image.open(target) as im:
  assert im.mode=='RGBA',im.mode
  alpha=im.getchannel('A');assert alpha.getextrema()==(0,255)
  report[name]={'size':im.size,'alphaRange':alpha.getextrema(),'bounds':alpha.getbbox()}
 m=re.sub(r'^guid:.*$', 'guid: '+uuid.uuid4().hex,meta,flags=re.M)
 m=re.sub(r'maxTextureSize: \d+','maxTextureSize: 2048',m)
 m=re.sub(r'textureCompression: \d+','textureCompression: 0',m)
 (assets/(name+'.png.meta')).write_text(m,encoding='utf-8')
(work/'asset-validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report))
