from pathlib import Path
root=Path(r'C:/Users/PC/UnityStudy/MyGame/Assets/_Project/Scripts')
work=Path(__file__).parent
def stage(rel, replacements):
    source=root/rel
    original=source.read_text(encoding='utf-8-sig')
    text=original
    for old,new in replacements:
        assert old in text, (rel,old)
        text=text.replace(old,new,1)
    (work/(source.name+'.before.txt')).write_text(original,encoding='utf-8')
    (work/(source.name+'.txt')).write_text(text,encoding='utf-8')
stage('UI/GoldUiTheme.cs', [('else if (root.name == "HUDCanvas") HudLayout(root);','else if (root.name == "HUDCanvas") HudLayout(root);\n            else if (root.name == "WorldMapCanvas") WorldMapPresentation.Apply(root);')])
stage('WorldMap/WorldMapController.cs',[
    ('private bool paused;','private bool paused;\n        private WorldMapPresentation presentation;'),
    ('enterButton.onClick.AddListener(TryEnter);','presentation = WorldMapPresentation.Apply(menuButton.transform.parent);\n            presentation.Configure(focus);\n            enterButton.onClick.AddListener(TryEnter);'),
    ('if (input.PausePressed || input.CancelPressed)\n                    Resume();','if (input.PausePressed || input.CancelPressed)\n                {\n                    if (!presentation.CloseMore()) Resume();\n                }'),
    ('paused = true;\n            pausePanel.SetActive(true);','paused = true;\n            presentation.SetMenuVisible(true);\n            pausePanel.SetActive(true);'),
    ('pausePanel.SetActive(false);\n            systems.InputMode.SetMode(InputMode.Gameplay);','pausePanel.SetActive(false);\n            presentation.SetMenuVisible(false);\n            systems.InputMode.SetMode(InputMode.Gameplay);'),
    ('private bool CanUseMenu => !systems.Loader.IsBusy','private bool CanUseMenu => systems != null && !systems.Loader.IsBusy'),
])
stage('UI/GoldUiReveal.cs', [('private void OnEnable(){','public void RefreshRestPose()\n        {\n            Initialize(); position=rect.anchoredPosition; scale=rect.localScale;\n        }\n        private void OnEnable(){')])
stage('Services/LocalizationTable.cs', [('var t = new Dictionary<string, string[]>();','''var t = new Dictionary<string, string[]>();
            Add(t, "worldmap.heading", "WORLD MAP", "世界地图", "월드맵", "MAPA DEL MUNDO", "ワールドマップ");
            Add(t, "worldmap.hint_short", "Move: WASD / Stick     Zoom: Wheel / LB RB     Menu: Esc / Start", "移动：WASD / 摇杆    缩放：滚轮 / LB RB    菜单：Esc / Start", "이동  WASD / 스틱     확대·축소  휠 / LB RB     메뉴  Esc / Start", "Mover: WASD / palanca    Zoom: rueda / LB RB    Menú: Esc / Start", "移動 WASD / スティック　ズーム ホイール / LB RB　メニュー Esc / Start");
            Add(t, "worldmap.menu_title", "Adventure menu", "冒险菜单", "모험 메뉴", "Menú de aventura", "冒険メニュー");
            Add(t, "worldmap.more", "More options", "更多选项", "추가 메뉴", "Más opciones", "その他");
            Add(t, "worldmap.back", "Back", "返回", "이전 메뉴", "Volver", "戻る");
            Add(t, "worldmap.menu_note", "Choose a stage to continue your adventure.", "选择关卡，继续冒险。", "스테이지를 선택해 모험을 이어가세요.", "Elige un nivel y continúa la aventura.", "ステージを選んで冒険を続けましょう。");
            Add(t, "worldmap.data_note", "Progress saves automatically at each stage stop.", "到达关卡时自动保存进度。", "스테이지에 도착하면 진행 상황이 자동 저장됩니다.", "Se guarda al llegar a cada nivel.", "ステージ到着時に進行状況を自動保存します。");''')])
