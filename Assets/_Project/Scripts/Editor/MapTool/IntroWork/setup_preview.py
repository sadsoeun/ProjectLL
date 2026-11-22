from pathlib import Path
import shutil,json
base=Path('C:/Users/PC/.codex/visualizations/2026/10/31/01a19252-8ac5-7110-b2a9-856eb2b4ca42')
project=base/'IntroPreview';game=Path('C:/Users/PC/UnityStudy/MyGame');work=Path(__file__).resolve().parent
for d in ['Assets/Runtime','Assets/Editor','Assets/Resources/Fonts','Assets/StreamingAssets/Video','Packages','ProjectSettings','Verification']:(project/d).mkdir(parents=True,exist_ok=True)
for name in ['IntroStory','IntroView','IntroSequencePlayer','LocalizationTable']:
 shutil.copyfile(work/'Staged'/f'{name}.cs.txt',project/'Assets/Runtime'/f'{name}.cs')
for folder,name in [('Services','FontProvider'),('UI','LocalizedTextFit'),('UI','UiTextKeys')]:
 shutil.copyfile(game/'Assets/_Project/Scripts'/folder/f'{name}.cs',project/'Assets/Runtime'/f'{name}.cs')
for p in (game/'Assets/_Project/Resources/Fonts').glob('*.otf'):shutil.copyfile(p,project/'Assets/Resources/Fonts'/p.name)
shutil.copyfile(base/'intro-film/v2/LeafAndLeapIntro.mp4',project/'Assets/StreamingAssets/Video/LeafAndLeapIntro.mp4')
shutil.copyfile(base/'intro-film/v2/poster.jpg',project/'Assets/Backdrop.jpg')
shutil.copyfile(game/'ProjectSettings/ProjectVersion.txt',project/'ProjectSettings/ProjectVersion.txt')
packages={'com.unity.ugui':'file:C:/Users/PC/UnityStudy/MyGame/Library/PackageCache/com.unity.ugui@b9d1dd437cb9'}
for module in ['ui','imgui','imageconversion','audio','video']:packages['com.unity.modules.'+module]='1.0.0'
(project/'Packages/manifest.json').write_text(json.dumps({'dependencies':packages},indent=2))
shutil.copyfile(work/'PreviewServices.cs.txt',project/'Assets/Runtime/PreviewServices.cs')
shutil.copyfile(work/'IntroVerification.cs.txt',project/'Assets/Editor/IntroVerification.cs')
print(project)
