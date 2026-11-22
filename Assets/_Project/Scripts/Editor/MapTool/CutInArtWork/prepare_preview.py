from pathlib import Path
import shutil,json
root=Path('C:/Users/PC/UnityStudy/MyGame')
work=Path(__file__).resolve().parent
preview=Path('C:/Users/PC/.codex/visualizations/2026/10/31/01a19252-8ac5-7110-b2a9-856eb2b4ca42/CutInPreview')
for d in ['Assets/Runtime','Assets/Editor','Assets/_Project/Resources/CutInArt','Assets/Resources/Fonts','Assets/_Project/Scripts/Editor/MapTool/PresentationWork/Captures','Packages','ProjectSettings']:(preview/d).mkdir(parents=True,exist_ok=True)
for src,dst in [
 ('Assets/_Project/Scripts/InGame/FX/NowCutIn.cs','Assets/Runtime/NowCutIn.cs'),
 ('Assets/_Project/Scripts/Services/FontProvider.cs','Assets/Runtime/FontProvider.cs'),
 ('Assets/_Project/Scripts/Services/LocalizationTable.cs','Assets/Runtime/LocalizationTable.cs'),
 ('Assets/_Project/Scripts/UI/UiTextKeys.cs','Assets/Runtime/UiTextKeys.cs'),
 ('Assets/_Project/Scripts/Editor/MapTool/NowCutInVerification.cs','Assets/Editor/NowCutInVerification.cs'),
 ('Assets/_Project/Scripts/Editor/MapTool/PresentationWork/Captures/dodge-71.9.png','Assets/_Project/Scripts/Editor/MapTool/PresentationWork/Captures/dodge-71.9.png')]:shutil.copy2(root/src,preview/dst)
for p in (work/'Art').iterdir():
 if p.is_file() and (p.suffix=='.png' or p.name.endswith('.png.meta')):shutil.copy2(p,preview/'Assets/_Project/Resources/CutInArt'/p.name)
for p in (root/'Assets/_Project/Resources/Fonts').iterdir():
 if p.is_file() and p.suffix.lower() in ['.ttf','.otf','.meta','.txt']:shutil.copy2(p,preview/'Assets/Resources/Fonts'/p.name)
(preview/'Assets/Runtime/PreviewServices.cs').write_text('''using UnityEngine;
namespace MyGame {
 // Isolated UI preview only. Full game integration is compiled separately against actual services.
 public static class TimeStop { public static void Cancel() {} }
 public enum SfxId { CutInNow }
 public static class Sfx { public static void Play(SfxId id) {} }
 public sealed class PreviewLocalization { public string CurrentLanguage => "ko"; }
 public sealed class GameSystems { public static bool Exists => false; public static GameSystems Instance => null; public PreviewLocalization Localization => null; }
 public static class GoldUiTheme { public static readonly Color Cream = new Color(1f,.94f,.78f); }
}
''',encoding='utf-8')
(preview/'Packages/manifest.json').write_text(json.dumps({'dependencies':{'com.unity.ugui':'file:'+str(root/'Library/PackageCache/com.unity.ugui@b9d1dd437cb9').replace('\\','/'),'com.unity.modules.ui':'1.0.0','com.unity.modules.imgui':'1.0.0','com.unity.modules.imageconversion':'1.0.0'}},indent=2),encoding='utf-8')
shutil.copy2(root/'ProjectSettings/ProjectVersion.txt',preview/'ProjectSettings/ProjectVersion.txt')
print(preview)
