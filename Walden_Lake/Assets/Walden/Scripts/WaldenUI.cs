using UnityEngine;

namespace Walden
{
    /// <summary>A small, resolution-independent journal and HUD. No prefab or UI package setup required.</summary>
    public sealed class WaldenUI : MonoBehaviour
    {
        private WaldenGame game;
        private Font font;
        private GUIStyle title, heading, body, small, tiny, centered, button, number;
        private Texture2D gradient;
        private readonly Color paper = new Color(.95f, .93f, .85f);
        private readonly Color muted = new Color(.71f, .77f, .72f);
        private readonly Color ink = new Color(.065f, .105f, .105f);
        private readonly Color accent = new Color(.83f, .75f, .54f);
        private float width;

        public void Initialize(WaldenGame value)
        {
            game = value;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Microsoft YaHei UI", "Noto Sans CJK SC", "SimHei", "Arial" }, 20);
            gradient = new Texture2D(128, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < 128; x++) gradient.SetPixel(x, 0, new Color(ink.r, ink.g, ink.b, Mathf.Lerp(.94f, 0, Mathf.SmoothStep(0, 1, x / 127f))));
            gradient.Apply();
        }

        private GUIStyle Style(int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            return new GUIStyle(GUI.skin.label)
            {
                font = font, fontSize = size, alignment = anchor, wordWrap = true,
                normal = { textColor = color }, padding = new RectOffset(0, 0, 0, 0), richText = false
            };
        }

        private void PrepareStyles()
        {
            if (title != null) return;
            title = Style(56, paper);
            heading = Style(27, paper);
            body = Style(17, paper);
            small = Style(14, muted);
            tiny = Style(11, muted);
            centered = Style(16, paper, TextAnchor.MiddleCenter);
            number = Style(24, paper, TextAnchor.MiddleRight);
            button = Style(17, ink, TextAnchor.MiddleCenter);
        }

        private void OnGUI()
        {
            if (game == null || game.State == null) return;
            PrepareStyles();
            float scale = Screen.height / 720f;
            width = Screen.width / scale;
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            if (!game.Started) Welcome();
            else
            {
                Hud();
                if (game.Paused) Pause();
                else if (game.ShowJournal) Journal();
                else if (game.ShowCooking) Cooking();
                else if (!game.Sleeping) Interaction();
            }
            if (!string.IsNullOrEmpty(game.Toast) && Time.unscaledTime < game.ToastUntil && game.Started && !game.Paused && !game.ShowJournal && !game.ShowCooking && !game.Sleeping)
            {
                float alpha = Mathf.Clamp01((game.ToastUntil - Time.unscaledTime) * 1.5f);
                GUI.color = new Color(1, 1, 1, alpha);
                float toastWidth = Mathf.Min(650, width - 56);
                Box(new Rect(width / 2 - toastWidth / 2, 520, toastWidth, 48), new Color(ink.r, ink.g, ink.b, .79f));
                GUI.Label(new Rect(width / 2 - toastWidth / 2 + 15, 521, toastWidth - 30, 46), game.Toast, centered);
                GUI.color = Color.white;
            }
            if (game.Fade > 0)
            {
                Box(new Rect(0, 0, width, 720), new Color(.025f, .045f, .055f, game.Fade));
                if (game.Fade > .5f)
                {
                    GUI.color = new Color(1, 1, 1, (game.Fade - .5f) * 2);
                    GUI.Label(new Rect(0, 295, width, 42), "把今天轻轻放下", new GUIStyle(heading) { alignment = TextAnchor.MiddleCenter });
                    GUI.Label(new Rect(0, 353, width, 40), game.LastDay, centered);
                    GUI.color = Color.white;
                }
            }
            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
        }

        private void Welcome()
        {
            GUI.DrawTexture(new Rect(0, 0, Mathf.Min(width, 1050), 720), gradient);
            GUI.Label(new Rect(65, 69, 450, 24), "W A L D E N   /   A   Q U I E T   D A Y", tiny);
            Line(65, 110, 40, accent);
            GUI.Label(new Rect(60, 145, 500, 148), "瓦尔登\n湖畔的一天", title);
            GUI.Label(new Rect(65, 327, 450, 68), "一间小屋，一片菜园，一湖晨光。\n拾柴、钓鱼、做饭，把日子过得慢一点。", body);
            GUI.Label(new Rect(65, 426, 450, 38), "第一人称 · 简单生活 · 菜园、炉火与湖岸", small);
            if (Button(new Rect(65, 487, 310, 55), game.HasSave ? $"继续第 {game.State.day} 天的生活    →" : "开始湖畔生活    →", true)) game.Begin();
            GUI.Label(new Rect(65, 568, 520, 72), "WASD 移动    鼠标环顾    E 互动 / 收线\nF 喝茶 / 吃饭    X 结束垂钓\nTab 生活手记    Esc 暂停", small);
            GUI.Label(new Rect(65, 674, 440, 20), "灵感来自亨利·戴维·梭罗的《瓦尔登湖》", tiny);
            GUI.Label(new Rect(width - 280, 674, 220, 20), "2026  /  可玩原型 02", new GUIStyle(tiny) { alignment = TextAnchor.UpperRight });
        }

        private void Hud()
        {
            Box(new Rect(28, 27, 220, 66), new Color(ink.r, ink.g, ink.b, .61f));
            GUI.Label(new Rect(46, 38, 175, 23), "瓦尔登  /  湖畔生活", body);
            int hour = Mathf.FloorToInt(game.State.hour);
            int minute = Mathf.FloorToInt((game.State.hour - hour) * 60);
            GUI.Label(new Rect(46, 66, 190, 21), $"第 {game.State.day:00} 天     {hour:00}:{minute:00}   {Period()}", tiny);
            float resourceX = width - 346;
            Box(new Rect(resourceX, 27, 318, 99), new Color(ink.r, ink.g, ink.b, .68f));
            GUI.Label(new Rect(resourceX + 17, 38, 284, 18), "小 屋 储 备", tiny);
            GUI.Label(new Rect(resourceX + 17, 61, 284, 25), $"柴火  {game.State.wood}/{WaldenGame.WoodCapacity}     清水  {game.State.water}/{WaldenGame.WaterCapacity}", body);
            GUI.Label(new Rect(resourceX + 17, 94, 284, 23), $"蔬菜 {game.State.vegetables}/{WaldenGame.VegetableCapacity}   湖鱼 {game.State.fish}/{WaldenGame.FishCapacity}   热汤 {game.State.meals}/{WaldenGame.MealCapacity}", small);
            if (game.IsPlaying)
            {
                if (!game.IsFishing) Box(new Rect(width / 2 - 2, 358, 4, 4), new Color(paper.r, paper.g, paper.b, .82f));
                float progressY = 137;
                if (game.State.brewSeconds > 0)
                {
                    Progress(resourceX, progressY, "烧一壶茶", game.State.brewSeconds, WaldenGame.BrewDuration);
                    progressY += 48;
                }
                else if (game.State.teaReady)
                {
                    GUI.Label(new Rect(resourceX + 12, progressY, 300, 25), "窗边桌上备好了一杯热茶", small);
                    progressY += 31;
                }
                if (game.State.cookSeconds > 0)
                    Progress(resourceX, progressY, game.State.cookingRecipe == 2 ? "熬一锅湖鱼汤" : "熬一锅蔬菜汤", game.State.cookSeconds, WaldenGame.CookDuration);
                else if (game.State.meals > 0)
                    GUI.Label(new Rect(resourceX + 12, progressY, 300, 25), $"窗边桌上有 {game.State.meals} 份热汤，可以端起享用", small);
                bool roomForHelp = width >= 1120 || (!game.Focus && !game.Player.IsSeated);
                if (game.ShowHelp && roomForHelp && !game.IsFishing)
                {
                    Box(new Rect(28, 589, 277, 100), new Color(ink.r, ink.g, ink.b, .61f));
                    GUI.Label(new Rect(45, 599, 240, 42), FirstStep(), small);
                    GUI.Label(new Rect(45, 648, 250, 33), "WASD 行走  ·  E 互动  ·  Shift 快走\nTab 手记  ·  Esc 暂停  ·  H 收起提示", tiny);
                }
                if (game.State.carryingTea || game.State.carryingMeal)
                {
                    Box(new Rect(width - 220, 634, 192, 55), new Color(ink.r, ink.g, ink.b, .65f));
                    Key(new Rect(width - 205, 647, 28, 28), "F");
                    GUI.Label(new Rect(width - 165, 649, 130, 30), game.State.carryingMeal ? "吃一碗温热的汤" : "慢慢喝一口茶", small);
                }
                if (game.State.hour >= 21.9f)
                    GUI.Label(new Rect(width / 2 - 200, 168, 400, 30), "夜深了。小屋里的床一直在等你。", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
            }
        }

        private void Interaction()
        {
            if (game.IsFishing)
            {
                Fishing();
                return;
            }
            if (game.Player.IsSeated)
            {
                Box(new Rect(width / 2 - 160, 627, 320, 57), new Color(ink.r, ink.g, ink.b, .76f));
                Key(new Rect(width / 2 - 143, 642, 28, 28), "E");
                GUI.Label(new Rect(width / 2 - 101, 643, 240, 29), "起身    ·    鼠标环顾风景", small);
                return;
            }
            if (!game.Focus) return;
            float panelWidth = 530;
            Box(new Rect(width / 2 - panelWidth / 2, 615, panelWidth, 69), new Color(ink.r, ink.g, ink.b, .86f));
            Key(new Rect(width / 2 - 245, 636, 30, 30), "E");
            GUI.Label(new Rect(width / 2 - 199, 625, 435, 19), game.Focus.label, tiny);
            GUI.Label(new Rect(width / 2 - 199, 648, 435, 30), game.Prompt(game.Focus), small);
        }

        private void Progress(float x, float y, string label, float remaining, float duration)
        {
            Box(new Rect(x, y, 318, 42), new Color(ink.r, ink.g, ink.b, .62f));
            GUI.Label(new Rect(x + 13, y + 6, 292, 24), $"{label}   ·   {Mathf.CeilToInt(remaining)} 秒", small);
            Line(x + 13, y + 34, 292, new Color(1, 1, 1, .15f), 2);
            Line(x + 13, y + 34, 292 * Mathf.Clamp01(1 - remaining / duration), accent, 2);
        }

        private void Fishing()
        {
            float x = width / 2 - 280;
            Box(new Rect(x, 574, 560, 111), new Color(ink.r, ink.g, ink.b, .9f));
            string message = game.FishBiting ? "浮漂动了！现在收线吧。" : "让浮漂在水面上轻轻晃一会儿。";
            GUI.Label(new Rect(x + 20, 585, 520, 28), message, new GUIStyle(centered) { normal = { textColor = game.FishBiting ? accent : paper } });
            float remaining = game.FishBiting ? game.BiteRemaining : game.FishingRemaining;
            float progress = game.FishBiting ? Mathf.Clamp01(remaining / 6f) : 1 - Mathf.Clamp01(remaining / 12f);
            Line(x + 27, 622, 506, new Color(1, 1, 1, .15f), 3);
            Line(x + 27, 622, 506 * progress, accent, 3);
            Key(new Rect(x + 71, 642, 28, 28), "E");
            GUI.Label(new Rect(x + 111, 644, 193, 26), game.FishBiting ? $"收线 · 还剩 {Mathf.CeilToInt(remaining)} 秒" : "提前收线 · 这次无鱼", small);
            Key(new Rect(x + 333, 642, 28, 28), "X");
            GUI.Label(new Rect(x + 373, 644, 140, 26), "结束垂钓", small);
        }

        private void Cooking()
        {
            Scrim();
            const float panelWidth = 760;
            float x = width / 2 - panelWidth / 2;
            Box(new Rect(x, 65, panelWidth, 591), new Color(ink.r, ink.g, ink.b, .97f));
            GUI.Label(new Rect(x + 34, 90, 690, 22), "T H E   C A B I N   K I T C H E N", tiny);
            GUI.Label(new Rect(x + 34, 126, 690, 44), "在炉边做一顿热饭", heading);
            GUI.Label(new Rect(x + 34, 181, 690, 28), $"手边有：蔬菜 {game.State.vegetables}    湖鱼 {game.State.fish}    清水 {game.State.water}    柴火 {game.State.wood}    /    热汤 {game.State.meals}/{WaldenGame.MealCapacity}", small);

            Recipe(new Rect(x + 34, 227, 335, 222), 1, "田园蔬菜汤", "两份蔬菜的清甜，刚好够一顿饭。", "蔬菜 ×2\n清水 ×1    柴火 ×1", game.State.vegetables >= 2 && game.State.water >= 1 && game.State.wood >= 1);
            Recipe(new Rect(x + 391, 227, 335, 222), 2, "湖岸鲜鱼汤", "把今天的收获，慢慢熬进一碗汤里。", "湖鱼 ×1    蔬菜 ×1\n清水 ×1    柴火 ×1", game.State.fish >= 1 && game.State.vegetables >= 1 && game.State.water >= 1 && game.State.wood >= 1);

            string note = game.State.cookSeconds > 0
                ? $"锅里正在熬汤，还需 {Mathf.CeilToInt(game.State.cookSeconds)} 秒。合上菜单后继续。"
                : "每份汤熬煮约 12 秒。做好后在窗边桌上端起，按 F 慢慢享用。";
            GUI.Label(new Rect(x + 34, 467, 692, 33), note, small);
            if (!string.IsNullOrEmpty(game.Toast) && Time.unscaledTime < game.ToastUntil)
                GUI.Label(new Rect(x + 34, 506, 692, 41), game.Toast, centered);
            if (Button(new Rect(x + 34, 565, 692, 49), "合上食谱，回到小屋", true)) game.CloseCooking();
            GUI.Label(new Rect(x + 34, 631, 692, 19), "Esc 合上食谱   ·   不急着吃饭，日子也会安静地继续。", tiny);
        }

        private void Recipe(Rect rect, int recipe, string name, string description, string ingredients, bool enough)
        {
            Box(rect, new Color(.13f, .19f, .18f));
            GUI.Label(new Rect(rect.x + 19, rect.y + 16, rect.width - 38, 34), name, new GUIStyle(heading) { fontSize = 23 });
            GUI.Label(new Rect(rect.x + 19, rect.y + 61, rect.width - 38, 37), description, small);
            GUI.Label(new Rect(rect.x + 19, rect.y + 106, rect.width - 38, 49), ingredients, body);
            string label = game.State.cookSeconds > 0 ? "炉上的汤正在慢慢煮" : enough ? "添柴开火    →" : "查看缺少的食材";
            if (Button(new Rect(rect.x + 19, rect.y + 165, rect.width - 38, 40), label, enough && game.State.cookSeconds <= 0))
                game.StartCooking(recipe);
        }

        private void Pause()
        {
            Scrim();
            Rect panel = new Rect(width / 2 - 220, 104, 440, 516);
            Box(panel, new Color(ink.r, ink.g, ink.b, .97f));
            float x = panel.x + 35;
            GUI.Label(new Rect(x, 137, 370, 43), "在湖边停一会儿", heading);
            GUI.Label(new Rect(x, 189, 370, 28), "时间、炉火和湖上的浮漂，都在这里等你。", small);
            Line(x, 236, 370, new Color(.7f, .75f, .7f, .18f));
            GUI.Label(new Rect(x, 261, 370, 28), $"环境音量   {Mathf.RoundToInt(game.Volume * 100)}%", body);
            float volume = GUI.HorizontalSlider(new Rect(x, 300, 370, 20), game.Volume, 0, 1);
            if (Mathf.Abs(volume - game.Volume) > .001f) game.SetVolume(volume);
            GUI.Label(new Rect(x, 338, 370, 28), "鼠标灵敏度", body);
            float sensitivity = GUI.HorizontalSlider(new Rect(x, 377, 370, 20), game.Player.Sensitivity, .035f, .25f);
            if (Mathf.Abs(sensitivity - game.Player.Sensitivity) > .0001f) game.SetSensitivity(sensitivity);
            if (Button(new Rect(x, 428, 370, 49), "回到湖边", true)) game.SetPaused(false);
            if (Button(new Rect(x, 490, 177, 45), "保存进度")) { game.Save(true); game.SetPaused(false); }
            if (Button(new Rect(x + 193, 490, 177, 45), "保存并退出")) game.Quit();
            GUI.Label(new Rect(x, 565, 370, 23), "睡眠、每分钟与退出时自动保存   /   F5 手动保存", tiny);
        }

        private void Journal()
        {
            Scrim();
            Rect panel = new Rect(width / 2 - 380, 38, 760, 639);
            Box(panel, new Color(ink.r, ink.g, ink.b, .97f));
            float x = panel.x + 34;
            float rightX = x + 358;
            GUI.Label(new Rect(x, 65, 692, 22), $"L A K E S I D E   N O T E S    /    第 {game.State.day:00} 天", tiny);
            GUI.Label(new Rect(x, 101, 692, 43), "生活手记", heading);
            GUI.Label(new Rect(x, 161, 692, 61), $"今天拾了 {game.State.branchesToday} 束落枝，取了 {game.State.waterToday} 壶清水，喝了 {game.State.teaToday} 杯茶。\n收获 {game.State.harvestToday} 份蔬菜，钓到 {game.State.fishToday} 条湖鱼，吃了 {game.State.mealsToday} 碗热汤。", body);
            GUI.Label(new Rect(x, 229, 692, 24), $"至今的日常：喝茶 {game.State.totalTea} 次 · 收获 {game.State.totalHarvest} 份蔬菜 · 钓鱼 {game.State.totalFish} 条 · 用餐 {game.State.totalMeals} 次", small);
            Line(x, 269, 692, accent);

            GUI.Label(new Rect(x, 292, 326, 28), "小屋里还有", body);
            GUI.Label(new Rect(x, 332, 326, 30), $"柴火  {game.State.wood}/12     清水  {game.State.water}/3", body);
            GUI.Label(new Rect(x, 373, 326, 52), $"蔬菜  {game.State.vegetables}/12     湖鱼  {game.State.fish}/6\n热汤  {game.State.meals}/6", body);
            GUI.Label(new Rect(x, 446, 326, 79), "落枝每天重新出现，储备留到次日。\n初到湖边有两份蔬菜，可先尝试做汤。\n端起茶或热汤，找个位置按 F 享用。", small);

            GUI.Label(new Rect(rightX, 292, 334, 28), "门前的三块菜畦", body);
            for (int i = 0; i < WaldenGame.PlotCount; i++)
            {
                float y = 333 + i * 37;
                GUI.Label(new Rect(rightX, y, 334, 35), $"菜畦 {i + 1}   {game.GardenStatus(i)}", small);
            }
            GUI.Label(new Rect(rightX, 457, 334, 80), "播种后，每天可用一份清水浇灌。\n两个浇过水的夜晚后成熟，收获两份蔬菜。\n忘记浇水时只会停长，随时可以接着照料。", small);
            GUI.Label(new Rect(x, 548, 692, 25), "菜园、钓鱼、做饭，或只是在湖边散步。今天由你安排。", small);
            if (Button(new Rect(x, 588, 692, 49), "合上手记，继续今天", true)) game.CloseJournal();
            GUI.Label(new Rect(x, 652, 692, 18), "Tab / Esc 合上手记", tiny);
        }

        private string FirstStep()
        {
            if (game.State.carryingMeal) return "找个喜欢的位置，按 F 吃一碗汤。";
            if (game.State.carryingTea) return "找个座位坐下，按 F 喝茶。";
            if (game.State.cookSeconds > 0) return "汤正在慢慢煮，先在屋里坐坐。";
            if (game.State.meals > 0 && game.State.totalMeals == 0) return "热汤备好了，去窗边桌上端一碗。";
            if (game.State.totalTea > 0 && game.State.totalMeals > 0) return "菜园、钓鱼、散步，今天由你安排。";
            if (game.State.teaReady) return "热茶备好了，去窗边桌上端起它。";
            if (game.State.brewSeconds > 0) return "水正在烧开，听听炉火吧。";
            if (game.State.wood == 0) return "林间有可以拾起的落枝。";
            if (game.State.water == 0) return "沿小路去码头，装一壶湖水。";
            if (game.State.totalTea > 0) return "在料理台用两份蔬菜煮一碗汤。";
            return "回小屋，给炉灶添柴烧茶。";
        }

        private string Period()
        {
            if (game.State.hour < 10) return "清晨";
            if (game.State.hour < 15) return "白昼";
            if (game.State.hour < 18.5f) return "午后";
            if (game.State.hour < 20) return "黄昏";
            return "夜晚";
        }

        private void Scrim() => Box(new Rect(0, 0, width, 720), new Color(.025f, .045f, .05f, .64f));

        private bool Button(Rect rect, string text, bool primary = false)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            Color background = primary ? (hover ? paper : accent) : new Color(.17f, .23f, .22f);
            Box(rect, background);
            if (!primary) Line(rect.x, rect.yMax - 1, rect.width, new Color(.6f, .65f, .6f, .27f));
            GUIStyle current = new GUIStyle(button) { normal = { textColor = primary ? ink : paper } };
            return GUI.Button(rect, text, current);
        }

        private void Key(Rect rect, string text)
        {
            Box(rect, new Color(.9f, .88f, .76f, .13f));
            Line(rect.x, rect.yMax - 1, rect.width, accent);
            GUI.Label(rect, text, centered);
        }

        private static void Line(float x, float y, float length, Color color, float height = 1) => Box(new Rect(x, y, length, height), color);

        private static void Box(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color *= color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void OnDestroy()
        {
            if (gradient) Destroy(gradient);
            if (font) Destroy(font);
        }
    }
}
