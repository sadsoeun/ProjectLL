from pathlib import Path
import re,subprocess
root=Path('C:/Users/PC/UnityStudy/MyGame');work=Path(__file__).resolve().parent
out=Path('C:/Users/PC/.codex/visualizations/2026/10/31/01a19252-8ac5-7110-b2a9-856eb2b4ca42/intro-compile');out.mkdir(exist_ok=True)
editor=Path('C:/Program Files/Unity/Hub/Editor/6000.3.6f1/Editor/Data');built=[]
for assembly in ['UnityChan.Editor','Assembly-CSharp','Assembly-CSharp-Editor']:
 text=(root/'Library/Bee/artifacts/1900b0aE.dag'/f'{assembly}.rsp').read_text(encoding='utf-8-sig')
 text=re.sub(r'^-out:.*$',lambda _:f'-out:"{out}/{assembly}.dll"',text,flags=re.M)
 text=re.sub(r'^-refout:.*$',lambda _:f'-refout:"{out}/{assembly}.ref.dll"',text,flags=re.M)
 for dependency in built:text=text.replace(f'-r:"Library/Bee/artifacts/1900b0aE.dag/{dependency}.ref.dll"',f'-r:"{out}/{dependency}.ref.dll"')
 if assembly=='Assembly-CSharp':
  for name in ['IntroStory','IntroView','IntroSequencePlayer','TitleController','TitlePresentation','TitleIllustration','IntroTitleTransition']:
   source=work/'Handoff'/f'{name}.cs.txt'
   if not source.exists():source=root/'Assets/_Project/Scripts/Title'/f'{name}.cs'
   text=re.sub(r'^"Assets/_Project/Scripts/Title/'+name+r'\.cs"\s*$', '',text,flags=re.M)
   text+=f'\n"{source}"\n'
 rsp=out/(assembly+'.rsp');rsp.write_text(text,encoding='utf-8')
 p=subprocess.run([str(editor/'NetCoreRuntime/dotnet.exe'),str(editor/'DotNetSdkRoslyn/csc.dll'),'@'+str(rsp)],cwd=root,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,encoding='utf-8',errors='replace')
 (out/(assembly+'.log')).write_text(p.stdout,encoding='utf-8');print(assembly,'exit',p.returncode)
 print('\n'.join(x for x in p.stdout.splitlines() if 'error ' in x)[:4000])
 if p.returncode:raise SystemExit(p.returncode)
 built.append(assembly)
