using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Walden
{
    public sealed class WaldenGame : MonoBehaviour
    {
        public const int WoodCapacity = 12;
        public const int WaterCapacity = 3;
        public const float BrewDuration = 9f;
        public const int PlotCount = 3;
        public const int VegetableCapacity = 12;
        public const int FishCapacity = 6;
        public const int MealCapacity = 6;
        public const float CookDuration = 12f;
        [Range(3, 30)] public float dayLengthMinutes = 12;

        public Light StoveLight;
        public Transform Flame;
        public ParticleSystem Steam;
        public GameObject TeaVisual;
        public Renderer LakeSurface;
        public GameObject[] GardenSeedlings = new GameObject[PlotCount];
        public GameObject[] GardenMature = new GameObject[PlotCount];
        public Renderer[] GardenSoils = new Renderer[PlotCount];
        public ParticleSystem CookingSteam;
        public GameObject MealVisual;
        public Transform FishingBobber;
        public GameObject FishingRod;
        public LineRenderer FishingLine;
        public Transform RodTip;
        public Vector3 SpawnPosition;
        public float SpawnYaw;

        public WaldenState State { get; private set; }
        public WaldenPlayer Player { get; private set; }
        public WaldenAtmosphere Atmosphere { get; private set; }
        public WaldenInteractable Focus { get; private set; }
        public readonly List<WaldenInteractable> Interactables = new List<WaldenInteractable>();
        public bool Started { get; private set; }
        public bool Paused { get; private set; }
        public bool Sleeping { get; private set; }
        public bool ShowJournal { get; private set; }
        public bool ShowCooking { get; private set; }
        public bool IsFishing { get; private set; }
        public bool FishBiting { get; private set; }
        public float FishingRemaining { get; private set; }
        public float BiteRemaining { get; private set; }
        public bool HasSave { get; private set; }
        public string Toast { get; private set; }
        public float ToastUntil { get; private set; }
        public string LastDay { get; private set; }
        public float Fade { get; private set; }
        public float Volume { get; private set; } = .65f;
        public bool ShowHelp { get; private set; } = true;
        public string SavePathOverride { get; set; }
        public bool IsPlaying => Started && !Paused && !Sleeping && !ShowJournal && !ShowCooking;
        private float nextAutoSave;
        private Material lakeMaterial;
        private WaldenUI ui;
        private bool initialized;
        private bool smokeMode;
        private readonly Material[] gardenMaterials = new Material[PlotCount];
        private Vector3 bobberRest;
        private static readonly int RippleOffset = Shader.PropertyToID("_RippleOffset");

        private void Start()
        {
            smokeMode = Array.IndexOf(Environment.GetCommandLineArgs(), "-waldenSmokeTest") >= 0;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 1;
            if (smokeMode) Application.runInBackground = true;
            if (smokeMode) SavePathOverride = System.IO.Path.Combine(Application.temporaryCachePath, "walden-smoke.json");
            HasSave = !smokeMode && WaldenSave.Exists;
            bool recovered = false;
            State = HasSave ? WaldenSave.Read(WaldenSave.FilePath, out recovered) : new WaldenState();
            State.Sanitize();
            WaldenWorld.Build(this);
            for (int i = 0; i < PlotCount; i++)
                if (GardenSoils[i]) gardenMaterials[i] = GardenSoils[i].material;
            if (FishingBobber) bobberRest = FishingBobber.position;
            Player = new GameObject("Player — first person").AddComponent<WaldenPlayer>();
            Player.Initialize(SpawnPosition, SpawnYaw);
            Atmosphere = new GameObject("Lake ambience & daylight").AddComponent<WaldenAtmosphere>();
            Atmosphere.Initialize(Player.View);
            Volume = PlayerPrefs.GetFloat("Walden.Volume", .65f);
            Player.Sensitivity = PlayerPrefs.GetFloat("Walden.Sensitivity", .12f);
            Atmosphere.SetVolume(Volume);
            ui = gameObject.AddComponent<WaldenUI>();
            ui.Initialize(this);
            if (LakeSurface) lakeMaterial = LakeSurface.material;
            ApplyCollectedBranches();
            ApplyVisuals();
            Atmosphere.Apply(State.hour, State.fireSeconds > 0 || State.cookSeconds > 0, StovePosition());
            SetCursor(false);
            initialized = true;
            if (recovered) Notify("存档已恢复。今天也可以慢慢来。", 8);
            if (smokeMode) gameObject.AddComponent<WaldenSmokeTest>();
        }

        public WaldenInteractable Register(InteractionKind kind, GameObject target, string label, Vector3 seatPosition = default, float seatYaw = 0)
        {
            var item = target.AddComponent<WaldenInteractable>();
            item.kind = kind;
            item.label = label;
            item.seatPosition = seatPosition;
            item.seatYaw = seatYaw;
            Interactables.Add(item);
            return item;
        }

        private void Update()
        {
            if (!initialized) return;
            var keyboard = Keyboard.current;
            if (Started && !Sleeping && keyboard != null && !smokeMode)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    if (ShowCooking) CloseCooking();
                    else if (ShowJournal) CloseJournal();
                    else SetPaused(!Paused);
                }
                if (keyboard.tabKey.wasPressedThisFrame)
                {
                    if (ShowJournal) CloseJournal();
                    else if (IsPlaying) OpenJournal();
                }
                if (IsPlaying && keyboard.hKey.wasPressedThisFrame) ShowHelp = !ShowHelp;
                if (IsPlaying && keyboard.f5Key.wasPressedThisFrame) Save(true);
            }
            Player.Tick(IsPlaying && !smokeMode);
            if (IsPlaying)
            {
                Advance(Time.deltaTime);
                RefreshFocus();
                if (!smokeMode && keyboard != null && keyboard.eKey.wasPressedThisFrame)
                {
                    if (IsFishing) ReelFishing();
                    else if (Player.IsSeated) Player.Stand();
                    else if (Focus) Interact(Focus);
                }
                if (!smokeMode && keyboard != null && keyboard.fKey.wasPressedThisFrame)
                {
                    if (State.carryingMeal) EatMeal();
                    else DrinkTea();
                }
                if (!smokeMode && keyboard != null && keyboard.xKey.wasPressedThisFrame && IsFishing) CancelFishing();
                if (Time.unscaledTime >= nextAutoSave)
                {
                    Save(false);
                    nextAutoSave = Time.unscaledTime + 60;
                }
            }
            else Focus = null;
            ApplyVisuals();
            Atmosphere.Apply(State.hour, State.fireSeconds > 0 || State.cookSeconds > 0, StovePosition());
            if (lakeMaterial && lakeMaterial.HasProperty(RippleOffset))
                lakeMaterial.SetFloat(RippleOffset, Time.time * .13f);
        }

        public void Begin()
        {
            Started = true;
            Paused = false;
            ShowJournal = false;
            ShowCooking = false;
            Time.timeScale = 1;
            SetCursor(!smokeMode);
            nextAutoSave = Time.unscaledTime + 60;
            Notify(HasSave ? "又回到了湖边。欢迎回家。" : "沿着林间小路拾些落枝，再去码头取一壶水。", 7);
        }

        public void SetPaused(bool value)
        {
            if (!Started || Sleeping) return;
            Paused = value;
            Time.timeScale = value || ShowJournal || ShowCooking ? 0 : 1;
            SetCursor(!value && !ShowJournal && !ShowCooking && !smokeMode);
        }

        public void OpenJournal()
        {
            if (!IsPlaying) return;
            ShowJournal = true;
            Time.timeScale = 0;
            SetCursor(false);
        }

        public void CloseJournal()
        {
            ShowJournal = false;
            Time.timeScale = Paused ? 0 : 1;
            SetCursor(Started && !Paused && !smokeMode);
        }

        public void Advance(float seconds)
        {
            if (seconds <= 0) return;
            State.hour = Mathf.Min(22, State.hour + seconds * (15f / (dayLengthMinutes * 60f)));
            State.fireSeconds = Mathf.Max(0, State.fireSeconds - seconds);
            if (State.brewSeconds > 0)
            {
                State.brewSeconds = Mathf.Max(0, State.brewSeconds - seconds);
                if (State.brewSeconds == 0)
                {
                    State.teaReady = true;
                    Notify("水开了。窗边桌上的热茶已经备好。", 5);
                }
            }
            if (State.cookSeconds > 0)
            {
                State.cookSeconds = Mathf.Max(0, State.cookSeconds - seconds);
                if (State.cookSeconds == 0) FinishCooking();
            }
            AdvanceFishing(seconds);
        }

        private void RefreshFocus()
        {
            Focus = null;
            if (Player.IsSeated) return;
            var ray = new Ray(Player.View.transform.position, Player.View.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, 3.1f, ~((1 << 2)), QueryTriggerInteraction.Ignore))
                Focus = hit.collider.GetComponentInParent<WaldenInteractable>();
        }

        public string Prompt(WaldenInteractable item)
        {
            switch (item.kind)
            {
                case InteractionKind.Branch: return State.wood >= WoodCapacity ? "柴枝已经装满了" : "拾起落枝 · 柴火 +1";
                case InteractionKind.Water: return State.water >= WaterCapacity ? "水壶已经装满了" : "装一份湖水 · 清水 +1";
                case InteractionKind.Stove:
                    if (State.brewSeconds > 0) return "水正在慢慢烧开…";
                    if (State.teaReady || State.carryingTea) return "先享用已经备好的热茶";
                    return "生火烧茶 · 1 柴火 + 1 清水";
                case InteractionKind.Tea: return State.teaReady ? "端起热茶 · 找个喜欢的位置坐坐" : "茶盘 · 在炉灶生火烧一壶茶";
                case InteractionKind.Seat: return "坐下 · 看一会儿风景";
                case InteractionKind.Bed: return "睡到明早 · 保存这一天";
                case InteractionKind.Storage: return "翻开生活手记";
                case InteractionKind.GardenPlot:
                    if (item.resourceId < 0 || item.resourceId >= PlotCount) return "查看菜畦";
                    var plot = State.gardenPlots[item.resourceId];
                    if (!plot.planted) return "播种萝卜 · 种子已经备好";
                    if (plot.growth >= 2) return "收获萝卜 · 蔬菜 +2";
                    return plot.lastWateredDay == State.day ? "今天已浇水 · 等待夜晚生长" : "给幼苗浇水 · 消耗 1 清水";
                case InteractionKind.Cooking: return State.cookSeconds > 0 ? "查看正在炖煮的热汤" : "打开食谱 · 蔬菜汤 / 湖鱼汤";
                case InteractionKind.Meal: return State.meals > 0 ? "端起一碗热汤 · 找个喜欢的位置享用" : "餐盘 · 先在料理台做一碗汤";
                case InteractionKind.Fishing: return State.fish >= FishCapacity ? "鱼篓已经装满了" : "坐到码头垂钓 · 等待浮漂的动静";
                default: return item.label;
            }
        }

        public bool Interact(WaldenInteractable item)
        {
            if (!item || !item.gameObject.activeInHierarchy || !IsPlaying) return false;
            if (IsFishing) { Notify("先按 E 收线，或按 X 收好鱼竿。"); return false; }
            bool acted = false;
            switch (item.kind)
            {
                case InteractionKind.Branch:
                    if (State.wood >= WoodCapacity) { Notify("柴火够用了，先回家放松一下吧。"); break; }
                    if (State.collectedBranches.Contains(item.resourceId)) break;
                    State.wood++;
                    State.branchesToday++;
                    State.collectedBranches.Add(item.resourceId);
                    item.gameObject.SetActive(false);
                    Notify("拾起一束干燥的落枝。", 3);
                    acted = true;
                    break;
                case InteractionKind.Water:
                    if (State.water >= WaterCapacity) { Notify("水壶已满，可以回屋烧茶了。"); break; }
                    State.water++;
                    State.waterToday++;
                    Notify("湖水装进了水壶。回屋烧开后，便能泡茶。", 4);
                    acted = true;
                    break;
                case InteractionKind.Stove:
                    if (State.brewSeconds > 0) { Notify("听听炉火的声音，再等一会儿。"); break; }
                    if (State.teaReady || State.carryingTea) { Notify("已经有一杯热茶在等你了。"); break; }
                    if (State.wood < 1 || State.water < 1) { Notify("需要 1 份柴火和 1 份清水。落枝在林间，取水点在码头。"); break; }
                    State.wood--;
                    State.water--;
                    State.brewSeconds = BrewDuration;
                    State.fireSeconds = 90;
                    Notify("炉火燃起。壶里的水很快就会烧开。", 4);
                    acted = true;
                    break;
                case InteractionKind.Tea:
                    if (!State.teaReady) { Notify("先在旁边的炉灶生火烧水吧。"); break; }
                    if (State.carryingMeal) { Notify("先按 F 享用手中的热汤，再来端茶。"); break; }
                    State.teaReady = false;
                    State.carryingTea = true;
                    Notify("端起热茶。可以去窗边或湖边坐下，按 F 慢慢喝。", 6);
                    acted = true;
                    break;
                case InteractionKind.Seat:
                    Player.Sit(item.seatPosition, item.seatYaw);
                    Notify(State.carryingMeal ? "坐下来慢慢吃。按 F 享用热汤，按 E 起身。" : State.carryingTea ? "湖面正轻轻晃动。按 F 喝茶，按 E 起身。" : "在这里停留一会儿。按 E 起身。", 5);
                    acted = true;
                    break;
                case InteractionKind.Bed:
                    StartCoroutine(Sleep());
                    acted = true;
                    break;
                case InteractionKind.Storage:
                    OpenJournal();
                    acted = true;
                    break;
                case InteractionKind.GardenPlot:
                    acted = TendGarden(item.resourceId);
                    break;
                case InteractionKind.Cooking:
                    OpenCooking();
                    acted = ShowCooking;
                    break;
                case InteractionKind.Meal:
                    acted = TakeMeal();
                    break;
                case InteractionKind.Fishing:
                    acted = StartFishing(item);
                    break;
            }
            if (acted) Atmosphere.PlayCue(item.kind);
            return acted;
        }

        public bool DrinkTea()
        {
            if (!IsPlaying || !State.carryingTea) return false;
            State.carryingTea = false;
            State.teaToday++;
            State.totalTea++;
            Atmosphere.PlayCue(InteractionKind.Tea);
            Notify(Player.IsSeated ? "一口温热的茶，一段属于自己的安静时光。" : "热茶暖了手心。也许下次可以坐下来慢慢喝。", 7);
            return true;
        }

        public string GardenStatus(int index)
        {
            if (index < 0 || index >= PlotCount) return "菜畦";
            var plot = State.gardenPlots[index];
            if (!plot.planted) return "空地 · 可以播种";
            if (plot.growth >= 2) return "萝卜成熟了 · 可收获 2 份蔬菜";
            string care = plot.lastWateredDay == State.day ? "今天已浇水" : "还需要浇水";
            return $"生长 {plot.growth}/2 · {care}";
        }

        private bool TendGarden(int index)
        {
            if (index < 0 || index >= PlotCount) return false;
            var plot = State.gardenPlots[index];
            if (!plot.planted)
            {
                plot.planted = true;
                plot.growth = 0;
                plot.lastWateredDay = 0;
                Notify("萝卜种下了。再按 E 浇一份水，过夜后就会长大一点。", 5);
                return true;
            }
            if (plot.growth >= 2)
            {
                if (State.vegetables + 2 > VegetableCapacity) { Notify("蔬菜架需要留出 2 个位置，先回屋煮一碗汤吧。"); return false; }
                State.vegetables += 2;
                State.harvestToday += 2;
                State.totalHarvest += 2;
                plot.planted = false;
                plot.growth = plot.lastWateredDay = 0;
                Notify("收下两份新鲜萝卜。空菜畦现在可以重新播种。", 5);
                return true;
            }
            if (plot.lastWateredDay == State.day) { Notify("这块菜畦今天已经喝饱水了，等到明早看看吧。"); return false; }
            if (State.water < 1) { Notify("浇水需要 1 份清水，先去码头装一壶水。"); return false; }
            State.water--;
            plot.lastWateredDay = State.day;
            Notify("给菜苗浇了水。经历两个浇过水的夜晚，就可以收获。", 5);
            return true;
        }

        public void OpenCooking()
        {
            if (!IsPlaying || IsFishing) return;
            ShowCooking = true;
            Focus = null;
            Time.timeScale = 0;
            SetCursor(false);
        }

        public void CloseCooking()
        {
            ShowCooking = false;
            Time.timeScale = Paused || ShowJournal ? 0 : 1;
            SetCursor(Started && !Paused && !ShowJournal && !smokeMode);
        }

        public bool StartCooking(int recipe)
        {
            if (!Started || Paused || Sleeping || ShowJournal || IsFishing || (recipe != 1 && recipe != 2)) return false;
            if (State.cookSeconds > 0) { Notify("锅里已经有一份汤了，合上食谱，让它慢慢炖煮。"); return false; }
            if (State.meals >= MealCapacity) { Notify("餐架已经放满了，先享用一份热汤吧。"); return false; }
            int vegetables = recipe == 1 ? 2 : 1;
            int fish = recipe == 2 ? 1 : 0;
            if (State.wood < 1 || State.water < 1 || State.vegetables < vegetables || State.fish < fish)
            {
                Notify(recipe == 1 ? "蔬菜汤需要：2 蔬菜、1 清水、1 柴火。" : "湖鱼汤需要：1 条鱼、1 蔬菜、1 清水、1 柴火。", 6);
                return false;
            }
            State.wood--;
            State.water--;
            State.vegetables -= vegetables;
            State.fish -= fish;
            State.cookingRecipe = recipe;
            State.cookSeconds = CookDuration;
            State.fireSeconds = Mathf.Max(State.fireSeconds, 90);
            CloseCooking();
            Atmosphere.PlayCue(InteractionKind.Stove);
            Notify(recipe == 1 ? "新鲜蔬菜下锅了。十二秒后，窗边就有热汤可享用。" : "把湖鱼和蔬菜一起炖煮，十二秒后就能享用。", 6);
            return true;
        }

        private void FinishCooking()
        {
            if (State.cookingRecipe < 1 || State.cookingRecipe > 2) return;
            string dish = State.cookingRecipe == 1 ? "蔬菜汤" : "湖鱼汤";
            State.cookSeconds = 0;
            State.cookingRecipe = 0;
            State.meals = Mathf.Min(MealCapacity, State.meals + 1);
            Notify(dish + "做好了。去窗边餐盘端起一碗，按 F 慢慢享用。", 7);
        }

        public bool TakeMeal()
        {
            if (!IsPlaying || IsFishing) return false;
            if (State.carryingTea || State.carryingMeal) { Notify("先按 F 享用手中的茶或热汤，再来端一碗。"); return false; }
            if (State.meals <= 0) { Notify("餐盘还是空的。在炉灶旁的料理台选一道汤来做吧。"); return false; }
            State.meals--;
            State.carryingMeal = true;
            Notify("端起一碗热汤。找个位置坐下，按 F 慢慢享用。", 6);
            return true;
        }

        public bool EatMeal()
        {
            if (!IsPlaying || !State.carryingMeal) return false;
            State.carryingMeal = false;
            State.mealsToday++;
            State.totalMeals++;
            Atmosphere.PlayCue(InteractionKind.Meal);
            Notify("吃完一碗自己做的热汤。今天的生活又添了一点满足。", 6);
            return true;
        }

        private bool StartFishing(WaldenInteractable spot)
        {
            if (State.fish >= FishCapacity) { Notify("鱼篓已满，先回屋做一碗湖鱼汤吧。"); return false; }
            if (Player.IsSeated) Player.Stand();
            Player.Sit(spot.seatPosition, spot.seatYaw);
            IsFishing = true;
            FishBiting = false;
            FishingRemaining = UnityEngine.Random.Range(8f, 12f);
            BiteRemaining = 0;
            Focus = null;
            Notify("鱼线轻轻落进湖面。等浮漂下沉时按 E 收线，按 X 可以收竿。", 7);
            return true;
        }

        private void AdvanceFishing(float seconds)
        {
            if (!IsFishing) return;
            if (!FishBiting)
            {
                FishingRemaining -= seconds;
                if (FishingRemaining > 0) return;
                FishBiting = true;
                // Preserve elapsed time even if a long frame crosses both phases.
                BiteRemaining = 6f + FishingRemaining;
                FishingRemaining = 0;
                if (BiteRemaining > 0)
                {
                    Atmosphere.PlayCue(InteractionKind.Water);
                    Notify("浮漂动了！现在按 E 收线。", 6);
                }
            }
            else BiteRemaining -= seconds;
            if (BiteRemaining <= 0) FinishFishing("鱼又游回了湖里。随时可以再试一竿。", false);
        }

        public bool ReelFishing()
        {
            if (!IsPlaying || !IsFishing) return false;
            if (!FishBiting) { FinishFishing("收起了鱼线。下次等浮漂动起来再收线吧。", false); return false; }
            if (State.fish >= FishCapacity) { FinishFishing("鱼篓已满，把这条鱼轻轻放回湖里。", false); return false; }
            State.fish++;
            State.fishToday++;
            State.totalFish++;
            FinishFishing("钓到一条湖鱼。和一份蔬菜一起，可以炖成一碗湖鱼汤。", true);
            return true;
        }

        public void CancelFishing()
        {
            if (IsFishing) FinishFishing("收好鱼竿，继续今天的生活。", false);
        }

        private void FinishFishing(string message, bool caught)
        {
            IsFishing = FishBiting = false;
            FishingRemaining = BiteRemaining = 0;
            if (Player.IsSeated) Player.Stand();
            if (caught) Atmosphere.PlayCue(InteractionKind.Fishing);
            Notify(message, 6);
        }

        public IEnumerator Sleep()
        {
            if (Sleeping) yield break;
            Sleeping = true;
            Focus = null;
            SetCursor(false);
            LastDay = $"第 {State.day} 天，收获 {State.harvestToday} 份蔬菜，钓到 {State.fishToday} 条鱼，享用了 {State.mealsToday} 碗汤、{State.teaToday} 杯茶。";
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime) { Fade = t; yield return null; }
            Fade = 1;
            CompleteNight();
            yield return new WaitForSecondsRealtime(1.1f);
            for (float t = 1; t > 0; t -= Time.unscaledDeltaTime) { Fade = t; yield return null; }
            Fade = 0;
            Sleeping = false;
            SetCursor(true);
            Notify($"第 {State.day} 天。晨光又落在了小屋里。", 6);
        }

        public void CompleteNight()
        {
            if (IsFishing) CancelFishing();
            for (int i = 0; i < PlotCount; i++)
            {
                var plot = State.gardenPlots[i];
                if (plot.planted && plot.lastWateredDay == State.day) plot.growth = Mathf.Min(2, plot.growth + 1);
            }
            if (State.cookSeconds > 0) FinishCooking();
            State.day++;
            State.hour = 7.5f;
            if (State.brewSeconds > 0) State.teaReady = true;
            State.brewSeconds = 0;
            State.fireSeconds = 0;
            State.branchesToday = State.waterToday = State.teaToday = 0;
            State.harvestToday = State.fishToday = State.mealsToday = 0;
            State.collectedBranches.Clear();
            ApplyCollectedBranches();
            if (Player.IsSeated) Player.Stand();
            Player.Teleport(new Vector3(1.2f, .18f, 10.7f), 180);
            Save(false);
        }

        private void ApplyCollectedBranches()
        {
            foreach (var item in Interactables)
                if (item.kind == InteractionKind.Branch)
                    item.gameObject.SetActive(!State.collectedBranches.Contains(item.resourceId));
        }

        private Vector3 StovePosition()
        {
            return StoveLight ? StoveLight.transform.position : new Vector3(-2.6f, 1, 11.9f);
        }

        private void ApplyVisuals()
        {
            bool burning = State.fireSeconds > 0 || State.cookSeconds > 0;
            if (StoveLight)
            {
                StoveLight.enabled = burning;
                StoveLight.intensity = burning ? 2.1f + Mathf.PerlinNoise(Time.time * 7, 0) * .65f : 0;
            }
            if (Flame)
            {
                Flame.gameObject.SetActive(burning);
                Flame.localScale = new Vector3(1, .85f + Mathf.PerlinNoise(Time.time * 9, 3) * .3f, 1);
            }
            if (Steam)
            {
                var emission = Steam.emission;
                emission.enabled = State.brewSeconds > 0 || State.teaReady;
            }
            if (TeaVisual) TeaVisual.SetActive(State.teaReady);
            if (CookingSteam)
            {
                var emission = CookingSteam.emission;
                emission.enabled = State.cookSeconds > 0;
            }
            if (MealVisual) MealVisual.SetActive(State.meals > 0);
            if (Player)
            {
                Player.SetHeldCup(State.carryingTea);
                Player.SetHeldMeal(State.carryingMeal);
            }
            for (int i = 0; i < PlotCount; i++)
            {
                var plot = State.gardenPlots[i];
                if (GardenSeedlings[i])
                {
                    GardenSeedlings[i].SetActive(plot.planted && plot.growth < 2);
                    GardenSeedlings[i].transform.localScale = Vector3.one * (.55f + plot.growth * .35f);
                }
                if (GardenMature[i]) GardenMature[i].SetActive(plot.planted && plot.growth >= 2);
                if (gardenMaterials[i]) gardenMaterials[i].color = plot.lastWateredDay == State.day && plot.planted ? new Color(.24f, .19f, .13f) : new Color(.37f, .27f, .17f);
            }
            if (FishingRod) FishingRod.SetActive(IsFishing);
            if (FishingBobber)
            {
                FishingBobber.gameObject.SetActive(IsFishing);
                FishingBobber.position = bobberRest + Vector3.up * (FishBiting ? -.04f + Mathf.Sin(Time.time * 14) * .04f : Mathf.Sin(Time.time * 2) * .012f);
            }
            if (FishingLine)
            {
                FishingLine.enabled = IsFishing;
                if (IsFishing && RodTip && FishingBobber)
                {
                    FishingLine.SetPosition(0, RodTip.position);
                    FishingLine.SetPosition(1, FishingBobber.position);
                }
            }
        }

        public bool Save(bool announce)
        {
            bool success = WaldenSave.Write(State, SavePathOverride ?? WaldenSave.FilePath, out string error);
            if (success)
            {
                HasSave = true;
                if (announce) Notify("这一天已经保存。", 3);
            }
            else
            {
                Debug.LogWarning("Save failed: " + error);
                Notify("暂时无法保存，请检查存储空间与文件权限。", 7);
            }
            return success;
        }

        public void SetVolume(float value)
        {
            Volume = Mathf.Clamp01(value);
            Atmosphere.SetVolume(Volume);
            PlayerPrefs.SetFloat("Walden.Volume", Volume);
        }

        public void SetSensitivity(float value)
        {
            Player.Sensitivity = Mathf.Clamp(value, .035f, .25f);
            PlayerPrefs.SetFloat("Walden.Sensitivity", Player.Sensitivity);
        }

        public void Notify(string message, float duration = 4)
        {
            Toast = message;
            ToastUntil = Time.unscaledTime + duration;
        }

        public void Quit()
        {
            Save(false);
            PlayerPrefs.Save();
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private static void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (initialized && Started && !focused && !Sleeping && !smokeMode) SetPaused(true);
        }

        private void OnApplicationPause(bool paused)
        {
            if (initialized && Started && paused) Save(false);
        }

        private void OnApplicationQuit()
        {
            if (initialized && Started) Save(false);
            Time.timeScale = 1;
            SetCursor(false);
        }

        private void OnDestroy()
        {
            Time.timeScale = 1;
            SetCursor(false);
        }
    }
}
