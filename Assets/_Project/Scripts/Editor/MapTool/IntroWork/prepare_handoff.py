from pathlib import Path
import shutil
root=Path('C:/Users/PC/UnityStudy/MyGame');work=Path(__file__).resolve().parent
stage=work/'Handoff';stage.mkdir(exist_ok=True)
for name in ['IntroSequencePlayer','TitlePresentation','TitleIllustration','TitleController']:
 source=(root/'Assets/_Project/Scripts/Title'/f'{name}.cs').read_text(encoding='utf-8-sig')
 (stage/f'{name}.before.txt').write_text(source,encoding='utf-8')
 text=source
 if name=='IntroSequencePlayer':
  text=text.replace('            view.SetTime(time);','            view.SetTime(time);\n            if(time>=81.58)IntroTitleTransition.Ensure()?.SetFilmTime(time);')
  text=text.replace('            StartCoroutine(FinishRoutine());','            IntroTitleTransition.Ensure()?.BeginHandoff(player!=null&&player.isPrepared?target:null);\n            StartCoroutine(FinishRoutine());')
 elif name=='TitlePresentation':
  text=text.replace('        private bool initialized;','        private bool initialized;\n        private bool logoHandedOff;')
  text=text.replace('        private bool logoHandedOff;','        private bool logoHandedOff;\n        private readonly List<CanvasGroup> auxiliaryGroups=new List<CanvasGroup>();')
  text=text.replace('if (!GameSystems.Exists || !GameSystems.Instance.Loader.IsBusy)','if (!IntroTitleTransition.IsActive && (!GameSystems.Exists || !GameSystems.Instance.Loader.IsBusy))')
  text=text.replace('        public void Initialize()','        public void CompleteIntroEntrance(){logoHandedOff=true;elapsed=0;if(logoGroup!=null)logoGroup.alpha=1;}\n\n        public void Initialize()')
  text=text.replace('            var menu=transform.Find("MainMenu");','            foreach(var name in new[]{"VersionText","NoticeText"}){var item=transform.Find(name);if(item!=null)auxiliaryGroups.Add(Group(item));}\n            var menu=transform.Find("MainMenu");')
  text=text.replace('            for(int i=0;i<buttons.Count;i++)','            foreach(var group in auxiliaryGroups)group.alpha=IntroTitleTransition.IsActive?0:Mathf.SmoothStep(0,1,Mathf.Clamp01((time-.25f)/.45f));\n            for(int i=0;i<buttons.Count;i++)')
  old='{logoGroup.alpha=intro;logo.anchoredPosition=logoPosition+new Vector2(-32*(1-intro),0);}'
  new='{logoGroup.alpha=IntroTitleTransition.IsActive?0:logoHandedOff?1:intro;logo.anchoredPosition=logoPosition+new Vector2(logoHandedOff||IntroTitleTransition.IsActive?0:-32*(1-intro),0);}'
  assert old in text;text=text.replace(old,new)
 elif name=='TitleIllustration':
  text=text.replace('        float elapsed;','        float elapsed;\n        bool arrivedFromIntro;')
  text=text.replace('            var texture = Resources.Load<Texture2D>("TitleArt/Meadow");','            arrivedFromIntro=IntroTitleTransition.IsActive;\n            var texture = Resources.Load<Texture2D>("TitleArt/Meadow");')
  text=text.replace('if (!GameSystems.Exists || !GameSystems.Instance.Loader.IsBusy)','if (!IntroTitleTransition.IsActive && (!GameSystems.Exists || !GameSystems.Instance.Loader.IsBusy))')
  text=text.replace('float intro=1-Mathf.Pow','float intro=arrivedFromIntro?1:1-Mathf.Pow')
 else:
  text=text.replace('private bool CanAct() =>','private bool CanAct() => !IntroTitleTransition.IsActive &&')
 assert text!=source
 (stage/f'{name}.cs.txt').write_text(text,encoding='utf-8')
shutil.copyfile(work/'IntroTitleTransition.cs.txt',stage/'IntroTitleTransition.cs.txt')
copy=Path('C:/Users/PC/.codex/visualizations/2026/10/31/01a19252-8ac5-7110-b2a9-856eb2b4ca42/IntroFlowProject')
for name in ['IntroSequencePlayer','TitlePresentation','TitleIllustration','TitleController','IntroTitleTransition']:
 shutil.copyfile(stage/f'{name}.cs.txt',copy/'Assets/_Project/Scripts/Title'/f'{name}.cs')
print('Prepared continuous-logo handoff and copied it to isolated verification project.')
