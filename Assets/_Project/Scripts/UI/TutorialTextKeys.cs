using System.Collections.Generic;

namespace MyGame
{
    /// <summary>Tutorial prompt strings (TutorialConfig keys). Column order: en, zh-Hans, ko, es, ja.</summary>
    public static class TutorialTextKeys
    {
        public static void Register(Dictionary<string, string[]> t)
        {
            // W1_S1 (handoff design v1, cuts 01-07)
            Add(t, "tutorial.first", "Shall we follow the wind?", "跟着风走吧？", "바람을 따라가 볼까?", "¿Seguimos al viento?", "風について行ってみよう？");
            Add(t, "tutorial.move", "Move left and right.", "左右移动看看。", "좌우로 움직여 보세요.", "Muévete a izquierda y derecha.", "左右に動いてみよう。");
            Add(t, "tutorial.run", "Try going a little faster.", "试着跑得更快一点。", "조금 더 빠르게 달려보세요.", "Prueba a ir un poco más rápido.", "もう少し速く走ってみよう。");
            Add(t, "tutorial.run_sub", "Hold it while moving to run.", "移动时按住即可奔跑。", "누른 채로 이동하면 달려요.", "Mantenlo pulsado al moverte para correr.", "押したまま移動すると走れます。");
            Add(t, "tutorial.jump", "Jump to get across.", "跳过去吧。", "점프로 건너가세요.", "Salta para cruzar.", "ジャンプで渡ろう。");
            Add(t, "tutorial.jump_sub", "Hold the button to jump higher.", "按住按钮可以跳得更高。", "길게 누르면 더 높이 뛰어요.", "Mantén pulsado para saltar más alto.", "長押しするとより高く跳べます。");
            Add(t, "tutorial.leaf", "Look for the hidden golden leaf.", "找找隐藏的金色树叶吧。", "숨겨진 황금 나뭇잎을 찾아보세요.", "Busca la hoja dorada escondida.", "隠された黄金の葉を探してみよう。");
            Add(t, "tutorial.checkpoint", "You can restart from here.", "可以从这里重新开始。", "여기서 다시 시작할 수 있어요.", "Puedes volver a empezar desde aquí.", "ここから再スタートできます。");
            Add(t, "tutorial.stomp", "Land on the enemy's head.", "踩到敌人的头上。", "적의 머리 위로 착지하세요.", "Aterriza sobre la cabeza del enemigo.", "敵の頭の上に着地しよう。");
            Add(t, "tutorial.dodge", "Dodge just before the attack hits!", "在攻击命中前一刻闪避！", "공격이 닿기 직전, 회피!", "¡Esquiva justo antes del golpe!", "攻撃が当たる直前に回避！");
            Add(t, "tutorial.dodge_sub", "A perfect dodge stops time for a moment.", "完美闪避会让时间暂停片刻。", "완벽하게 피하면 잠시 시간이 멈춰요.", "Una esquiva perfecta detiene el tiempo un instante.", "完璧に避けると一瞬時間が止まります。");
            Add(t, "tutorial.dodge_help", "It's fine to keep exploring and learn it along the way.", "继续探索，慢慢掌握也没关系。", "계속 탐험하며 익혀도 괜찮아요.", "Puedes seguir explorando y aprenderlo poco a poco.", "冒険を続けながら覚えても大丈夫。");

            // World map (first visit)
            Add(t, "tutorial.map_move", "Go to the next or previous stage.", "前往下一关或上一关。", "다음·이전 스테이지로 이동해요.", "Ve al nivel siguiente o al anterior.", "次・前のステージへ移動します。");
            Add(t, "tutorial.map_enter", "Enter the stage.", "进入关卡。", "스테이지에 들어가요.", "Entra en el nivel.", "ステージに入ります。");

            // Explanation bubbles (game frozen, one highlight at a time). ".t" = title, ".b" = body.
            Add(t, "tutorial.next", "Next", "下一步", "다음", "Siguiente", "次へ");
            Add(t, "tutorial.continue", "Continue", "继续", "계속", "Continuar", "つづける");
            Add(t, "tutorial.skip", "Hold to skip the tutorial", "长按跳过教程", "길게 눌러 튜토리얼 건너뛰기", "Mantén para saltar el tutorial", "長押しでチュートリアルをスキップ");
            Add(t, "tutorial.success", "Well done!", "做得好！", "잘했어요!", "¡Bien hecho!", "よくできました！");

            Add(t, "tutorial.ex.move.t", "Shall we follow the wind?", "跟着风走吧？", "바람을 따라가 볼까?", "¿Seguimos al viento?", "風について行ってみよう？");
            Add(t, "tutorial.ex.move.b",
                "Let's start with walking.\nYou can move left and right.",
                "先从走路开始吧。\n可以向左、向右移动。",
                "먼저 걷는 법부터 익혀 봐요.\n왼쪽과 오른쪽으로 움직일 수 있어요.",
                "Empecemos por caminar.\nPuedes moverte a izquierda y derecha.",
                "まずは歩き方から覚えよう。\n左と右に動けます。");
            Add(t, "tutorial.ex.run.t", "Running", "奔跑", "달리기", "Correr", "ダッシュ");
            Add(t, "tutorial.ex.run.b",
                "Hold the run button while moving to go faster.\nIt helps when you need a long jump.",
                "移动时按住奔跑键，就能跑得更快。\n需要跳得更远时很有用。",
                "달리기 버튼을 누른 채로 움직이면 더 빨리 달려요.\n멀리 뛰어야 할 때 도움이 돼요.",
                "Mantén el botón de correr al moverte para ir más rápido.\nViene bien cuando necesitas saltar lejos.",
                "ダッシュボタンを押したまま動くと、もっと速く走れます。\n遠くへ跳びたいときに役立ちます。");
            Add(t, "tutorial.ex.jump.t", "Jumping", "跳跃", "점프", "Saltar", "ジャンプ");
            Add(t, "tutorial.ex.jump.b",
                "There's a high step ahead. Jump up onto it.\nHold the button longer to jump higher.",
                "前面有一个高台，跳上去吧。\n按住按钮越久，跳得越高。",
                "앞에 높은 턱이 있어요. 점프로 올라가 보세요.\n버튼을 길게 누르면 더 높이 뛰어요.",
                "Hay un escalón alto delante. Súbete de un salto.\nMantén pulsado el botón para saltar más alto.",
                "この先に高い段差があります。ジャンプで登ってみよう。\nボタンを長く押すと、より高く跳べます。");
            Add(t, "tutorial.ex.leaf.t", "Golden Leaves", "金色树叶", "황금 나뭇잎", "Hojas doradas", "黄金の葉");
            Add(t, "tutorial.ex.leaf.b",
                "Three golden leaves are hidden in every stage.\nIt's okay to miss one. You can come back for it later.",
                "每个关卡都藏着三片金色树叶。\n错过了也没关系，以后还可以再来找。",
                "스테이지마다 황금 나뭇잎이 세 장 숨어 있어요.\n지나쳐도 괜찮아요. 나중에 다시 찾으러 올 수 있어요.",
                "En cada nivel hay tres hojas doradas escondidas.\nNo pasa nada si se te escapa alguna. Puedes volver a por ella después.",
                "ステージごとに黄金の葉が3枚隠れています。\n見逃しても大丈夫。あとでまた探しに来られます。");
            Add(t, "tutorial.ex.leaf_hud.t", "Leaves You've Found", "已找到的树叶", "모은 나뭇잎", "Hojas encontradas", "集めた葉");
            Add(t, "tutorial.ex.leaf_hud.b",
                "The leaves you collect show up here.\nTry to find all three!",
                "收集到的树叶会显示在这里。\n试着把三片都找齐吧！",
                "모은 나뭇잎은 여기에 표시돼요.\n세 장을 모두 찾아보세요!",
                "Las hojas que recojas aparecen aquí.\n¡Intenta encontrar las tres!",
                "集めた葉はここに表示されます。\n3枚ぜんぶ見つけてみよう！");
            Add(t, "tutorial.ex.checkpoint.t", "Checkpoint", "检查点", "체크포인트", "Punto de control", "チェックポイント");
            Add(t, "tutorial.ex.checkpoint.b",
                "Touching a flag turns the checkpoint on.\nIf you fall, you can start again from here.",
                "碰到旗帜就会点亮检查点。\n即使失败，也可以从这里重新开始。",
                "깃발에 닿으면 체크포인트가 켜져요.\n쓰러져도 여기서 다시 시작할 수 있어요.",
                "Al tocar la bandera se activa el punto de control.\nSi caes, puedes volver a empezar desde aquí.",
                "旗に触れるとチェックポイントが点灯します。\nやられても、ここからやり直せます。");
            Add(t, "tutorial.ex.stomp.t", "Stomping", "踩踏", "밟기", "Pisotón", "ふみつけ");
            Add(t, "tutorial.ex.stomp.b",
                "Bumping into an enemy knocks you down.\nBut jump and land on its head to defeat it!",
                "撞到敌人会被击倒。\n不过跳起来踩到它的头上，就能打败它！",
                "적과 부딪히면 쓰러져요.\n하지만 점프해서 머리를 밟으면 물리칠 수 있어요!",
                "Si chocas con un enemigo, caes.\n¡Pero salta y aterriza en su cabeza para derrotarlo!",
                "敵にぶつかるとやられてしまいます。\nでも、ジャンプして頭をふめば倒せます！");
            Add(t, "tutorial.ex.dodge.t", "Dodging", "闪避", "회피", "Esquivar", "回避");
            Add(t, "tutorial.ex.dodge.b",
                "This time, try dodging instead of stomping.\nPress the dodge button to dash quickly forward.",
                "这次不要踩，试着闪避吧。\n按下闪避键，就能迅速向前冲。",
                "이번에는 밟지 말고 피해 볼까요?\n회피 버튼을 누르면 앞으로 재빨리 몸을 날려요.",
                "Esta vez, prueba a esquivar en lugar de pisar.\nPulsa el botón de esquivar para lanzarte hacia delante.",
                "今度はふまずに、よけてみよう。\n回避ボタンを押すと、すばやく前に飛びこみます。");
            Add(t, "tutorial.ex.dodge_timing.t", "Timing Matters", "时机很重要", "타이밍이 중요해요", "El momento importa", "タイミングが大事");
            Add(t, "tutorial.ex.dodge_timing.b",
                "Dodge just before you touch the enemy for a perfect dodge, and time stops for a moment.\nIf it's tricky, it's fine to just move on.",
                "在快要碰到敌人的瞬间闪避，就是完美闪避，时间会暂停片刻。\n觉得难的话，直接过去也没关系。",
                "적과 닿기 직전에 회피하면 퍼펙트 회피! 잠시 시간이 멈춰요.\n어려우면 그냥 지나가도 괜찮아요.",
                "Esquiva justo antes de tocar al enemigo y harás una esquiva perfecta: el tiempo se detiene un instante.\nSi te cuesta, puedes seguir adelante sin más.",
                "敵に触れる直前に回避するとパーフェクト回避！一瞬、時間が止まります。\nむずかしければ、そのまま進んでも大丈夫。");
            Add(t, "tutorial.ex.map_enter.t", "World Map", "世界地图", "월드맵", "Mapa del mundo", "ワールドマップ");
            Add(t, "tutorial.ex.map_enter.b",
                "This is the world map.\nShall we enter the stage you're standing on?",
                "这里是世界地图。\n进入现在所站的关卡看看吧？",
                "여기는 월드맵이에요.\n지금 서 있는 발판의 스테이지에 들어가 볼까요?",
                "Este es el mapa del mundo.\n¿Entramos en el nivel donde estás?",
                "ここはワールドマップです。\n今立っているステージに入ってみよう。");
            Add(t, "tutorial.ex.map_move.t", "A New Path", "新的道路", "새로운 길", "Un nuevo camino", "新しい道");
            Add(t, "tutorial.ex.map_move.b",
                "A new stage has opened!\nMove along the path to the next or previous stage.",
                "新的关卡开放了！\n沿着道路前往下一关或上一关吧。",
                "새 스테이지가 열렸어요!\n길을 따라 다음·이전 스테이지로 이동해 보세요.",
                "¡Se ha abierto un nivel nuevo!\nSigue el camino hasta el nivel siguiente o el anterior.",
                "新しいステージが開きました！\n道に沿って次・前のステージへ移動してみよう。");

            // W1_B boss (Sprout King). Same bubble style; opened only at calm moments of the fight.
            Add(t, "tutorial.ex.boss.t", "The Sprout King!", "新芽王登场！", "새싹왕 등장!", "¡El Rey Brote!", "新芽王あらわる！");
            Add(t, "tutorial.ex.boss.b",
                "The master of this fort, the Sprout King, has appeared.\nDefeat him to clear the stage.",
                "这座要塞的主人——新芽王出现了。\n打倒它就能通关。",
                "요새의 주인 새싹왕이 나타났어요.\n새싹왕을 쓰러뜨리면 스테이지 클리어예요.",
                "Ha aparecido el Rey Brote, señor de esta fortaleza.\nDerrótalo para superar el nivel.",
                "とりでの主、新芽王があらわれました。\n倒せばステージクリアです。");
            Add(t, "tutorial.ex.boss_hp.t", "Boss Health", "首领的体力", "보스의 체력", "Vida del jefe", "ボスの体力");
            Add(t, "tutorial.ex.boss_hp.b",
                "This bar is the Sprout King's health.\nEmpty it completely to win!",
                "这条就是新芽王的体力。\n把它全部清空就赢了！",
                "이 막대가 새싹왕의 체력이에요.\n체력을 모두 비우면 이겨요!",
                "Esta barra es la vida del Rey Brote.\n¡Vacíala del todo para ganar!",
                "このバーが新芽王の体力です。\nぜんぶ空にすれば勝ちです！");
            Add(t, "tutorial.ex.boss_charge.t", "Watch Out for the Charge", "小心冲撞", "돌진을 조심하세요", "Cuidado con la embestida", "突進に気をつけて");
            Add(t, "tutorial.ex.boss_charge.b",
                "When his body flashes, he is about to charge.\nJump high over him, or dodge just before he reaches you.",
                "它的身体一闪一闪，就是要冲过来了。\n高高跳起越过去，或者在快被撞到时闪避。",
                "몸이 번쩍이면 곧 돌진해 와요.\n높이 점프해서 뛰어넘거나, 닿기 직전에 회피하세요.",
                "Cuando su cuerpo parpadea, está a punto de embestir.\nSalta alto por encima o esquiva justo antes de que te alcance.",
                "体がピカピカ光ったら、突進の合図です。\n高くジャンプして飛びこえるか、当たる直前に回避しましょう。");
            Add(t, "tutorial.ex.boss_stun.t", "Now's Your Chance!", "机会来了！", "지금이 기회!", "¡Ahora es tu oportunidad!", "いまがチャンス！");
            Add(t, "tutorial.ex.boss_stun.b",
                "After an attack, the Sprout King is stunned for a moment.\nStomp on his head while the stars are spinning to hurt him!",
                "攻击结束后，新芽王会晕一会儿。\n趁它头上星星打转的时候踩它的头，就能削减体力！",
                "공격이 끝나면 새싹왕이 잠시 기절해요.\n머리 위에 별이 도는 동안 머리를 밟으면 체력이 줄어요!",
                "Tras atacar, el Rey Brote queda aturdido un momento.\n¡Písale la cabeza mientras giran las estrellas para hacerle daño!",
                "攻撃のあと、新芽王は少しのあいだ気絶します。\n頭の上で星が回っているうちに頭をふむと、体力を減らせます！");
            Add(t, "tutorial.ex.boss_rain.t", "Acorn Rain", "橡果雨", "도토리 비", "Lluvia de bellotas", "どんぐりの雨");
            Add(t, "tutorial.ex.boss_rain.b",
                "The angry Sprout King now drops acorns too.\nWhen you see a shadow on the ground, step away from it.",
                "生气的新芽王开始往下丢橡果了。\n看到地上的影子，就赶快离开那里。",
                "화가 난 새싹왕이 이제 도토리도 떨어뜨려요.\n바닥에 그림자가 보이면 그 자리를 피하세요.",
                "El Rey Brote, enfadado, ahora también lanza bellotas.\nCuando veas una sombra en el suelo, apártate de ella.",
                "怒った新芽王が、どんぐりも落としてきます。\n地面に影が見えたら、その場所からはなれましょう。");
            Add(t, "tutorial.boss_charge", "Jump over the charge.", "跳过它的冲撞。", "돌진을 뛰어넘으세요.", "Salta por encima de la embestida.", "突進を飛びこえよう。");
            Add(t, "tutorial.boss_charge_sub", "Dodging just before it hits works too.", "在快被撞到时闪避也可以。", "닿기 직전에 회피해도 좋아요.", "También vale esquivar justo antes del golpe.", "当たる直前に回避してもOK。");
            Add(t, "tutorial.boss_stomp", "Stomp on the stunned Sprout King's head.", "踩晕倒的新芽王的头。", "기절한 새싹왕의 머리를 밟으세요.", "Pisa la cabeza del Rey Brote aturdido.", "気絶した新芽王の頭をふもう。");
            Add(t, "tutorial.boss_stomp_sub", "When he isn't stunned, you only bounce off.", "它没晕的时候，踩上去只会被弹开。", "기절하지 않았을 때는 밟아도 튕겨 나기만 해요.", "Si no está aturdido, solo rebotas.", "気絶していないときは、ふんでもはね返されるだけです。");
            Add(t, "tutorial.boss_rain", "Watch the shadows on the ground.", "注意地上的影子。", "바닥의 그림자를 잘 보세요.", "Fíjate en las sombras del suelo.", "地面の影をよく見よう。");
        }

        private static void Add(Dictionary<string, string[]> t, string key, params string[] values) => t[key] = values;
    }
}
