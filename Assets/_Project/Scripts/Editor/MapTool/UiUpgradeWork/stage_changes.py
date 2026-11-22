from pathlib import Path
import json
root=Path('C:/Users/PC/UnityStudy/MyGame/Assets/_Project/Scripts')
work=Path(__file__).parent
manifest=[]
def stage(rel,replacements):
    path=root/rel
    original=path.read_text(encoding='utf-8-sig'); text=original
    for old,new in replacements:
        assert old in text,(rel,old)
        text=text.replace(old,new,1)
    dest=work/(path.stem+'.cs.txt'); before=work/(path.stem+'.before.txt')
    if not before.exists():before.write_text(original,encoding='utf-8')
    dest.write_text(text,encoding='utf-8');manifest.append([str(dest),str(path)])
stage('UI/GoldUiTheme.cs',[
('new Color(.025f,.043f,.047f,.96f)','new Color(.035f,.038f,.045f,.96f)'),
('            foreach (var text in root.GetComponentsInChildren<Text>(true))','            foreach (var text in root.GetComponentsInChildren<Text>(true))'),
('                var bar = root.Find("ProgressBar"); if (bar != null) Frame(bar);\n            }','                var bar = root.Find("ProgressBar"); if (bar != null) Frame(bar);\n            }\n            if(root.GetComponent<SettingsPopup>()!=null) SettingsPresentation.Apply(root);\n            else if(root.name=="LanguageCanvas"||root.name=="HUDCanvas"||root.name=="LoadingCanvas"||root.name=="TitleCanvas"||root.GetComponent<ConfirmPopup>()!=null) UiScreenLayout.Apply(root);'),
])
stage('Services/LocalizationTable.cs',[('var t = new Dictionary<string, string[]>();','var t = new Dictionary<string, string[]>();\n            UiTextKeys.Register(t);')])
stage('LanguageSelect/LanguageSelectController.cs',[
('GameSystems.Instance.Localization.SetLanguage(code);','GameSystems.Instance.Localization.SetLanguage(code);\n            foreach(var item in languageButtons)\n            {\n                int index=LocalizationTable.IndexOf(item.LanguageCode);\n                item.GetComponentInChildren<LocalizedText>().SetText((item.LanguageCode==code ? "✓  " : "")+LocalizationTable.NativeNames[index]);\n                GoldUiTheme.MarkLanguage(item.GetComponent<Button>(),item.LanguageCode==code);\n            }')])
stage('UI/SettingsPopup.cs',[
('closeButton.onClick.AddListener(Close);','closeButton.onClick.AddListener(RequestClose);'),
('BeginOpen(host, languageButtons[current].gameObject);','BeginOpen(host, masterSlider.gameObject);\n            GetComponent<SettingsPresentation>()?.RefreshValues();'),
('            UpdateLanguageMarkers();\n        }\n\n        private void UpdateLanguageMarkers()', '            UpdateLanguageMarkers();\n            GetComponent<SettingsPresentation>()?.RefreshValues();\n            GetComponent<SettingsPresentation>()?.CloseLanguages();\n        }\n\n        private void UpdateLanguageMarkers()'),
('        protected override void OnClosed()', '        private void RequestClose()\n        {\n            if(GetComponent<SettingsPresentation>()?.CloseLanguages()==true)return;\n            Close();\n        }\n        protected override void OnCancelRequested()=>RequestClose();\n\n        protected override void OnClosed()'),
('                Debug.LogError("[SettingsPopup] Failed to save settings.json.");','            {\n                Debug.LogError("[SettingsPopup] Failed to save settings.json.");\n                var service=settings;\n                UiFeedback.SaveFailed("ui.save_settings_failed",service.Save);\n            }')])
stage('UI/ConfirmPopup.cs',[
('            message.SetKey(messageKey);','''            message.SetKey(messageKey);
            string action="common.yes", cancel="ui.cancel";
            switch(messageKey)
            {
                case "title.confirm_quit": case "worldmap.confirm_quit": action="title.quit";break;
                case "title.confirm_overwrite":action="title.new_game";break;
                case "pause.confirm_exit":action="pause.exit_map";break;
                case "worldmap.confirm_load":action="pause.load";break;
                case "ui.save_settings_failed":case "ui.save_progress_failed":action="ui.retry";cancel="ui.dismiss";break;
            }
            UiKit.Key(yesButton.transform,action);UiKit.Key(noButton.transform,cancel);''')])
stage('UI/HudPresentation.cs',[
('new Vector2(22,22)','new Vector2(24,24)')])
stage('InGame/InGameView.cs',[
('private int shownSecond = -1;','public bool IsHudVisible { get; private set; } = true;\n        private int shownSecond = -1;'),
('specialSlots[i].color = (collected != null && i < collected.Length && collected[i]) ? HudColors.SpecialFull : HudColors.SpecialEmpty;','CollectibleSlot.Set(specialSlots[i],collected != null && i < collected.Length && collected[i]);'),
('public void ShowResult(float clearSeconds)','public void ShowResult(float clearSeconds,int coins=0,int collected=0,int total=3,bool newRecord=false)'),
('                focus.Select(resultContinueButton.gameObject);\n            });','                focus.Select(resultContinueButton.gameObject);\n            });\n            clearCelebration.SetSummary(coins,collected,total,newRecord);'),
('        private void SetHudVisible(bool visible)\n        {','        private void SetHudVisible(bool visible)\n        {\n            IsHudVisible=visible;')])
stage('WorldMap/WorldMapController.cs',[
('specialSlots[i].color = (record != null && record.IsCollected(i)) ? HudColors.SpecialFull : HudColors.SpecialEmpty;','CollectibleSlot.Set(specialSlots[i],record != null && record.IsCollected(i));')])
stage('InGame/Boss/BossHud.cs',[
('private float pulse;','private float pulse;\n        private InGameView gameView;'),
('            root.sizeDelta = new Vector2(760f, 90f);','''            root.sizeDelta = new Vector2(760f, 100f);
            var plate=root.gameObject.AddComponent<Image>();plate.color=GoldUiTheme.Panel;plate.raycastTarget=false;
            GoldUiTheme.Frame(root).diamond=false;
            gameView=Object.FindFirstObjectByType<InGameView>();'''),
('text.color = Color.white;','text.color = GoldUiTheme.Cream;'),
('            if (root == null || pulse <= 0f)\n                return;','''            if (root == null)return;
            if(gameView==null)gameView=Object.FindFirstObjectByType<InGameView>();
            bool visible=(gameView==null||gameView.IsHudVisible)&&(!GameSystems.Exists||!GameSystems.Instance.Popups.IsBlocking);
            root.gameObject.SetActive(visible);
            float width=((RectTransform)transform).rect.width;
            root.anchoredPosition=new Vector2(0,width<1240?-220:-120);'''),
('root.localScale = Vector3.one * (1f + 0.08f * pulse);','root.localScale = Vector3.one * Mathf.Min(1,Mathf.Max(.1f,((RectTransform)transform).rect.width/900f)) * (1f + 0.04f * pulse);')])
stage('InGame/StageClearCelebration.cs',[
('private RectTransform results;','private RectTransform results;\n        private LocalizedText summary,recordBadge;'),
('            results.gameObject.SetActive(false);\n        }','''            summary=UiKit.Label(results,"LootSummary","ui.result_loot",26,TextAnchor.MiddleCenter);
            UiKit.Place(summary.transform,.5f,.5f,0,-82,980,38);
            recordBadge=UiKit.Label(results,"RecordBadge","ui.new_record",26,TextAnchor.MiddleCenter);
            UiKit.Place(recordBadge.transform,.5f,.5f,0,136,900,36);recordBadge.GetComponent<Text>().color=GoldUiTheme.Gold;
            recordBadge.gameObject.SetActive(false);
            results.gameObject.SetActive(false);
        }
        public void SetSummary(int coins,int collected,int total,bool newRecord)
        {Build();summary.SetKey("ui.result_loot",coins,collected,total);recordBadge.gameObject.SetActive(newRecord);}'''),
('new Vector2(0,-130),new Vector2(480,90)','new Vector2(0,-160),new Vector2(480,76)'),
('            GoldUiTheme.StyleButton(continueButton);','            UiKit.ButtonStyle(continueButton,true);\n            UiKit.Key(continueButton.transform,"pause.exit_map");')])
stage('InGame/InGameController.cs',[
('            save.Current.RecordClear(stageData.StageId, session.Elapsed, session.CopySpecials(), session.Coins);','''            var previousRecord=save.Current.FindStage(stageData.StageId);
            bool newRecord=previousRecord==null||!previousRecord.cleared||previousRecord.bestTime<=0||session.Elapsed<previousRecord.bestTime;
            var collectedSlots=session.CopySpecials();int collectedCount=0;
            foreach(bool obtained in collectedSlots)if(obtained)collectedCount++;
            save.Current.RecordClear(stageData.StageId, session.Elapsed, collectedSlots, session.Coins);'''),
('                Debug.LogError("[InGame] Failed to save stage clear.");','            {\n                Debug.LogError("[InGame] Failed to save stage clear.");\n                UiFeedback.SaveFailed("ui.save_progress_failed",save.Save);\n            }'),
('            view.ShowResult(session.Elapsed);','            view.ShowResult(session.Elapsed,session.Coins,collectedCount,stageData.CollectibleCount,newRecord);'),
('                Debug.LogError("[InGame] Failed to save after game over.");','            {\n                Debug.LogError("[InGame] Failed to save after game over.");\n                var service=systems.Save;UiFeedback.SaveFailed("ui.save_progress_failed",service.Save);\n            }'),
('        private void OnResultContinue()\n        {\n            if (systems.Loader.IsBusy)','        private void OnResultContinue()\n        {\n            if (systems.Loader.IsBusy || systems.Popups.IsBlocking)'),
('        private void OnGameOverContinue()\n        {\n            if (systems.Loader.IsBusy)','        private void OnGameOverContinue()\n        {\n            if (systems.Loader.IsBusy || systems.Popups.IsBlocking)')])
stage('Title/TitleController.cs',[
('            ShowSaveNotice(systems.Save.LastStatus);','''            ShowSaveNotice(systems.Save.LastStatus);
            if(hasSave&&systems.StageDb.TryGetNode(systems.Save.Current.currentNodeId,out var node)&&node.Stage!=null)
            {
                var label=UiKit.Label(continueButton.transform,"ResumeStage",node.Stage.DisplayNameKey,22,TextAnchor.MiddleCenter);
                UiKit.Place(label.transform,.5f,0,0,6,500,28);label.GetComponent<Text>().color=UiKit.Muted;
                var main=continueButton.transform.Find("Label") as RectTransform;
                if(main!=null){main.offsetMin=new Vector2(24,32);main.offsetMax=new Vector2(-24,-5);}
            }''')])
stage('Loading/LoadingController.cs',[
('            // Step 1 (10%): release unused assets from the previous scene.','''            var canvas=tipText.GetComponentInParent<Canvas>();
            var destination=canvas!=null?canvas.transform.Find("LoadingContent/Destination"):null;
            if(destination!=null)
            {
                string key="worldmap.heading";
                if(request.TargetScene==SceneId.InGame&&systems.StageDb.TryGetStage(request.StageId,out var nextStage))key=nextStage.DisplayNameKey;
                destination.GetComponent<LocalizedText>().SetKey(key);
            }
            // Step 1 (10%): release unused assets from the previous scene.'''),
('                request = new LoadRequest(SceneId.WorldMap, null, LoadReason.Failed);','                request = new LoadRequest(SceneId.WorldMap, null, LoadReason.Failed);\n                UiFeedback.Notice("ui.loading_failed");\n                if(destination!=null)destination.GetComponent<LocalizedText>().SetKey("worldmap.heading");')])
stage('InGame/FX/NowCutIn.cs',[
('label.color = new Color(1f, 0.93f, 0.25f);','label.color = GoldUiTheme.Cream;\n            label.resizeTextForBestFit=true;label.resizeTextMinSize=64;label.resizeTextMaxSize=140;'),
('outline.effectColor = new Color(0.55f, 0.02f, 0.05f);','outline.effectColor = GoldUiTheme.Panel;')])
for name in ['UiKit','UiScreenLayout','SettingsPresentation','UiFeedback','CollectibleSlot','UiTextKeys']:
    manifest.append([str(work/(name+'.cs.txt')),str(root/'UI'/(name+'.cs'))])
(work/'install-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print('Staged',len(manifest),'files')
