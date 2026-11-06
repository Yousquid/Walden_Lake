using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Walden
{
    /// <summary>Opt-in, end-to-end verification for a built demo. Never uses the player's save.</summary>
    public sealed class WaldenSmokeTest : MonoBehaviour
    {
        [Serializable]
        private sealed class Report
        {
            public bool success;
            public string completedAtUtc;
            public string failure;
            public List<string> checks = new List<string>();
            public List<string> screenshots = new List<string>();
            public List<string> runtimeErrors = new List<string>();
        }

        private readonly Report report = new Report();
        private WaldenGame game;
        private string outputDirectory;

        private IEnumerator Start()
        {
            // WaldenGame adds this component after its world and player have initialized.
            yield return null;
            Application.logMessageReceived += OnLog;
            IEnumerator run = Run();
            bool failed = false;
            while (true)
            {
                bool more = false;
                object instruction = null;
                try
                {
                    more = run.MoveNext();
                    if (more) instruction = run.Current;
                }
                catch (Exception exception)
                {
                    report.failure = exception.ToString();
                    failed = true;
                }
                if (failed || !more) break;
                yield return instruction;
            }
            if (report.runtimeErrors.Count > 0)
            {
                failed = true;
                if (string.IsNullOrEmpty(report.failure)) report.failure = "Unity reported runtime errors.";
            }
            Finish(!failed);
        }

        private IEnumerator Run()
        {
            game = GetComponent<WaldenGame>();
            if (game == null) throw new InvalidOperationException("WaldenGame was not initialized.");
            outputDirectory = ReadOutputDirectory();
            Directory.CreateDirectory(outputDirectory);
            game.SavePathOverride = Path.Combine(outputDirectory, "smoke-save.json");
            // Hidden windows do not present the startup splash. The explicit camera
            // render request below is independent of that presentation state.
            game.Begin();
            Check("Fresh smoke session uses an isolated save", game.State.day == 1 &&
                game.State.wood == 0 && game.State.water == 0 &&
                !string.Equals(game.SavePathOverride, WaldenSave.FilePath, StringComparison.OrdinalIgnoreCase));

            // Walk the same capsule through the front doorway, rather than teleporting past walls.
            game.Player.Teleport(new Vector3(0f, 0.18f, 4.3f), 0f);
            CharacterController controller = game.Player.GetComponent<CharacterController>();
            Check("First-person collision controller is enabled", controller != null && controller.enabled);
            for (int i = 0; i < 75; i++)
            {
                controller.Move(new Vector3(0f, -0.03f, 0.08f));
                yield return null;
            }
            Check("Cabin doorway is passable", game.Player.transform.position.z > 9.6f &&
                Mathf.Abs(game.Player.transform.position.x) < 0.2f);

            WaldenInteractable stove = Find(InteractionKind.Stove);
            WaldenInteractable branch = Find(InteractionKind.Branch);
            WaldenInteractable water = Find(InteractionKind.Water);
            WaldenInteractable seat = FindLakeSeat();
            CheckInteractionRay(water, new Vector3(7f, 0.15f, -11f));
            CheckInteractionRay(stove, new Vector3(-2.65f, 0.18f, 10.1f));
            Check("Empty resources cannot light the stove", !game.Interact(stove) &&
                game.State.brewSeconds == 0 && game.State.wood == 0 && game.State.water == 0);
            Check("A fallen branch can be collected", game.Interact(branch) && game.State.wood == 1);
            Check("The same branch cannot be collected twice", !game.Interact(branch) &&
                game.State.wood == 1 && !branch.gameObject.activeSelf);
            WaldenInteractable secondBranch = Find(InteractionKind.Branch);
            Check("A second branch supplies the next day", game.Interact(secondBranch) && game.State.wood == 2);
            for (int i = 0; i < WaldenGame.WaterCapacity; i++)
                Check("Fill lake water portion " + (i + 1), game.Interact(water));
            Check("Water container respects its capacity", game.State.water == WaldenGame.WaterCapacity &&
                !game.Interact(water) && game.State.water == WaldenGame.WaterCapacity);

            Check("Brewing consumes one wood and one water", game.Interact(stove) &&
                game.State.wood == 1 && game.State.water == 2 && game.State.brewSeconds > 0 && !game.State.teaReady);
            Check("A busy stove cannot charge resources twice", !game.Interact(stove) &&
                game.State.wood == 1 && game.State.water == 2);
            game.Advance(WaldenGame.BrewDuration + 0.1f);
            Check("Boiled tea becomes ready", game.State.teaReady && game.State.brewSeconds == 0);
            yield return null; // ApplyVisuals makes the cup available before it is picked up.
            WaldenInteractable tea = Find(InteractionKind.Tea);
            CheckInteractionRay(tea, new Vector3(1.2f, 0.18f, 8.6f));
            Check("Ready tea can be carried", game.Interact(tea) && game.State.carryingTea && !game.State.teaReady);
            Vector3 beforeSitting = game.Player.transform.position;
            Check("A lakeside chair accepts the player", game.Interact(seat) && game.Player.IsSeated);
            yield return null;

            // Also verify the lower cabin window from the actual seated eye height.
            game.Player.Stand();
            WaldenInteractable windowSeat = game.Interactables.Find(item => item.kind == InteractionKind.Seat && item.seatPosition.z > 0);
            Check("A window chair accepts the player", windowSeat != null && game.Interact(windowSeat) && game.Player.IsSeated);
            yield return null;

            // Tea screenshot also checks that the rendered hand-held cup can coexist with sitting.
            string teaScreenshot = CapturePath("tea");
            yield return new WaitForEndOfFrame();
            CaptureScene(teaScreenshot);
            yield return null;
            yield return null;
            Check("Tea can be enjoyed while seated", game.DrinkTea() && !game.State.carryingTea &&
                game.State.teaToday == 1 && game.State.totalTea == 1);
            Check("A finished cup cannot be consumed again", !game.DrinkTea() && game.State.totalTea == 1);
            game.Player.Stand();
            Check("Standing returns to the previous safe position", !game.Player.IsSeated &&
                Vector3.Distance(beforeSitting, game.Player.transform.position) < 0.05f);

            int woodBeforeSleep = game.State.wood;
            int waterBeforeSleep = game.State.water;
            game.CompleteNight();
            Check("Sleep advances to the next morning", game.State.day == 2 &&
                Mathf.Abs(game.State.hour - 7.5f) < 0.01f);
            Check("Stored resources survive the night", game.State.wood == woodBeforeSleep &&
                game.State.water == waterBeforeSleep && game.State.totalTea == 1);
            Check("Fallen branches return on the next day", branch.gameObject.activeSelf &&
                secondBranch.gameObject.activeSelf && game.State.collectedBranches.Count == 0);
            Check("The next day resets daily notes", game.State.branchesToday == 0 &&
                game.State.waterToday == 0 && game.State.teaToday == 0);
            Check("Save writes successfully", game.Save(false));
            bool recovered;
            WaldenState saved = WaldenSave.Read(game.SavePathOverride, out recovered);
            Check("Save can be loaded without losing progress", !recovered && saved.day == 2 &&
                saved.wood == woodBeforeSleep && saved.water == waterBeforeSleep && saved.totalTea == 1);
            CheckBackupRecovery();

            // Gather actual branches across days until the wood store reaches its limit.
            for (int supplyDay = 0; supplyDay < 4 && game.State.wood < WaldenGame.WoodCapacity; supplyDay++)
            {
                foreach (WaldenInteractable item in game.Interactables)
                {
                    if (item.kind != InteractionKind.Branch || !item.gameObject.activeInHierarchy) continue;
                    if (game.State.wood >= WaldenGame.WoodCapacity) break;
                    Check("Gather replenished branch " + item.resourceId + " on day " + game.State.day, game.Interact(item));
                }
                if (game.State.wood < WaldenGame.WoodCapacity) game.CompleteNight();
            }
            WaldenInteractable spareBranch = Find(InteractionKind.Branch);
            Check("Wood storage respects its capacity", game.State.wood == WaldenGame.WoodCapacity &&
                !game.Interact(spareBranch) && game.State.wood == WaldenGame.WoodCapacity &&
                spareBranch.gameObject.activeInHierarchy);

            Check("An unfinished kettle starts before bed", game.Interact(stove) && game.State.brewSeconds > 0);
            int dayBeforeKettleSleep = game.State.day;
            int woodBeforeKettleSleep = game.State.wood;
            int waterBeforeKettleSleep = game.State.water;
            game.CompleteNight();
            Check("An unfinished kettle is ready after sleeping", game.State.day == dayBeforeKettleSleep + 1 &&
                game.State.teaReady && game.State.brewSeconds == 0 &&
                game.State.wood == woodBeforeKettleSleep && game.State.water == waterBeforeKettleSleep);

            IEnumerator homestead = VerifyHomesteadLoops();
            while (homestead.MoveNext()) yield return homestead.Current;

            // Keep gameplay visible; a paused game would cover these captures with its menu.
            game.SetPaused(false);
            game.State.hour = 8.5f;
            AimFrom(new Vector3(-4f, 0.12f, 1.5f), new Vector3(1f, 1.0f, -48f));
            yield return null;
            string lakeScreenshot = CapturePath("lake");
            yield return new WaitForEndOfFrame();
            CaptureScene(lakeScreenshot);
            yield return null;
            yield return null;

            AimFrom(new Vector3(10f, 0.12f, -1f), new Vector3(0f, 2.0f, 10f));
            yield return null;
            string cabinScreenshot = CapturePath("cabin");
            yield return new WaitForEndOfFrame();
            CaptureScene(cabinScreenshot);
            yield return null;
            yield return null;

            game.State.hour = 18.6f;
            game.State.fireSeconds = 90f;
            AimFrom(new Vector3(10f, 0.12f, -1f), new Vector3(0f, 2.0f, 10f));
            yield return null;
            string duskScreenshot = CapturePath("dusk");
            yield return new WaitForEndOfFrame();
            CaptureScene(duskScreenshot);
            yield return null;
            yield return null;

            Check("All seven reference screenshots were written", ScreenshotsExist());
            foreach (string screenshot in report.screenshots)
                Check("Rendered image has scene detail: " + Path.GetFileName(screenshot), ImageHasDetail(screenshot));
        }

        private IEnumerator VerifyHomesteadLoops()
        {
            WaldenInteractable water = Find(InteractionKind.Water);
            WaldenInteractable stove = Find(InteractionKind.Stove);
            WaldenInteractable tea = Find(InteractionKind.Tea);
            WaldenInteractable plot = Find(InteractionKind.GardenPlot);
            WaldenInteractable secondPlot = game.Interactables.Find(item => item.kind == InteractionKind.GardenPlot && item.resourceId == 1);
            WaldenInteractable dryPlot = game.Interactables.Find(item => item.kind == InteractionKind.GardenPlot && item.resourceId == 2);
            WaldenInteractable fishing = Find(InteractionKind.Fishing);
            WaldenInteractable cooking = Find(InteractionKind.Cooking);
            WaldenInteractable meal = Find(InteractionKind.Meal);
            Check("New homesteads have three garden beds and starter vegetables", game.State.version == 2 &&
                game.State.gardenPlots != null && game.State.gardenPlots.Length == WaldenGame.PlotCount &&
                game.State.vegetables == 2 && secondPlot != null && dryPlot != null);
            CheckInteractionRay(plot, new Vector3(7.1f, 0.05f, 1.1f));
            CheckInteractionRay(cooking, new Vector3(-1.15f, 0.18f, 10.5f));
            CheckInteractionRay(fishing, new Vector3(7.15f, 0.13f, -10.4f));
            CheckInteractionRay(meal, new Vector3(3.35f, 0.18f, 8.35f));

            int plotId = plot.resourceId;
            int waterBeforePlanting = game.State.water;
            Check("An empty bed can be planted", game.Interact(plot) && game.State.gardenPlots[plotId].planted &&
                game.State.gardenPlots[plotId].growth == 0 && game.State.water == waterBeforePlanting);
            Check("Watering a seedling costs one lake water", game.Interact(plot) &&
                game.State.water == waterBeforePlanting - 1 && game.State.gardenPlots[plotId].lastWateredDay == game.State.day);
            Check("Water can be refilled before garden work", game.Interact(water));
            int waterBeforeRepeat = game.State.water;
            Check("A garden bed cannot be watered twice in one day", !game.Interact(plot) &&
                game.State.water == waterBeforeRepeat);
            Check("A second bed can be planted and watered", game.Interact(secondPlot) && game.Interact(secondPlot));
            Check("The third bed can be planted", game.Interact(dryPlot));
            Check("A dry water container cannot water a bed", game.State.water == 0 && !game.Interact(dryPlot) &&
                game.State.gardenPlots[2].lastWateredDay != game.State.day);
            game.CompleteNight();
            Check("Only watered beds grow overnight", game.State.gardenPlots[plotId].growth == 1 &&
                game.State.gardenPlots[1].growth == 1 && game.State.gardenPlots[2].growth == 0);
            Check("The next day accepts fresh water", game.Interact(water) && game.Interact(water));
            Check("Seedlings can be watered on the next day", game.Interact(plot) && game.Interact(secondPlot));
            game.CompleteNight();
            Check("Two watered nights mature the vegetables", game.State.gardenPlots[plotId].growth == 2 &&
                game.State.gardenPlots[1].growth == 2 && game.State.gardenPlots[2].growth == 0);
            int vegetablesBeforeHarvest = game.State.vegetables;
            int totalHarvestBefore = game.State.totalHarvest;
            Check("Harvesting produces two fresh vegetables", game.Interact(plot) &&
                game.State.vegetables == vegetablesBeforeHarvest + 2 && !game.State.gardenPlots[plotId].planted &&
                game.State.totalHarvest > totalHarvestBefore);
            int totalHarvestAfter = game.State.totalHarvest;
            game.Interact(plot); // Pressing E again may plant a new crop, but cannot duplicate the harvest.
            Check("A harvested bed cannot duplicate vegetables", game.State.vegetables == vegetablesBeforeHarvest + 2 &&
                game.State.totalHarvest == totalHarvestAfter);
            game.State.hour = 9f;
            AimFrom(new Vector3(9.2f, 0.12f, -1.5f), new Vector3(9.2f, 0.35f, 3f));
            yield return null;
            string gardenScreenshot = CapturePath("garden");
            yield return new WaitForEndOfFrame();
            CaptureScene(gardenScreenshot);
            yield return null;
            yield return null;
            Check("The second mature bed can also be harvested", game.Interact(secondPlot) &&
                game.State.vegetables == vegetablesBeforeHarvest + 4);
            // Persist a visibly growing bed for the later save round trip.
            Check("A replanted bed can be watered again", game.Interact(water) && game.Interact(plot));

            int fishBefore = game.State.fish;
            Check("Casting starts a seated fishing session", game.Interact(fishing) && game.IsFishing &&
                game.Player.IsSeated && !game.FishBiting && game.FishingRemaining > 0);
            Check("Other interactions are unavailable while fishing", !game.Interact(water));
            Check("Reeling too early catches no fish", !game.ReelFishing() && !game.IsFishing &&
                !game.Player.IsSeated && game.State.fish == fishBefore);
            Check("A second cast can be cancelled", game.Interact(fishing));
            game.CancelFishing();
            Check("Cancelling fishing restores standing without a catch", !game.IsFishing &&
                !game.Player.IsSeated && game.State.fish == fishBefore);
            Check("A new cast can wait for a bite", game.Interact(fishing));
            game.Advance(game.FishingRemaining + 0.05f);
            Check("Waiting reaches a bite window", game.IsFishing && game.FishBiting && game.BiteRemaining > 0);
            yield return null;
            string fishingScreenshot = CapturePath("fishing");
            yield return new WaitForEndOfFrame();
            CaptureScene(fishingScreenshot);
            yield return null;
            yield return null;
            Check("Reeling during a bite catches one fish", game.ReelFishing() && !game.IsFishing &&
                !game.Player.IsSeated && game.State.fish == fishBefore + 1 && game.State.totalFish > 0);
            Check("A cast can also miss its bite window", game.Interact(fishing));
            game.Advance(game.FishingRemaining + 0.05f);
            game.Advance(game.BiteRemaining + 0.05f);
            Check("An expired bite window releases the fish", !game.IsFishing &&
                !game.Player.IsSeated && game.State.fish == fishBefore + 1);
            Check("Fishing can begin before sleep", game.Interact(fishing));
            game.CompleteNight();
            Check("Sleeping cancels an unfinished fishing session", !game.IsFishing && !game.Player.IsSeated &&
                game.State.fish == fishBefore + 1);
            while (game.State.fish < WaldenGame.FishCapacity)
            {
                int previousFish = game.State.fish;
                Check("Cast for fish portion " + (previousFish + 1), game.Interact(fishing));
                game.Advance(game.FishingRemaining + 0.05f);
                Check("Land fish portion " + (previousFish + 1), game.ReelFishing() && game.State.fish == previousFish + 1);
            }
            Check("A full fish basket prevents another cast", !game.Interact(fishing) &&
                !game.IsFishing && game.State.fish == WaldenGame.FishCapacity);

            int woodBeforeCooking = game.State.wood;
            int vegetablesBeforeCooking = game.State.vegetables;
            Check("A kitchen interaction opens the recipe menu", game.Interact(cooking) && game.ShowCooking &&
                !game.IsPlaying && Time.timeScale == 0f);
            Check("Missing recipe water cannot consume ingredients", !game.StartCooking(1) &&
                game.State.water == 0 && game.State.wood == woodBeforeCooking &&
                game.State.vegetables == vegetablesBeforeCooking && game.State.cookSeconds == 0);
            game.CloseCooking();
            Check("Closing the recipe menu restores play", !game.ShowCooking && game.IsPlaying && Time.timeScale == 1f);
            Check("The kitchen can be supplied with lake water", game.Interact(water));
            game.OpenCooking();
            Check("Vegetable soup consumes its exact recipe", game.StartCooking(1) && !game.ShowCooking &&
                game.State.cookingRecipe == 1 && game.State.cookSeconds > 0 &&
                game.State.wood == woodBeforeCooking - 1 && game.State.water == 0 &&
                game.State.vegetables == vegetablesBeforeCooking - 2);
            int ingredientsWhileCooking = game.State.vegetables;
            Check("A busy cooking pot cannot charge ingredients twice", !game.StartCooking(1) &&
                game.State.vegetables == ingredientsWhileCooking && game.State.wood == woodBeforeCooking - 1);
            game.Advance(WaldenGame.CookDuration + 0.1f);
            Check("Cooking adds one meal to the pantry", game.State.cookSeconds == 0 && game.State.meals == 1);

            // The kettle from the previous day's test is still ready, so verify both hand-held exclusions.
            Check("Tea can be held beside a prepared meal", game.Interact(tea) && game.State.carryingTea);
            Check("A held teacup prevents taking a soup bowl", !game.TakeMeal() && game.State.carryingTea &&
                !game.State.carryingMeal && game.State.meals == 1);
            Check("The held tea can be finished", game.DrinkTea());
            Check("A fresh kettle can be prepared for the reverse exclusion", game.Interact(water) && game.Interact(stove));
            game.Advance(WaldenGame.BrewDuration + 0.1f);
            Check("A prepared meal can be carried", game.TakeMeal() && game.State.carryingMeal &&
                !game.State.carryingTea && game.State.meals == 0);
            Check("A held soup bowl prevents taking tea", !game.Interact(tea) && game.State.carryingMeal &&
                !game.State.carryingTea && game.State.teaReady);
            Check("A hand-held meal saves successfully", game.Save(false));
            bool carriedRecovered;
            WaldenState carriedSave = WaldenSave.Read(game.SavePathOverride, out carriedRecovered);
            Check("A carried meal survives saving without becoming a teacup", !carriedRecovered &&
                carriedSave.carryingMeal && !carriedSave.carryingTea && carriedSave.meals == 0);
            AimFrom(new Vector3(-0.1f, 0.18f, 9.6f), new Vector3(-1.45f, 1.1f, 12f));
            yield return null;
            Transform bowl = game.Player.View.transform.Find("A bowl of homemade soup");
            Transform cup = game.Player.View.transform.Find("A warm cup of tea");
            Check("The hand-held bowl is visible while the teacup is hidden", bowl != null && bowl.gameObject.activeSelf &&
                (cup == null || !cup.gameObject.activeSelf));
            string cookingScreenshot = CapturePath("cooking");
            yield return new WaitForEndOfFrame();
            CaptureScene(cookingScreenshot);
            yield return null;
            yield return null;
            int mealsBeforeEating = game.State.totalMeals;
            Check("Eating a meal consumes the held bowl", game.EatMeal() && !game.State.carryingMeal &&
                game.State.totalMeals == mealsBeforeEating + 1 && game.State.mealsToday == 1);
            Check("A finished bowl cannot be eaten twice", !game.EatMeal() && game.State.totalMeals == mealsBeforeEating + 1);
            Check("The fish recipe can be supplied with water", game.Interact(water));
            int fishBeforeRecipe = game.State.fish;
            int vegetablesBeforeFishRecipe = game.State.vegetables;
            int woodBeforeFishRecipe = game.State.wood;
            Check("Lake fish soup consumes fish, vegetables, water and wood", game.StartCooking(2) &&
                game.State.cookingRecipe == 2 && game.State.fish == fishBeforeRecipe - 1 &&
                game.State.vegetables == vegetablesBeforeFishRecipe - 1 && game.State.water == 0 &&
                game.State.wood == woodBeforeFishRecipe - 1);
            Check("An active cooking recipe saves successfully", game.Save(false));
            bool simmerRecovered;
            WaldenState simmerSave = WaldenSave.Read(game.SavePathOverride, out simmerRecovered);
            Check("Saving preserves a simmering recipe and daily meal notes", !simmerRecovered &&
                simmerSave.cookingRecipe == 2 && simmerSave.cookSeconds > 0 &&
                Mathf.Abs(simmerSave.cookSeconds - game.State.cookSeconds) < 0.01f &&
                simmerSave.mealsToday == game.State.mealsToday && simmerSave.totalMeals == game.State.totalMeals);
            game.CompleteNight();
            Check("Cooking finishes safely while sleeping", game.State.cookSeconds == 0 && game.State.meals == 1);
            Check("Extended progress saves successfully", game.Save(false));
            bool recovered;
            WaldenState saved = WaldenSave.Read(game.SavePathOverride, out recovered);
            Check("Garden, fishing and meals survive a save round trip", !recovered && saved.version == 2 &&
                saved.vegetables == game.State.vegetables && saved.fish == game.State.fish && saved.meals == game.State.meals &&
                saved.totalHarvest == game.State.totalHarvest && saved.totalFish == game.State.totalFish &&
                saved.totalMeals == game.State.totalMeals && saved.gardenPlots.Length == WaldenGame.PlotCount &&
                saved.gardenPlots[plotId].planted == game.State.gardenPlots[plotId].planted &&
                saved.gardenPlots[plotId].growth == game.State.gardenPlots[plotId].growth &&
                saved.gardenPlots[plotId].lastWateredDay == game.State.gardenPlots[plotId].lastWateredDay);
            CheckLegacyMigration();
        }

        private void CheckLegacyMigration()
        {
            string path = Path.Combine(outputDirectory, "legacy-v1.json");
            File.WriteAllText(path, "{\"version\":1,\"day\":8,\"hour\":14.5,\"wood\":5,\"water\":2," +
                "\"teaReady\":true,\"totalTea\":3,\"collectedBranches\":[1,4]}");
            bool recovered;
            WaldenState migrated = WaldenSave.Read(path, out recovered);
            Check("Version one saves retain the original day and supplies", !recovered && migrated.version == 2 &&
                migrated.day == 8 && migrated.wood == 5 && migrated.water == 2 && migrated.teaReady &&
                migrated.totalTea == 3 && migrated.collectedBranches.Contains(1) && migrated.collectedBranches.Contains(4));
            Check("Version one saves receive usable garden and cooking defaults", migrated.gardenPlots != null &&
                migrated.gardenPlots.Length == WaldenGame.PlotCount && migrated.gardenPlots[0] != null &&
                migrated.gardenPlots[1] != null && migrated.gardenPlots[2] != null &&
                migrated.vegetables == 2 && migrated.fish == 0 && migrated.meals == 0 && !migrated.carryingMeal);
        }

        private void CheckBackupRecovery()
        {
            Check("A second save creates a previous-file backup", game.Save(false) &&
                File.Exists(game.SavePathOverride + ".backup"));
            string original = File.ReadAllText(game.SavePathOverride);
            bool recovered = false;
            WaldenState restored = null;
            try
            {
                File.WriteAllText(game.SavePathOverride, "intentionally damaged smoke save");
                restored = WaldenSave.Read(game.SavePathOverride, out recovered);
            }
            finally
            {
                // Even a failed assertion must not leave a broken test save for OnApplicationQuit.
                File.WriteAllText(game.SavePathOverride, original);
            }
            Check("A damaged primary save falls back to the backup", recovered && restored != null &&
                restored.day == game.State.day && restored.wood == game.State.wood &&
                restored.water == game.State.water && restored.totalTea == game.State.totalTea);
        }

        private WaldenInteractable Find(InteractionKind kind)
        {
            foreach (WaldenInteractable item in game.Interactables)
                if (item != null && item.kind == kind && item.gameObject.activeInHierarchy) return item;
            throw new InvalidOperationException("No active interaction registered for " + kind);
        }

        private WaldenInteractable FindLakeSeat()
        {
            WaldenInteractable result = null;
            foreach (WaldenInteractable item in game.Interactables)
                if (item.kind == InteractionKind.Seat &&
                    (result == null || item.seatPosition.z < result.seatPosition.z)) result = item;
            if (result == null) throw new InvalidOperationException("No lakeside chair was registered.");
            return result;
        }

        private static string ReadOutputDirectory()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++)
                if (arguments[i] == "-waldenOutput")
                {
                    string path = arguments[i + 1];
                    if (!Path.IsPathRooted(path)) throw new ArgumentException("-waldenOutput requires an absolute path.");
                    return Path.GetFullPath(path);
                }
            return Path.Combine(Application.temporaryCachePath, "WaldenSmoke");
        }

        private void AimFrom(Vector3 feet, Vector3 target)
        {
            Vector3 eye = feet + Vector3.up * 1.65f;
            Vector3 direction = target - eye;
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
            game.Player.Teleport(feet, yaw, pitch);
        }

        private void CheckInteractionRay(WaldenInteractable item, Vector3 feet)
        {
            Collider targetCollider = item.GetComponent<Collider>();
            if (targetCollider == null) throw new InvalidOperationException(item.kind + " has no target collider.");
            Physics.SyncTransforms();
            AimFrom(feet, targetCollider.bounds.center);
            Physics.SyncTransforms();
            Ray ray = new Ray(game.Player.View.transform.position, game.Player.View.transform.forward);
            bool hitSomething = Physics.Raycast(ray, out RaycastHit hit, 3.1f, ~(1 << 2), QueryTriggerInteraction.Ignore);
            WaldenInteractable found = hitSomething ? hit.collider.GetComponentInParent<WaldenInteractable>() : null;
            Check("Camera ray reaches " + item.kind + " (hit " +
                (hitSomething ? hit.collider.name : "nothing") + ")", found == item);
        }

        private string CapturePath(string name)
        {
            string path = Path.Combine(outputDirectory, name + ".png");
            report.screenshots.Add(path);
            return path;
        }

        private void CaptureScene(string path)
        {
            // A hidden player may have an unpresented/black swap chain. Render the real
            // player camera explicitly into its own target, then read back that target.
            const int width = 1280;
            const int height = 720;
            Camera camera = game.Player.View;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            float previousAspect = camera.aspect;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB) { name = "Walden smoke capture", antiAliasing = 1 };
            var image = new Texture2D(width, height, TextureFormat.RGB24, false, false);
            try
            {
                target.Create();
                camera.aspect = (float)width / height;
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                if (!RenderPipeline.SupportsRenderRequest(camera, request))
                    throw new InvalidOperationException("The current pipeline does not support a single-camera capture.");
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                image.Apply(false, false);
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.aspect = previousAspect;
                RenderTexture.active = previousActive;
                target.Release();
                Destroy(target);
                Destroy(image);
            }
        }

        private bool ScreenshotsExist()
        {
            foreach (string path in report.screenshots)
                if (!File.Exists(path) || new FileInfo(path).Length < 1024) return false;
            return report.screenshots.Count == 7;
        }

        private static bool ImageHasDetail(string path)
        {
            var texture = new Texture2D(2, 2);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path))) return false;
                var colors = new HashSet<int>();
                int lit = 0;
                for (int y = 1; y < 12; y++)
                    for (int x = 1; x < 20; x++)
                    {
                        Color32 pixel = texture.GetPixel(texture.width * x / 20, texture.height * y / 12);
                        colors.Add((pixel.r / 8 << 10) | (pixel.g / 8 << 5) | pixel.b / 8);
                        if (pixel.r + pixel.g + pixel.b > 30) lit++;
                    }
                return colors.Count > 18 && lit > 80;
            }
            finally { Destroy(texture); }
        }

        private void Check(string label, bool passed)
        {
            Debug.Log("CHECK " + (passed ? "PASS " : "FAIL ") + label);
            if (!passed) throw new InvalidOperationException(label);
            report.checks.Add(label);
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                report.runtimeErrors.Add(condition + "\n" + stackTrace);
        }

        private void Finish(bool success)
        {
            Application.logMessageReceived -= OnLog;
            report.success = success;
            report.completedAtUtc = DateTime.UtcNow.ToString("O");
            try
            {
                if (string.IsNullOrEmpty(outputDirectory))
                    outputDirectory = Path.Combine(Application.temporaryCachePath, "WaldenSmoke");
                Directory.CreateDirectory(outputDirectory);
                File.WriteAllText(Path.Combine(outputDirectory, "smoke-report.json"), JsonUtility.ToJson(report, true));
            }
            catch (Exception exception)
            {
                Debug.LogError("Could not write smoke report: " + exception.Message);
                success = false;
            }
            Debug.Log(success ? "WALDEN SMOKE PASSED" : "WALDEN SMOKE FAILED: " + report.failure);
            Time.timeScale = 1f;
            Application.Quit(success ? 0 : 1);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
