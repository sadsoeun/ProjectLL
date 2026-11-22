from pathlib import Path
import re,subprocess
root=Path('C:/Users/PC/UnityStudy/MyGame')
out=Path('C:/Users/PC/.codex/visualizations/2026/10/31/01a19252-8ac5-7110-b2a9-856eb2b4ca42/textfit-compile');out.mkdir(exist_ok=True)
editor=Path('C:/Program Files/Unity/Hub/Editor/6000.3.6f1/Editor/Data')
built=[]
for assembly in ['UnityChan.Editor','Assembly-CSharp','Assembly-CSharp-Editor']:
 text=(root/'Library/Bee/artifacts/1900b0aE.dag'/f'{assembly}.rsp').read_text(encoding='utf-8-sig')
 text=re.sub(r'^-out:.*$',lambda _:f'-out:"{out}/{assembly}.dll"',text,flags=re.M)
 text=re.sub(r'^-refout:.*$',lambda _:f'-refout:"{out}/{assembly}.ref.dll"',text,flags=re.M)
 # Ignore deleted transient outputs still recorded by an open Editor's stale response file.
 text='\n'.join(line for line in text.splitlines() if not (line.startswith('-r:') and 'CutInArtWork/Compile/' in line))
 for dependency in built:
  text=text.replace(f'-r:"Library/Bee/artifacts/1900b0aE.dag/{dependency}.ref.dll"',f'-r:"{out}/{dependency}.ref.dll"')
 if assembly=='Assembly-CSharp':
  source='"Assets/_Project/Scripts/UI/LocalizedTextFit.cs"'
  if source not in text:text+='\n'+source+'\n'
 if assembly=='Assembly-CSharp-Editor':
  text=text.replace('-r:"Library/Bee/artifacts/1900b0aE.dag/Assembly-CSharp.ref.dll"',f'-r:"{out}/Assembly-CSharp.ref.dll"')
  source='"Assets/_Project/Scripts/Editor/MapTool/TextFitVerification.cs"'
  if source not in text:text+='\n'+source+'\n'
 rsp=out/(assembly+'.rsp');rsp.write_text(text,encoding='utf-8')
 result=subprocess.run([str(editor/'NetCoreRuntime/dotnet.exe'),str(editor/'DotNetSdkRoslyn/csc.dll'),'@'+str(rsp)],cwd=root,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,encoding='utf-8',errors='replace')
 (out/(assembly+'.log')).write_text(result.stdout,encoding='utf-8')
 print(assembly,'exit',result.returncode)
 print('\n'.join(x for x in result.stdout.splitlines() if 'error ' in x)[:4000])
 if result.returncode:raise SystemExit(result.returncode)
 built.append(assembly)
