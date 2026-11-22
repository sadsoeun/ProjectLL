from pathlib import Path
import re
root=Path('C:/Users/PC/UnityStudy/MyGame')
work=Path(__file__).resolve().parent
changes={
 'TitleController':('Title/TitleController.cs',[
 ('private GameSystems systems;', 'private GameSystems systems;\n        private bool introPlaying;'),
 ('private bool CanAct() => !systems.Loader.IsBusy && !systems.Popups.IsBlocking;', 'private bool CanAct() => !introPlaying && !systems.Loader.IsBusy && !systems.Popups.IsBlocking;'),
 ('if (systems.Loader.IsBusy)\n                return;', 'if (introPlaying || systems.Loader.IsBusy)\n                return;'),
 ('systems.Loader.LoadWithLoading(new LoadRequest(SceneId.WorldMap, null, LoadReason.NewGame));', '''introPlaying = true;
            focus.enabled = false;
            IntroSequencePlayer.Play(systems, () =>
            {
                if (this == null || systems == null) return;
                // Keep menu actions blocked until the title scene is replaced.
                systems.Loader.LoadWithLoading(new LoadRequest(SceneId.WorldMap, null, LoadReason.NewGame));
            });''')]),
 'LocalizationTable':('Services/LocalizationTable.cs',[
 ('UiTextKeys.Register(t);','UiTextKeys.Register(t);\n            IntroStory.Register(t);')])
}
for name,(relative,replacements) in changes.items():
 p=root/'Assets/_Project/Scripts'/relative
 text=p.read_text(encoding='utf-8-sig')
 backup=work/'Backup'/f'{name}.before.txt'
 if not backup.exists():backup.write_text(text,encoding='utf-8')
 for a,b in replacements:
  if text.count(a)!=1:raise RuntimeError(f'{name}: expected one replacement for {a}')
  text=text.replace(a,b)
 (work/'Staged'/f'{name}.cs.txt').write_text(text,encoding='utf-8')
print('Prepared title flow and localization registration')
