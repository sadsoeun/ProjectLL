from pathlib import Path
import shutil,json
base=Path('C:/Users/PC/.codex/visualizations/2026/10/31/01a19252-8ac5-7110-b2a9-856eb2b4ca42')
copy=base/'IntroFlowProject'; work=Path(__file__).resolve().parent
for folder,name in [('Title','TitleController'),('Logo','LogoSequencer'),('LanguageSelect','LanguageSelectController'),('Title','IntroSequencePlayer'),('Title','IntroView'),('Title','TitlePresentation'),('Title','TitleIllustration')]:
 shutil.copyfile(work/'Startup'/f'{name}.cs.txt',copy/'Assets/_Project/Scripts'/folder/f'{name}.cs')
shutil.copyfile(work/'StartupFlowVerification.cs.txt',copy/'Assets/_Project/Scripts/Editor/MapTool/StartupFlowVerification.cs')
shutil.copyfile(base/'intro-film/v2/LeafAndLeapIntro_Compatible.mp4',copy/'Assets/StreamingAssets/Video/LeafAndLeapIntro.mp4')
# Resolve cached packages locally; the verification run must not fetch or update packages.
manifest=copy/'Packages/manifest.json'; data=json.loads(Path('C:/Users/PC/UnityStudy/MyGame/Packages/manifest.json').read_text())
cache=copy/'LocalPackages'
for directory in cache.iterdir():
 p=directory/'package.json'
 if p.exists():
  info=json.loads(p.read_text(encoding='utf-8-sig'));name=info.get('name')
  if name and not name.startswith('com.unity.modules.'): data['dependencies'][name]='file:'+directory.as_posix()
manifest.write_text(json.dumps(data,indent=2))
print(copy)
