from pathlib import Path
import shutil

work=Path(__file__).resolve().parent
project=work.parents[5]
assert (project/'Assets/_Project').is_dir(), project
assets=project/'Assets/_Project'
backup=work/'Backup'
backup.mkdir(exist_ok=True)
def preserve(path):
    dst=backup/('Title-'+path.name+'.txt')
    if path.exists() and not dst.exists(): shutil.copy2(path,dst)

title=assets/'Scripts/Title/TitleIllustration.cs'
preserve(title)
text=title.read_text(encoding='utf-8-sig')
text=text.replace('Static title artwork with aspect-preserving responsive layout.', 'Cel title artwork and Cubism character in an aspect-preserving design space.')
if 'TitleLive2DCharacter' not in text:
    text=text.replace('art.texture = texture; art.raycastTarget = false;', '''art.texture = texture; art.raycastTarget = false;
            var cleanBackground = Resources.Load<Texture2D>("TitleArt/MeadowBackground");
            if (cleanBackground != null)
            {
                var live = gameObject.AddComponent<TitleLive2DCharacter>();
                if (live.Initialize(rt)) art.texture = cleanBackground;
            }''')
title.write_text(text,encoding='utf-8')
for name in ['TitleLive2DCharacter.cs']:
    shutil.copy2(work/'Staged'/(name+'.txt'),assets/'Scripts/Title'/name)
art=assets/'Resources/TitleArt'
preserve(art/'Meadow.png')
shutil.copy2(work/'Art/MeadowCel.png',art/'Meadow.png')
shutil.copy2(work/'Live2D/Source/Background.png',art/'MeadowBackground.png')
shutil.copy2(work/'Staged/TitlePremultiplied.shader.txt',art/'TitlePremultiplied.shader')
dst=assets/'Resources/TitleLive2D'
dst.mkdir(exist_ok=True)
for src in (work/'Live2D/Runtime').rglob('*'):
    if src.is_file() and src.suffix in ['.moc3','.json','.png']:
        target=dst/src.relative_to(work/'Live2D/Runtime')
        target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copy2(src,target)

# Rebind existing generated elements after scene serialization/domain reload.
hud=assets/'Scripts/UI/HudPresentation.cs'
preserve(hud)
s=hud.read_text(encoding='utf-8-sig')
s=s.replace('GoldUiTheme.Rect("Clock",timer)','ExistingRect("Clock",timer)').replace('GoldUiTheme.Rect("HudPauseButton",transform)','ExistingRect("HudPauseButton",transform)').replace('GoldUiTheme.Rect("PauseGlyph",pause)','ExistingRect("PauseGlyph",pause)')
s=s.replace('GoldUiTheme.Rect(name,transform)','ExistingRect(name,transform)').replace('GoldUiTheme.Rect(name,parent)','ExistingRect(name,parent)')
for typ in ['Image','Button','HudGlyph']:
    import re
    s=re.sub(r'(\w+)\.gameObject\.AddComponent<'+typ+r'>\(\)',r'Ensure<'+typ+r'>(\1)',s)
s=s.replace('PauseButton.onClick.AddListener(() => GetComponent<InGameView>()?.RequestPause());','PauseButton.onClick.RemoveListener(OnPauseClicked);\n            PauseButton.onClick.AddListener(OnPauseClicked);')
if 'static RectTransform ExistingRect' not in s:
    s=s.replace('        private RectTransform Plate(string name)', '''        private void OnPauseClicked() => GetComponent<InGameView>()?.RequestPause();
        private static RectTransform ExistingRect(string name,Transform parent)
            => parent.Find(name) as RectTransform ?? GoldUiTheme.Rect(name,parent);
        private static T Ensure<T>(Component parent) where T : Component
            => parent.GetComponent<T>() ?? parent.gameObject.AddComponent<T>();

        private RectTransform Plate(string name)''')
hud.write_text(s,encoding='utf-8')
print('Installed cel title, Cubism model, runtime compositor and serialization-safe HUD:',assets)
