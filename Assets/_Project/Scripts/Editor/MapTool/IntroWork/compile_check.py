from pathlib import Path
import re,subprocess,sys
root=Path('C:/Users/PC/UnityStudy/MyGame')
work=Path(__file__).resolve().parent
out=Path('C:/Users/PC/.codex/visualizations/2026/10/31/01a19252-8ac5-7110-b2a9-856eb2b4ca42/intro-compile');out.mkdir(exist_ok=True)
editor=Path('C:/Program Files/Unity/Hub/Editor/6000.3.6f1/Editor/Data')
built=[]
for assembly in ['UnityChan.Editor','Assembly-CSharp','Assembly-CSharp-Editor']:
 text=(root/'Library/Bee/artifacts/1900b0aE.dag'/f'{assembly}.rsp').read_text(encoding='utf-8-sig')
 text=re.sub(r'^-out:.*$',lambda _:f'-out:"{out}/{assembly}.dll"',text,flags=re.M)
 text=re.sub(r'^-refout:.*$',lambda _:f'-refout:"{out}/{assembly}.ref.dll"',text,flags=re.M)
 text='\n'.join(line for line in text.splitlines() if not (line.startswith('-r:') and 'CutInArtWork/Compile/' in line))
 for dependency in built:text=text.replace(f'-r:"Library/Bee/artifacts/1900b0aE.dag/{dependency}.ref.dll"',f'-r:"{out}/{dependency}.ref.dll"')
 if assembly=='Assembly-CSharp':
  for sub,name in [('Title','TitleController'),('Services','LocalizationTable')]:
   text=text.replace(f'"Assets/_Project/Scripts/{sub}/{name}.cs"',f'"{work}/Staged/{name}.cs.txt"')
  for name in ['IntroStory','IntroView','IntroSequencePlayer']:
   text=re.sub(r'^"Assets/_Project/Scripts/Title/'+name+r'\.cs"\s*$', '',text,flags=re.M)
   text+=f'\n"{work}/Staged/{name}.cs.txt"\n'
  source='"Assets/_Project/Scripts/UI/LocalizedTextFit.cs"'
  if source not in text:text+='\n'+source+'\n'
  if '--startup' in sys.argv:
   for folder,name in [('Title','TitleController'),('Title','IntroSequencePlayer'),('Title','IntroView'),('Title','TitlePresentation'),('Title','TitleIllustration'),('Logo','LogoSequencer'),('LanguageSelect','LanguageSelectController')]:
    text=text.replace(f'"{work}/Staged/{name}.cs.txt"',f'"{work}/Startup/{name}.cs.txt"')
    text=text.replace(f'"Assets/_Project/Scripts/{folder}/{name}.cs"',f'"{work}/Startup/{name}.cs.txt"')
 rsp=out/(assembly+'.rsp');rsp.write_text(text,encoding='utf-8')
 result=subprocess.run([str(editor/'NetCoreRuntime/dotnet.exe'),str(editor/'DotNetSdkRoslyn/csc.dll'),'@'+str(rsp)],cwd=root,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,encoding='utf-8',errors='replace')
 (out/(assembly+'.log')).write_text(result.stdout,encoding='utf-8')
 print(assembly,'exit',result.returncode)
 print('\n'.join(x for x in result.stdout.splitlines() if 'error ' in x)[:4000])
 if result.returncode:raise SystemExit(result.returncode)
 built.append(assembly)
