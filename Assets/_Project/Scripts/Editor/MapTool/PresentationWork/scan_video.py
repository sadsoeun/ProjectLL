from pathlib import Path
import sys,subprocess,json
sys.path.insert(0,str(Path(__file__).parent/'python_deps'))
import imageio_ffmpeg
import numpy as np
out=Path(__file__).parent/'Captures';out.mkdir(exist_ok=True)
ff=imageio_ffmpeg.get_ffmpeg_exe()
src='C:/Users/PC/UnityStudy/Docs/deliverables/LeafAndLeap_Showcase.mp4'
p=subprocess.run([ff,'-v','error','-i',src,'-vf','fps=4,scale=160:90','-f','rawvideo','-pix_fmt','rgb24','-'],capture_output=True,check=True)
a=np.frombuffer(p.stdout,dtype=np.uint8).reshape(-1,90,160,3).astype(float)
scores=[]
for i,f in enumerate(a):
 region=f[30:78,30:140]
 scores.append((round(float((region.max(2)-region.min(2)).mean()),2),i/4,round(float(region.mean()),2)))
(out/'video_scan.json').write_text(json.dumps(scores),encoding='utf8')
print('ffmpeg',ff)
print('lowest chroma nonblack frames',sorted(x for x in scores if x[2]>30 and 50<x[1]<110)[:22])
for t in [65,70,75,80,85,90,95]:
 subprocess.run([ff,'-v','error','-ss',str(t),'-i',src,'-frames:v','1','-y',str(out/f'video-{t}.png')],check=True)
