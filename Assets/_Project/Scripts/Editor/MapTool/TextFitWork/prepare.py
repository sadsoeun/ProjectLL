from pathlib import Path
root=Path('C:/Users/PC/UnityStudy/MyGame/Assets/_Project/Scripts/UI')
work=Path(__file__).parent
for name in ['LocalizedText','GoldUiTheme','UiKit','UiScreenLayout']:
 p=root/(name+'.cs');s=p.read_text(encoding='utf-8-sig')
 backup=work/(name+'.before.txt')
 if not backup.exists():backup.write_text(s,encoding='utf-8')
 if name=='LocalizedText':
  s=s.replace('            Refresh();\n        }\n\n        private void OnDisable()', '            Refresh();\n            LocalizedTextFit.Attach(Target);\n        }\n\n        private void OnDisable()')
  s=s.replace('                Target.font = font;','                Target.font = font;\n            LocalizedTextFit.Attach(Target);')
  s=s.replace('            Target.text = GameSystems.Instance.Localization.Get(key, args);','            Target.text = GameSystems.Instance.Localization.Get(key, args);\n            LocalizedTextFit.Attach(Target)?.FitNow();')
 elif name=='GoldUiTheme':
  s=s.replace('                text.resizeTextMaxSize = text.fontSize;','                text.resizeTextMaxSize = text.GetComponent<LocalizedTextFit>()?.DesignSize ?? text.fontSize;')
  s=s.replace('                rect.offsetMin = new Vector2(24,4); rect.offsetMax = new Vector2(-24,-4);','                rect.offsetMin = new Vector2(24,4); rect.offsetMax = new Vector2(-24,-4);\n                LocalizedTextFit.Attach(text, UiKit.ActionSize);')
 elif name=='UiKit':
  s=s.replace('            text.alignment=anchor;text.color=GoldUiTheme.Cream;text.raycastTarget=false;','            text.alignment=anchor;text.color=GoldUiTheme.Cream;text.raycastTarget=false;\n            LocalizedTextFit.Attach(text,size);')
 elif name=='UiScreenLayout':
  s=s.replace('                // All four peer menu actions use identical typography and padding.','                // Peer actions share the same design size; only overflowing translations shrink.')
  s=s.replace('                rect.localScale=Vector3.one;','                rect.localScale=Vector3.one;\n                LocalizedTextFit.Attach(label,UiKit.ActionSize);')
 (work/(name+'.cs.txt')).write_text(s,encoding='utf-8')
print('Prepared common localized fitting and 4 integration edits')
