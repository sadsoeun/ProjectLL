from pathlib import Path
root=Path(r'C:\Users\PC\UnityStudy\MyGame\Assets\_Project\Scripts')
work=root/'Editor/MapTool/HudReadabilityWork'
def update(rel,fn):
    source=root/rel
    text=source.read_text(encoding='utf-8-sig')
    backup=work/'Backup'/(source.name+'.before.txt')
    if not backup.exists(): backup.write_text(text,encoding='utf-8')
    result=fn(text)
    assert result!=text, rel
    (work/'Staged'/(source.name+'.txt')).write_text(result,encoding='utf-8')
def theme(s):
    start=s.index('            // One compact, non-overlapping HUD band;')
    end=s.index('            var message = root.Find("MessageText");',start)
    return s[:start]+'            HudPresentation.Apply(root);\n'+s[end:]
update('UI/GoldUiTheme.cs',theme)
def view(s):
    s=s.replace('public event Action ResumeClicked;','public event Action PauseClicked;\n        public event Action ResumeClicked;')
    s=s.replace('public void SetCoins(int coins) => coinsText.SetKey("ingame.coins", coins);','public void SetCoins(int coins) => coinsText.SetText(coins.ToString(System.Globalization.CultureInfo.InvariantCulture));\n\n        public void RequestPause() => PauseClicked?.Invoke();\n\n        public void SetPauseAvailable(bool available)\n        { GetComponent<HudPresentation>()?.SetPauseAvailable(available); }')
    s=s.replace('public void SetLives(int lives) => livesText.SetKey("ingame.lives", lives);','public void SetLives(int lives) => livesText.SetText("× " + lives.ToString(System.Globalization.CultureInfo.InvariantCulture));')
    s=s.replace('timeText.SetKey("ingame.time", TimeFormat.Format(seconds));','timeText.SetText(TimeFormat.Format(seconds));')
    s=s.replace('            livesText.gameObject.SetActive(visible);','            GetComponent<HudPresentation>()?.SetVisible(visible);\n            livesText.gameObject.SetActive(visible);')
    return s
update('InGame/InGameView.cs',view)
def controller(s):
    s=s.replace('            view.ResumeClicked += Resume;','            view.PauseClicked += RequestHudPause;\n            view.ResumeClicked += Resume;')
    s=s.replace('            view.ResumeClicked -= Resume;','            view.PauseClicked -= RequestHudPause;\n            view.ResumeClicked -= Resume;')
    marker='        private void Pause()\n'
    assert marker in s
    s=s.replace(marker,'        private void RequestHudPause()\n        {\n            if (!initialized || session == null || session.State != StageState.Playing ||\n                systems == null || systems.InputMode.IsLocked || systems.Popups.IsBlocking || systems.Loader.IsBusy) return;\n            Pause();\n        }\n\n'+marker)
    marker='        private void Update()\n        {\n'
    assert marker in s
    s=s.replace(marker,marker+'            view.SetPauseAvailable(initialized && session != null && session.State == StageState.Playing &&\n                systems != null && !systems.InputMode.IsLocked && !systems.Popups.IsBlocking && !systems.Loader.IsBusy && !NowCutIn.IsPlaying);\n')
    return s
update('InGame/InGameController.cs',controller)
print('Staged three source updates; originals backed up.')
