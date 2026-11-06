using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Walden
{
    [Serializable]
    public sealed class GardenPlotState
    {
        public bool planted;
        public int growth;
        public int lastWateredDay;
    }

    [Serializable]
    public sealed class WaldenState
    {
        public int version = 2;
        public int day = 1;
        public float hour = 7.5f;
        public int wood;
        public int water;
        public bool teaReady;
        public bool carryingTea;
        public float brewSeconds;
        public float fireSeconds;
        public int branchesToday;
        public int waterToday;
        public int teaToday;
        public int totalTea;
        public int vegetables = 2;
        public int fish;
        public int meals;
        public bool carryingMeal;
        public float cookSeconds;
        public int cookingRecipe;
        public int harvestToday;
        public int fishToday;
        public int mealsToday;
        public int totalHarvest;
        public int totalFish;
        public int totalMeals;
        public GardenPlotState[] gardenPlots = CreatePlots();
        public List<int> collectedBranches = new List<int>();

        private static GardenPlotState[] CreatePlots() => new[] { new GardenPlotState(), new GardenPlotState(), new GardenPlotState() };

        public void Sanitize()
        {
            // Version one had only the tea loop. Add a small pantry without replacing its progress.
            if (version == 1)
            {
                version = 2;
                vegetables = 2;
                fish = meals = 0;
                carryingMeal = false;
                cookSeconds = 0;
                cookingRecipe = 0;
                gardenPlots = CreatePlots();
            }
            day = Mathf.Clamp(day, 1, 99999);
            hour = IsFinite(hour) ? Mathf.Clamp(hour, 6f, 22f) : 7.5f;
            wood = Mathf.Clamp(wood, 0, 12);
            water = Mathf.Clamp(water, 0, 3);
            brewSeconds = IsFinite(brewSeconds) ? Mathf.Clamp(brewSeconds, 0, 10) : 0;
            fireSeconds = IsFinite(fireSeconds) ? Mathf.Clamp(fireSeconds, 0, 100) : 0;
            branchesToday = Mathf.Clamp(branchesToday, 0, 1000);
            waterToday = Mathf.Clamp(waterToday, 0, 1000);
            teaToday = Mathf.Clamp(teaToday, 0, 1000);
            totalTea = Mathf.Clamp(totalTea, 0, 1000000);
            vegetables = Mathf.Clamp(vegetables, 0, 12);
            fish = Mathf.Clamp(fish, 0, 6);
            meals = Mathf.Clamp(meals, 0, 6);
            cookSeconds = IsFinite(cookSeconds) ? Mathf.Clamp(cookSeconds, 0, 12) : 0;
            if (cookSeconds <= 0 || cookingRecipe < 1 || cookingRecipe > 2) { cookSeconds = 0; cookingRecipe = 0; }
            harvestToday = Mathf.Clamp(harvestToday, 0, 1000);
            fishToday = Mathf.Clamp(fishToday, 0, 1000);
            mealsToday = Mathf.Clamp(mealsToday, 0, 1000);
            totalHarvest = Mathf.Clamp(totalHarvest, 0, 1000000);
            totalFish = Mathf.Clamp(totalFish, 0, 1000000);
            totalMeals = Mathf.Clamp(totalMeals, 0, 1000000);
            if (gardenPlots == null || gardenPlots.Length != 3)
            {
                var replacement = CreatePlots();
                if (gardenPlots != null) Array.Copy(gardenPlots, replacement, Math.Min(gardenPlots.Length, 3));
                gardenPlots = replacement;
            }
            for (int i = 0; i < gardenPlots.Length; i++)
            {
                if (gardenPlots[i] == null) gardenPlots[i] = new GardenPlotState();
                var plot = gardenPlots[i];
                plot.growth = plot.planted ? Mathf.Clamp(plot.growth, 0, 2) : 0;
                plot.lastWateredDay = plot.planted ? Mathf.Clamp(plot.lastWateredDay, 0, day) : 0;
            }
            if (collectedBranches == null) collectedBranches = new List<int>();
            collectedBranches = collectedBranches.FindAll(id => id >= 0 && id < 1000);
            if (carryingTea) teaReady = false;
            if (carryingTea) carryingMeal = false;
            if (teaReady || carryingTea) brewSeconds = 0;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public static class WaldenSave
    {
        public static string FilePath => Path.Combine(Application.persistentDataPath, "walden-day.json");
        public static bool Exists => File.Exists(FilePath) || File.Exists(FilePath + ".backup");

        public static WaldenState Read(string path, out bool recovered)
        {
            recovered = false;
            foreach (string candidate in new[] { path, path + ".backup" })
            {
                if (!File.Exists(candidate)) continue;
                try
                {
                    string json = File.ReadAllText(candidate);
                    if (!json.TrimStart().StartsWith("{")) throw new InvalidDataException("Not a JSON object.");
                    var value = JsonUtility.FromJson<WaldenState>(json);
                    if (value == null || value.version < 1 || value.version > 2 || value.day < 1)
                        throw new InvalidDataException("Unsupported or incomplete save.");
                    value.Sanitize();
                    recovered = candidate != path;
                    return value;
                }
                catch (Exception exception) when (exception is IOException || exception is ArgumentException || exception is InvalidDataException || exception is UnauthorizedAccessException)
                {
                    recovered = true;
                    Debug.LogWarning("Could not read Walden save: " + exception.Message);
                }
            }
            return new WaldenState();
        }

        public static bool Write(WaldenState value, string path, out string error)
        {
            error = null;
            try
            {
                value.Sanitize();
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                string temporary = path + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(value, true));
                // Keep the previous complete file if the application is interrupted during a write.
                if (File.Exists(path)) File.Replace(temporary, path, path + ".backup");
                else File.Move(temporary, path);
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is NotSupportedException)
            {
                error = exception.Message;
                return false;
            }
        }
    }
}
