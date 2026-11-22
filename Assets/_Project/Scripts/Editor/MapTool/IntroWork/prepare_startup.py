from pathlib import Path
root=Path('C:/Users/PC/UnityStudy/MyGame')
work=Path(__file__).resolve().parent
stage=work/'Startup';stage.mkdir(exist_ok=True)
paths=['Title/TitleController.cs','Logo/LogoSequencer.cs','LanguageSelect/LanguageSelectController.cs','Title/IntroSequencePlayer.cs','Title/IntroView.cs','Title/TitlePresentation.cs','Title/TitleIllustration.cs']
for path in paths:
 name=Path(path).name
 source=(root/'Assets/_Project/Scripts'/path).read_text(encoding='utf-8-sig')
 (stage/(name+'.before.txt')).write_text(source,encoding='utf-8')
 text=source
 if name=='TitleController.cs':
  text=text.replace('        private bool introPlaying;\n','')
  text=text.replace('!introPlaying && !systems.Loader.IsBusy','!systems.Loader.IsBusy')
  text=text.replace('if (introPlaying || systems.Loader.IsBusy)','if (systems.Loader.IsBusy)')
  start=text.index('            introPlaying = true;')
  end=text.index('\n        }',start)
  text=text[:start]+'            systems.Loader.LoadWithLoading(new LoadRequest(SceneId.WorldMap, null, LoadReason.NewGame));'+text[end:]
 elif name=='LogoSequencer.cs':
  old='''            var next = systems.Settings.Data.hasSelectedLanguage ? SceneId.Title : SceneId.LanguageSelect;
            systems.Loader.Load(next);'''
  new='''            if (systems.Settings.Data.hasSelectedLanguage)
                IntroSequencePlayer.Play(systems, () => systems.Loader.Load(SceneId.Title));
            else
                systems.Loader.Load(SceneId.LanguageSelect);'''
  assert old in text;text=text.replace(old,new)
 elif name=='LanguageSelectController.cs':
  text=text.replace('        private string previewCode;','        private string previewCode;\n        private bool introPlaying;')
  text=text.replace('if (!GameSystems.Exists || string.IsNullOrEmpty(code))','if (introPlaying || !GameSystems.Exists || string.IsNullOrEmpty(code))')
  text=text.replace('if (systems.Loader.IsBusy)','if (introPlaying || systems.Loader.IsBusy)')
  old='            systems.Loader.Load(SceneId.Title);'
  new='''            introPlaying = true;
            focus.enabled = false;
            confirmButton.interactable = false;
            foreach (var button in languageButtons)
                button.GetComponent<Button>().interactable = false;
            IntroSequencePlayer.Play(systems, () => systems.Loader.Load(SceneId.Title));'''
  assert old in text;text=text.replace(old,new)
 elif name=='IntroSequencePlayer.cs':
  text=text.replace('using System;','using System;\nusing System.Collections;')
  text=text.replace('using UnityEngine.EventSystems;','using UnityEngine.EventSystems;\nusing UnityEngine.InputSystem.UI;')
  text=text.replace('            view.Skip.onClick.AddListener(Skip);','''            view.Skip.onClick.AddListener(Skip);
            // The logo scene has no menu EventSystem. Own one only when needed.
            if(EventSystem.current==null)
            {
                var inputRoot=new GameObject("IntroEventSystem",typeof(EventSystem));
                inputRoot.transform.SetParent(transform,false);
                inputRoot.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }''')
  text=text.replace('bool finished, pausedByApplication, selectionSet;','bool finished, finishing, pausedByApplication, selectionSet;\n        float transitionGain=1;')
  text=text.replace('if(audioSource!=null)audioSource.volume=bgm;','if(audioSource!=null)audioSource.volume=bgm*transitionGain;')
  text=text.replace('player.SetDirectAudioVolume(0,bgm*','player.SetDirectAudioVolume(0,transitionGain*bgm*')
  text=text.replace('if(finished||player==null||pausedByApplication)return;','if(finished||finishing||player==null||pausedByApplication)return;')
  text=text.replace('            if(finished)return;\n            lastProgress','            if(finished||finishing)return;\n            lastProgress')
  text=text.replace('if(finished)return;finished=true;','''if(finished||finishing)return;
            finishing=true;
            if(view!=null)view.Skip.interactable=false;
            StartCoroutine(FinishRoutine());
        }
        IEnumerator FinishRoutine()
        {
            const float fadeSeconds=.45f;
            float elapsed=0;
            while(elapsed<fadeSeconds)
            {
                elapsed+=Time.unscaledDeltaTime;
                transitionGain=1-Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/fadeSeconds));
                if(view!=null)view.SetTransitionGain(transitionGain);
                ApplyVolume();
                yield return null;
            }
            finished=true;''')
  text=text.replace('if(finished||player==null)return;','if(finished||finishing||player==null)return;')
  text=text.replace('Plays the opening once per New Game','Plays the opening during application startup')
  text=text.replace(' Continuing to the world map.',' Continuing to the title.')
  text=text.replace('until the title scene unloads','until the hosting scene unloads')
 elif name=='IntroView.cs':
  text=text.replace('        CanvasGroup captionAlpha;','        CanvasGroup captionAlpha, frameAlpha;')
  text=text.replace('Frame.sizeDelta=new Vector2(1920,1080);','Frame.sizeDelta=new Vector2(1920,1080);\n            frameAlpha=Frame.gameObject.AddComponent<CanvasGroup>();')
  text=text.replace('        public void ShowBlack()','        public void SetTransitionGain(float gain){if(frameAlpha!=null)frameAlpha.alpha=Mathf.Clamp01(gain);}\n        public void ShowBlack()')
 elif name=='TitlePresentation.cs':
  old='private void Update() { elapsed += Time.unscaledDeltaTime; Sample(elapsed); }'
  new='''private void Update()
        {
            // Let the scene's black fade reveal the backdrop before the logo and menu enter.
            if (!GameSystems.Exists || !GameSystems.Instance.Loader.IsBusy)
                elapsed += Time.unscaledDeltaTime;
            Sample(elapsed);
        }'''
  assert old in text;text=text.replace(old,new)
 elif name=='TitleIllustration.cs':
  text=text.replace('            elapsed += Time.unscaledDeltaTime;','            if (!GameSystems.Exists || !GameSystems.Instance.Loader.IsBusy)\n                elapsed += Time.unscaledDeltaTime;')
 assert text!=source
 (stage/(name+'.txt')).write_text(text,encoding='utf-8')
print('Prepared startup flow in',stage)
