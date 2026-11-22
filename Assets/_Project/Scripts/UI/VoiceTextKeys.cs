using System.Collections.Generic;

namespace MyGame
{
    /// <summary>Voice Studio strings. Column order: en, zh-Hans, ko, es, ja.</summary>
    public static class VoiceTextKeys
    {
        private static readonly string[] SlotKeys = { "jump", "dodge", "perfect", "death", "now", "clear", "boss", "title" };
        public static string NameKey(VoiceSlot slot) => "voice.slot." + SlotKeys[(int)slot];
        public static string DescriptionKey(VoiceSlot slot) => "voice.desc." + SlotKeys[(int)slot];

        public static void Register(Dictionary<string, string[]> t)
        {
            Add(t, "voice.title", "Voice Studio", "录音室", "녹음실", "Estudio de voz", "ボイススタジオ");
            Add(t, "voice.wallet", "Leaves {0}    Coins {1}", "金叶 {0}    金币 {1}", "나뭇잎 {0}    코인 {1}", "Hojas {0}    Monedas {1}", "葉っぱ {0}    コイン {1}");

            // Slots
            Add(t, "voice.slots", "Voice slots", "语音栏位", "목소리 슬롯", "Ranuras de voz", "ボイス枠");
            Add(t, "voice.duration", "{0} s", "{0} 秒", "{0}초", "{0} s", "{0}秒");
            Add(t, "voice.unlock_progress", "Slot locked", "栏位未解锁", "잠긴 슬롯", "Ranura bloqueada", "未解放のボイス枠");
            Add(t, "voice.monitor.empty", "Ready to record", "等待录音", "녹음 대기", "Listo para grabar", "録音待機");
            Add(t, "voice.monitor.saved", "Recorded waveform", "录音波形", "녹음 파형", "Forma de onda", "録音波形");
            Add(t, "voice.monitor.live", "Recording", "正在录音", "녹음 중", "Grabando", "録音中");
            Add(t, "voice.monitor.preview", "Playing", "正在播放", "재생 중", "Reproduciendo", "再生中");
            Add(t, "voice.slot.jump", "Jump", "跳跃", "점프", "Salto", "ジャンプ");
            Add(t, "voice.slot.dodge", "Dodge", "闪避", "회피", "Esquiva", "回避");
            Add(t, "voice.slot.perfect", "Perfect Dodge", "极限闪避", "극한회피", "Esquiva perfecta", "ジャスト回避");
            Add(t, "voice.slot.death", "Knocked Out", "倒下", "사망", "Derrota", "ダウン");
            Add(t, "voice.slot.now", "\"Now!\"", "“就是现在!”", "\"지금이다!\"", "«¡Ahora!»", "「今だ!」");
            Add(t, "voice.slot.clear", "Stage Clear", "过关", "스테이지 클리어", "Fase superada", "ステージクリア");
            Add(t, "voice.slot.boss", "Boss Defeated", "击败首领", "보스 처치", "Jefe derrotado", "ボス撃破");
            Add(t, "voice.slot.title", "Title Call", "标题喊话", "타이틀 콜", "Grito del título", "タイトルコール");
            Add(t, "voice.desc.clear", "Your cheer when you reach the goal, over the clear fanfare. Up to {0} s.", "到达终点时，在过关音乐之上播放你的欢呼。最长 {0} 秒。", "골인했을 때 클리어 팡파르와 함께 내 환호가 나와요. 최대 {0}초.", "Tu grito de alegría al llegar a la meta, sobre la fanfarria. Hasta {0} s.", "ゴールしたとき、クリアのファンファーレと一緒に自分の歓声が流れます。最大{0}秒。");
            Add(t, "voice.desc.boss", "Your victory line the moment a boss goes down. Up to {0} s.", "首领倒下的瞬间播放你的胜利台词。最长 {0} 秒。", "보스가 쓰러지는 순간 내 승리 대사가 나와요. 최대 {0}초.", "Tu frase de victoria en el instante en que cae un jefe. Hasta {0} s.", "ボスが倒れた瞬間に自分の勝利のセリフが流れます。最大{0}秒。");
            Add(t, "voice.desc.title", "Your voice calls the game's name on the title screen. The reward for every golden leaf. Up to {0} s.", "在标题画面用你的声音喊出游戏名。集齐所有黄金叶的奖励。最长 {0} 秒。", "타이틀 화면에서 내 목소리로 게임 이름을 외쳐요. 황금 잎을 전부 모은 보상이에요. 최대 {0}초.", "Tu voz grita el nombre del juego en la pantalla de título. La recompensa por todas las hojas doradas. Hasta {0} s.", "タイトル画面で自分の声がゲーム名をコールします。黄金の葉を全部集めたごほうびです。最大{0}秒。");
            Add(t, "voice.desc.jump", "Your voice every time you jump, on top of the jump sound. Up to {0} s.", "每次跳跃时播放你的声音，与跳跃音效叠加。最长 {0} 秒。", "점프할 때마다 점프 소리와 함께 내 목소리가 나와요. 최대 {0}초.", "Tu voz cada vez que saltas, junto al sonido del salto. Hasta {0} s.", "ジャンプするたびに、ジャンプ音と一緒に自分の声が流れます。最大{0}秒。");
            Add(t, "voice.desc.dodge", "Your voice when you dodge. Up to {0} s.", "闪避时播放你的声音。最长 {0} 秒。", "회피할 때 내 목소리가 나와요. 최대 {0}초.", "Tu voz al esquivar. Hasta {0} s.", "回避するときに自分の声が流れます。最大{0}秒。");
            Add(t, "voice.desc.perfect", "Your voice when you dodge an attack at the last moment. Up to {0} s.", "在最后一刻闪开攻击时播放你的声音。最长 {0} 秒。", "공격을 아슬아슬하게 피했을 때 내 목소리가 나와요. 최대 {0}초.", "Tu voz al esquivar un ataque en el último instante. Hasta {0} s.", "攻撃をぎりぎりでかわしたときに自分の声が流れます。最大{0}秒。");
            Add(t, "voice.desc.death", "Your voice when you are knocked out. Up to {0} s.", "倒下时播放你的声音。最长 {0} 秒。", "쓰러졌을 때 내 목소리가 나와요. 최대 {0}초.", "Tu voz cuando caes derrotado. Hasta {0} s.", "やられたときに自分の声が流れます。最大{0}秒。");
            Add(t, "voice.desc.now", "Your voice replaces the sound of the boss \"Now!\" cut-in. Up to {0} s.", "用你的声音替换首领战“就是现在!”特写的音效。最长 {0} 秒。", "보스전 \"지금이다!\" 컷인 소리 대신 내 목소리가 나와요. 최대 {0}초.", "Tu voz sustituye al sonido del «¡Ahora!» de los jefes. Hasta {0} s.", "ボス戦の「今だ!」カットインの音の代わりに自分の声が流れます。最大{0}秒。");

            // Slot list (second line of each row)
            Add(t, "voice.lock_badge", "Locked", "未解锁", "잠김", "Bloqueada", "ロック中");
            Add(t, "voice.list.locked", "Leaves {0} / {1}", "金叶 {0} / {1}", "나뭇잎 {0} / {1}", "Hojas {0} / {1}", "葉っぱ {0} / {1}");
            Add(t, "voice.list.default", "Default sound", "默认音效", "기본 소리", "Sonido original", "デフォルトの音");
            Add(t, "voice.list.recorded", "Recorded {0} / {1}", "已录制 {0} / {1}", "녹음됨 {0} / {1}", "Grabado {0} / {1}", "録音済み {0} / {1}");
            Add(t, "voice.list.off", "Recorded  ·  Off", "已录制  ·  关闭", "녹음됨  ·  꺼짐", "Grabado  ·  No", "録音済み  ·  オフ");

            // Takes
            Add(t, "voice.take", "Take {0}", "第 {0} 条", "테이크 {0}", "Toma {0}", "テイク {0}");
            Add(t, "voice.take_recorded", "Take {0}  ✓", "第 {0} 条  ✓", "테이크 {0}  ✓", "Toma {0}  ✓", "テイク {0}  ✓");
            Add(t, "voice.take_locked", "Take {0}  ·  {1} coins", "第 {0} 条  ·  {1} 金币", "테이크 {0}  ·  {1}코인", "Toma {0}  ·  {1} monedas", "テイク {0}  ·  {1}コイン");

            // Buttons
            Add(t, "voice.record", "Record", "录音", "녹음", "Grabar", "録音");
            Add(t, "voice.rerecord", "Record again", "重新录音", "다시 녹음", "Grabar de nuevo", "録り直す");
            Add(t, "voice.stop", "Stop", "停止", "멈추기", "Parar", "止める");
            Add(t, "voice.buy", "Open this take ({0} coins)", "解锁这一条（{0} 金币）", "테이크 열기 ({0}코인)", "Abrir toma ({0} monedas)", "テイクを開く（{0}コイン）");
            Add(t, "voice.play", "Listen", "试听", "들어보기", "Escuchar", "聞く");
            Add(t, "voice.delete", "Delete", "删除", "삭제", "Borrar", "削除");
            Add(t, "voice.use_on", "My voice: On", "我的声音：开启", "내 목소리: 켜짐", "Mi voz: Sí", "自分の声：オン");
            Add(t, "voice.use_off", "My voice: Off", "我的声音：关闭", "내 목소리: 꺼짐", "Mi voz: No", "自分の声：オフ");

            // Status line
            Add(t, "voice.msg.locked", "Golden leaves needed to unlock: {0}", "还需 {0} 片黄金叶即可解锁。", "황금 나뭇잎 {0}장을 더 모으면 열려요.", "Hojas doradas que faltan para desbloquear: {0}", "黄金の葉をあと{0}枚集めると開きます。");
            Add(t, "voice.msg.empty", "No recording yet: the default sound plays. Press Record to add your voice.", "还没有录音，将播放默认音效。按“录音”加入你的声音。", "아직 녹음이 없어 기본 소리가 나와요. 녹음을 눌러 내 목소리를 넣어 보세요.", "Aún no hay grabación: suena el original. Pulsa Grabar para añadir tu voz.", "まだ録音がないので、デフォルトの音が流れます。録音を押して声を入れましょう。");
            Add(t, "voice.msg.ready", "Listen to it, or record it again.", "可以试听，也可以重新录音。", "들어보거나 다시 녹음할 수 있어요.", "Escúchala o grábala de nuevo.", "聞いてみるか、録り直すことができます。");
            Add(t, "voice.msg.take_hint", "Extra takes play at random, so the voice does not repeat.", "多录几条会随机播放，声音不再重复。", "테이크를 추가하면 무작위로 재생되어 같은 소리가 반복되지 않아요.", "Las tomas extra suenan al azar para que la voz no se repita.", "テイクを増やすとランダムに再生され、同じ声が続きません。");
            Add(t, "voice.msg.get_ready", "Get ready…", "准备…", "준비하세요…", "Prepárate…", "準備して…");
            Add(t, "voice.msg.recording", "Recording… speak now!", "录音中…请说话！", "녹음 중… 지금 말하세요!", "Grabando… ¡habla ahora!", "録音中… 今しゃべって！");
            Add(t, "voice.msg.saved", "Saved. This is how it sounds.", "已保存。听听效果吧。", "저장했어요. 이렇게 들려요.", "Guardado. Así suena.", "保存しました。こう聞こえます。");
            Add(t, "voice.msg.deleted", "Deleted. The default sound plays again.", "已删除。恢复默认音效。", "삭제했어요. 다시 기본 소리가 나와요.", "Borrada. Vuelve a sonar el original.", "削除しました。デフォルトの音に戻ります。");
            Add(t, "voice.msg.too_quiet", "Nothing was heard. Check the microphone and speak louder.", "没有听到声音。请检查麦克风并大声一点。", "소리가 들리지 않았어요. 마이크를 확인하고 더 크게 말해 보세요.", "No se oyó nada. Revisa el micrófono y habla más alto.", "何も聞こえませんでした。マイクを確認して、もっと大きな声で。");
            Add(t, "voice.msg.no_mic", "No microphone found. Connect one and try again.", "未找到麦克风。请连接后重试。", "마이크를 찾지 못했어요. 연결한 뒤 다시 시도해 주세요.", "No se encontró micrófono. Conecta uno e inténtalo de nuevo.", "マイクが見つかりません。接続してからもう一度試してください。");
            Add(t, "voice.msg.mic_failed", "The microphone did not start. Check the microphone permission in Windows settings.", "麦克风未能启动。请在 Windows 设置中检查麦克风权限。", "마이크가 시작되지 않았어요. Windows 설정에서 마이크 권한을 확인해 주세요.", "El micrófono no se inició. Revisa su permiso en la configuración de Windows.", "マイクが開始できませんでした。Windowsの設定でマイクの許可を確認してください。");
            Add(t, "voice.msg.save_failed", "The recording could not be saved.", "录音保存失败。", "녹음을 저장하지 못했어요.", "No se pudo guardar la grabación.", "録音を保存できませんでした。");
            Add(t, "voice.msg.no_coins", "Not enough coins: {0} more needed.", "金币不足：还差 {0} 枚。", "코인이 부족해요. {0}개가 더 필요해요.", "No hay monedas suficientes: faltan {0}.", "コインが足りません。あと{0}枚必要です。");
            Add(t, "voice.msg.take_unlocked", "Take opened! Record another version.", "已解锁！再录一条吧。", "테이크가 열렸어요! 다른 버전을 녹음해 보세요.", "¡Toma abierta! Graba otra versión.", "テイクが開きました！別バージョンを録音しましょう。");

            // World map banner
            Add(t, "voice.unlock.heading", "Voice Slot Unlocked", "语音栏位解锁", "목소리 슬롯 해금", "Voz desbloqueada", "ボイス枠解放");
            Add(t, "voice.unlock.studio", "Record this voice in the Voice Studio.", "前往录音室录制这段语音。", "녹음실에서 내 목소리를 녹음할 수 있어요.", "Graba esta voz en el Estudio de voz.", "ボイススタジオで自分の声を録音できます。");
        }

        private static void Add(Dictionary<string, string[]> t, string key, params string[] values) => t[key] = values;
    }
}
