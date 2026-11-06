using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Walden
{
    /// <summary>A small, deliberately composed world made entirely from reusable meshes.</summary>
    public static class WaldenWorld
    {
        static Transform world;
        static System.Random random;
        static Mesh crownMesh;
        static readonly Dictionary<int, Mesh> cones = new Dictionary<int, Mesh>();
        static Material soil, grass, sand, timber, timberLight, timberDark, roof, stone, iron, linen, sage;

        public static void Build(WaldenGame game)
        {
            random = new System.Random(731);
            world = new GameObject("Walden · Lake and Woodland").transform;
            soil = Material("Forest floor", new Color(.32f, .38f, .25f));
            grass = Material("Meadow", new Color(.43f, .48f, .30f));
            sand = Material("Warm shoreline", new Color(.63f, .58f, .43f));
            timber = Material("Honey cedar", new Color(.43f, .28f, .16f));
            timberLight = Material("Cut wood", new Color(.62f, .44f, .25f));
            timberDark = Material("Dark end grain", new Color(.25f, .17f, .115f));
            roof = Material("Weathered cedar shingles", new Color(.25f, .29f, .26f));
            stone = Material("Granite", new Color(.46f, .49f, .43f));
            iron = Material("Cast iron", new Color(.13f, .17f, .17f), .3f);
            linen = Material("Unbleached linen", new Color(.81f, .77f, .62f));
            sage = Material("Sage wool", new Color(.39f, .47f, .37f));

            BuildLandscape(game);
            BuildCabin(game);
            BuildPier(game);
            BuildGarden(game);
            BuildWoodland(game);
            BuildMeadow();

            game.SpawnPosition = new Vector3(0f, .18f, 4.2f);
            game.SpawnYaw = 180f;
        }

        static void BuildLandscape(WaldenGame game)
        {
            var land = Group("Land and shore");
            Cube("Walkable woodland", new Vector3(0, -.3f, 35), new Vector3(150, .6f, 84), grass, land, true);
            Cube("Distant bank", new Vector3(0, -.15f, -112), new Vector3(210, .5f, 38), soil, land);
            Cube("Sandy waterline", new Vector3(0, .006f, -6.5f), new Vector3(144, .012f, 1.5f), sand, land);

            var lakeShader = Resources.Load<Shader>("WaldenLake");
            var lakeMaterial = lakeShader != null ? new Material(lakeShader) : Material("Lake", new Color(.24f, .46f, .47f), .82f);
            lakeMaterial.name = "Walden · quiet water";
            if (lakeMaterial.HasProperty("_BaseColor")) lakeMaterial.SetColor("_BaseColor", new Color(.22f, .43f, .44f, 1));
            if (lakeMaterial.HasProperty("_DeepColor")) lakeMaterial.SetColor("_DeepColor", new Color(.09f, .25f, .29f, 1));
            if (lakeMaterial.HasProperty("_RippleScale")) lakeMaterial.SetFloat("_RippleScale", 1.2f);
            var lake = MeshObject("Walden Pond", PlaneMesh(192, 106), lakeMaterial, land);
            lake.transform.position = new Vector3(0, -.14f, -60);
            lake.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            game.LakeSurface = lake.GetComponent<Renderer>();

            var hillMaterials = new[]
            {
                Material("Far hills · blue", new Color(.27f, .38f, .38f)),
                Material("Far hills · mist", new Color(.38f, .47f, .45f)),
                Material("Far hills · pine", new Color(.25f, .35f, .30f))
            };
            for (int i = 0; i < 11; i++)
            {
                float x = -102 + i * 21;
                var hill = MeshObject("Distant wooded ridge", CrownMesh(), hillMaterials[i % hillMaterials.Length], land);
                hill.transform.position = new Vector3(x, 1.5f, -124 - Next(0, 22));
                hill.transform.localScale = new Vector3(Next(21, 35), Next(9, 21), Next(22, 35));
                hill.transform.rotation = Quaternion.Euler(0, Next(0, 360), 0);
            }
            for (int i = 0; i < 7; i++)
            {
                var hill = MeshObject("Woodland rise", CrownMesh(), hillMaterials[2], land);
                hill.transform.position = new Vector3(i % 2 == 0 ? -65 : 65, 0, 8 + i * 10);
                hill.transform.localScale = new Vector3(23, Next(8, 14), 29);
            }

            Path("Path to the water", new[] { new Vector3(0, 0, 5.7f), new Vector3(-.4f, 0, 2.8f), new Vector3(.1f, 0, -.5f), new Vector3(2.1f, 0, -3.1f), new Vector3(7, 0, -5.8f) }, 1.65f, land);
            Path("Window walk", new[] { new Vector3(0, 0, -.5f), new Vector3(-2.7f, 0, -2.2f), new Vector3(-5, 0, -4.5f) }, 1.2f, land);
            Path("Woodland footpath", new[] { new Vector3(-.3f, 0, 2.6f), new Vector3(-5.9f, 0, 3.4f), new Vector3(-8.5f, 0, 9.5f), new Vector3(-6.8f, 0, 17.8f), new Vector3(1.5f, 0, 20.3f), new Vector3(7.1f, 0, 15.6f), new Vector3(6, 0, 7), new Vector3(2.3f, 0, 3) }, 1.15f, land);

            // Invisible safety edges live on Ignore Raycast so they never steal an interaction.
            Blocker("West shore edge", new Vector3(-34, 1.5f, -7.2f), new Vector3(79.6f, 4, .3f), land);
            Blocker("East shore edge", new Vector3(41, 1.5f, -7.2f), new Vector3(65.6f, 4, .3f), land);
            Blocker("Woodland west edge", new Vector3(-39, 1.5f, 17), new Vector3(.5f, 5, 50), land);
            Blocker("Woodland east edge", new Vector3(39, 1.5f, 17), new Vector3(.5f, 5, 50), land);
            Blocker("Woodland north edge", new Vector3(0, 1.5f, 41), new Vector3(80, 5, .5f), land);

            Chair("Lake chair", new Vector3(-5, .04f, -4.7f), 180, game, land);
            Cube("Lake chair side table", new Vector3(-6.15f, .43f, -4.7f), new Vector3(.65f, .1f, .65f), timberLight, land);
            Cube("Side table foot", new Vector3(-6.15f, .2f, -4.7f), new Vector3(.14f, .4f, .14f), timberDark, land);
        }

        static void BuildCabin(WaldenGame game)
        {
            var cabin = Group("The cabin");
            Cube("Foundation", new Vector3(0, .035f, 10), new Vector3(8.3f, .2f, 7.3f), timberDark, cabin, true);
            for (int i = 0; i < 20; i++)
                Cube("Floorboard", new Vector3(-3.8f + i * .4f, .15f, 10), new Vector3(.388f, .06f, 7), i % 3 == 0 ? timberLight : timber, cabin);
            Cube("Porch", new Vector3(0, .08f, 5.85f), new Vector3(8.5f, .15f, 1.3f), timberDark, cabin, true);
            for (int i = 0; i < 21; i++)
                Cube("Porch plank", new Vector3(-4f + i * .4f, .17f, 5.85f), new Vector3(.38f, .055f, 1.3f), timberLight, cabin);
            Cube("Entry step", new Vector3(0, .04f, 4.95f), new Vector3(2.4f, .08f, .55f), timberLight, cabin, true);

            // Front wall has a generous open doorway and two actual openings to the lake.
            WallPanel("Front left lower", new Vector3(-2.6f, .59f, 6.5f), new Vector3(2.8f, .82f, .23f), cabin);
            WallPanel("Front right lower", new Vector3(2.6f, .59f, 6.5f), new Vector3(2.8f, .82f, .23f), cabin);
            WallPanel("Front left upper", new Vector3(-2.6f, 2.85f, 6.5f), new Vector3(2.8f, .6f, .23f), cabin);
            WallPanel("Front right upper", new Vector3(2.6f, 2.85f, 6.5f), new Vector3(2.8f, .6f, .23f), cabin);
            Cube("Door lintel", new Vector3(0, 2.95f, 6.5f), new Vector3(2.4f, .4f, .28f), timberDark, cabin, true);
            foreach (float x in new[] { -4f, -1.15f, 1.15f, 4f })
                Cube("Front timber post", new Vector3(x, 1.65f, 6.5f), new Vector3(.18f, 3.1f, .3f), timberDark, cabin, true);
            Window("Lake window left", new Vector3(-2.6f, 1.79f, 6.5f), 2.65f, 1.52f, 0, cabin);
            Window("Lake window right", new Vector3(2.6f, 1.79f, 6.5f), 2.65f, 1.52f, 0, cabin);

            WallPanel("West wall", new Vector3(-4f, 1.65f, 10), new Vector3(.23f, 3, 7), cabin);
            WallPanel("East wall", new Vector3(4f, 1.65f, 10), new Vector3(.23f, 3, 7), cabin);
            WallPanel("North wall", new Vector3(0, 1.65f, 13.5f), new Vector3(8, 3, .23f), cabin);
            foreach (float x in new[] { -4f, 4f })
                Cube("Back timber post", new Vector3(x, 1.65f, 13.5f), new Vector3(.28f, 3.12f, .28f), timberDark, cabin);
            foreach (float x in new[] { -2.5f, 0, 2.5f })
                Cube("Exposed ceiling beam", new Vector3(x, 3.05f, 10), new Vector3(.18f, .18f, 7), timberDark, cabin);
            Cube("Roof ridge beam", new Vector3(0, 4.3f, 10), new Vector3(.18f, .18f, 8.1f), timberDark, cabin);
            for (int side = -1; side <= 1; side += 2)
            {
                var panel = Cube("Cedar roof", new Vector3(side * 2.15f, 3.67f, 10), new Vector3(4.53f, .18f, 8.2f), roof, cabin);
                panel.transform.rotation = Quaternion.Euler(0, 0, -side * 16);
                for (int i = 0; i < 9; i++)
                {
                    var seam = Cube("Roof batten", new Vector3(side * 2.15f, 3.78f, 6.1f + i * .98f), new Vector3(4.55f, .045f, .045f), timberDark, cabin);
                    seam.transform.rotation = panel.transform.rotation;
                }
            }
            Gable("Front gable", 6.5f, cabin);
            Gable("Back gable", 13.5f, cabin);
            Cube("Chimney", new Vector3(-2.65f, 4.25f, 12), new Vector3(.4f, 1.9f, .4f), iron, cabin);
            Cube("Chimney cap", new Vector3(-2.65f, 5.2f, 12), new Vector3(.65f, .1f, .65f), iron, cabin);

            var door = Cube("Open cedar door", new Vector3(-1.58f, 1.45f, 5.65f), new Vector3(.11f, 2.55f, 1.65f), timber, cabin);
            door.transform.rotation = Quaternion.Euler(0, -15, 0);
            Cube("Welcome mat", new Vector3(0, .19f, 6.92f), new Vector3(1.6f, .025f, .8f), sage, cabin);
            Cube("Woven rug", new Vector3(0, .195f, 10.3f), new Vector3(2.25f, .025f, 2.7f), linen, cabin);
            for (int i = 0; i < 5; i++)
                Cube("Rug woven stripe", new Vector3(0, .212f, 9.35f + i * .47f), new Vector3(2.24f, .008f, .07f), sage, cabin);

            BuildStove(game, cabin);
            BuildCooking(game, cabin);
            BuildBed(game, cabin);
            BuildStorage(game, cabin);
            BuildDesk(game, cabin);
            // A little still life makes the room look inhabited before any action is taken.
            Cube("Book shelf", new Vector3(-.5f, 2f, 13.28f), new Vector3(2.4f, .12f, .4f), timberDark, cabin);
            var bookColors = new[] { sage, linen, timberDark, roof };
            for (int i = 0; i < 9; i++)
                Cube("Cloth bound book", new Vector3(-1.4f + i * .19f, 2.23f, 13.28f), new Vector3(.13f, .38f + (i % 3) * .05f, .28f), bookColors[i % 4], cabin);
            Cube("Blank wall print", new Vector3(.3f, 2.02f, 13.33f), new Vector3(.72f, .97f, .06f), timberDark, cabin);
            Cube("Print paper", new Vector3(.3f, 2.02f, 13.285f), new Vector3(.61f, .86f, .015f), linen, cabin);
            Cube("Print horizon", new Vector3(.3f, 1.94f, 13.27f), new Vector3(.51f, .19f, .008f), sage, cabin);
        }

        static void BuildStove(WaldenGame game, Transform parent)
        {
            var stove = Group("Wood stove", parent);
            stove.position = new Vector3(-2.65f, .18f, 11.95f);
            LocalCube("Hearth stone", new Vector3(0, .035f, 0), new Vector3(1.5f, .07f, 1.45f), stone, stove);
            LocalCube("Cast iron stove", new Vector3(0, .63f, 0), new Vector3(.95f, .95f, .78f), iron, stove);
            LocalCube("Stove top", new Vector3(0, 1.13f, 0), new Vector3(1.08f, .09f, .9f), iron, stove);
            foreach (float x in new[] { -.36f, .36f })
                foreach (float z in new[] { -.27f, .27f })
                    LocalCube("Iron foot", new Vector3(x, .17f, z), new Vector3(.09f, .3f, .09f), iron, stove);
            LocalCube("Firebox frame", new Vector3(0, .67f, -.41f), new Vector3(.72f, .6f, .045f), timberDark, stove);
            var glow = Material("Ember glow", new Color(.42f, .13f, .035f));
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor", new Color(.5f, .13f, .025f));
            var embers = LocalCube("Firebox embers", new Vector3(0, .58f, -.442f), new Vector3(.55f, .33f, .015f), glow, stove);
            var flames = Group("Fire", stove);
            flames.localPosition = new Vector3(0, .48f, -.46f);
            embers.transform.SetParent(flames, true);
            var flameMat = Material("Flame gold", new Color(1f, .49f, .09f));
            flameMat.EnableKeyword("_EMISSION");
            flameMat.SetColor("_EmissionColor", new Color(1.5f, .42f, .025f));
            for (int i = 0; i < 3; i++)
            {
                var flame = MeshObject("Flame", ConeMesh(5), flameMat, flames);
                flame.transform.localPosition = new Vector3(-.17f + i * .17f, 0, 0);
                flame.transform.localScale = new Vector3(.13f, .22f + i % 2 * .12f, .055f);
            }
            game.Flame = flames;
            var lightObject = new GameObject("Stove firelight");
            lightObject.transform.SetParent(stove, false);
            lightObject.transform.localPosition = new Vector3(0, .8f, -.8f);
            var firelight = lightObject.AddComponent<Light>();
            firelight.type = LightType.Point;
            firelight.color = new Color(1f, .55f, .2f);
            firelight.range = 6;
            firelight.intensity = 0;
            firelight.shadows = LightShadows.None;
            game.StoveLight = firelight;
            Cylinder("Flue pipe", new Vector3(-2.65f, 2.45f, 12.12f), .13f, 2.9f, iron, parent);

            var kettle = Group("Kettle", stove);
            kettle.localPosition = new Vector3(0, 1.19f, 0);
            var body = Primitive(PrimitiveType.Sphere, "Enamel kettle", iron, kettle);
            body.transform.localScale = new Vector3(.44f, .34f, .44f);
            body.transform.localPosition = new Vector3(0, .15f, 0);
            LocalCube("Kettle lid", new Vector3(0, .31f, 0), new Vector3(.22f, .04f, .22f), iron, kettle);
            Segment("Kettle spout", new Vector3(.14f, .14f, 0), new Vector3(.31f, .28f, 0), .055f, iron, kettle, true);
            Segment("Kettle handle left", new Vector3(-.16f, .18f, .03f), new Vector3(-.16f, .48f, .03f), .022f, timberDark, kettle, true);
            Segment("Kettle handle right", new Vector3(.16f, .18f, .03f), new Vector3(.16f, .48f, .03f), .022f, timberDark, kettle, true);
            Segment("Kettle handle", new Vector3(-.16f, .48f, .03f), new Vector3(.16f, .48f, .03f), .025f, timberDark, kettle, true);
            game.Steam = Steam(kettle, new Vector3(.2f, .35f, 0));

            var collider = stove.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, .7f, 0);
            collider.size = new Vector3(1.2f, 1.5f, 1.05f);
            game.Register(InteractionKind.Stove, stove.gameObject, "生火烧水");
        }

        static void BuildCooking(WaldenGame game, Transform parent)
        {
            var kitchen = Group("Simple cooking corner", parent);
            kitchen.position = new Vector3(-1.15f, .18f, 12);
            LocalCube("Preparation table", new Vector3(0, .77f, 0), new Vector3(1.08f, .12f, .82f), timberLight, kitchen);
            foreach (float x in new[] { -.43f, .43f })
                foreach (float z in new[] { -.29f, .29f })
                    LocalCube("Preparation table leg", new Vector3(x, .37f, z), new Vector3(.085f, .74f, .085f), timberDark, kitchen);
            LocalCube("Stone cooking slab", new Vector3(-.07f, .86f, .03f), new Vector3(.67f, .06f, .64f), stone, kitchen);
            var pot = Group("Soup pot", kitchen);
            pot.localPosition = new Vector3(-.07f, .9f, .03f);
            Cylinder("Iron soup pot", new Vector3(0, .16f, 0), .26f, .29f, iron, pot, true);
            Cylinder("Pot lip", new Vector3(0, .32f, 0), .275f, .035f, iron, pot, true);
            Cylinder("Simmering broth", new Vector3(0, .343f, 0), .242f, .012f, Material("Soup in the cooking pot", new Color(.59f, .35f, .13f), .3f), pot, true);
            foreach (float side in new[] { -1f, 1f })
            {
                Segment("Pot handle", new Vector3(side * .23f, .25f, 0), new Vector3(side * .38f, .25f, 0), .035f, iron, pot, true);
                Segment("Pot handle grip", new Vector3(side * .38f, .25f, -.08f), new Vector3(side * .38f, .25f, .08f), .035f, timberDark, pot, true);
            }
            LocalCube("Small cutting board", new Vector3(.38f, .851f, -.1f), new Vector3(.24f, .025f, .39f), timberDark, kitchen);
            LocalCube("Kitchen knife", new Vector3(.39f, .875f, -.1f), new Vector3(.032f, .015f, .27f), iron, kitchen);
            LocalCube("Knife handle", new Vector3(.39f, .88f, -.25f), new Vector3(.047f, .027f, .12f), timberLight, kitchen);
            game.CookingSteam = Steam(pot, new Vector3(0, .35f, 0));
            game.CookingSteam.name = "Soup steam";
            var collider = kitchen.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, .7f, 0);
            collider.size = new Vector3(1.15f, 1.4f, .9f);
            game.Register(InteractionKind.Cooking, kitchen.gameObject, "煮一锅家常热汤");
        }

        static void BuildBed(WaldenGame game, Transform parent)
        {
            var bed = Group("Bed", parent);
            bed.position = new Vector3(2.72f, .18f, 11.7f);
            LocalCube("Bed frame", new Vector3(0, .32f, 0), new Vector3(1.5f, .2f, 2.65f), timberDark, bed);
            LocalCube("Mattress", new Vector3(0, .53f, 0), new Vector3(1.39f, .27f, 2.48f), linen, bed);
            LocalCube("Wool blanket", new Vector3(0, .69f, -.3f), new Vector3(1.42f, .075f, 1.65f), sage, bed);
            for (int i = 0; i < 3; i++)
                LocalCube("Blanket stripe", new Vector3(0, .731f, -.85f + i * .12f), new Vector3(1.42f, .01f, .035f), linen, bed);
            LocalCube("Pillow", new Vector3(0, .76f, .83f), new Vector3(.94f, .18f, .48f), linen, bed);
            LocalCube("Head board", new Vector3(0, .72f, 1.3f), new Vector3(1.55f, 1.2f, .12f), timber, bed);
            foreach (float x in new[] { -.6f, .6f })
                foreach (float z in new[] { -1f, 1f })
                    LocalCube("Bed leg", new Vector3(x, .17f, z), new Vector3(.13f, .34f, .13f), timberDark, bed);
            var collider = bed.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, .5f, 0);
            collider.size = new Vector3(1.55f, .9f, 2.7f);
            game.Register(InteractionKind.Bed, bed.gameObject, "休息，迎接明天");
        }

        static void BuildStorage(WaldenGame game, Transform parent)
        {
            var shelf = Group("Supplies shelf", parent);
            shelf.position = new Vector3(-3.28f, .18f, 8.4f);
            LocalCube("Shelf back", new Vector3(-.24f, .8f, 0), new Vector3(.1f, 1.6f, 1.35f), timberDark, shelf);
            foreach (float y in new[] { .1f, .7f, 1.35f, 1.65f })
                LocalCube("Shelf board", new Vector3(0, y, 0), new Vector3(.7f, .09f, 1.35f), timber, shelf);
            foreach (float z in new[] { -.64f, .64f })
                LocalCube("Shelf support", new Vector3(0, .85f, z), new Vector3(.66f, 1.7f, .07f), timberDark, shelf);
            for (int i = 0; i < 7; i++)
            {
                var log = Cylinder("Stored kindling", new Vector3(0, .27f + (i / 3) * .13f, -.38f + i % 3 * .28f), .085f, .55f, timberLight, shelf, true);
                log.transform.localRotation = Quaternion.Euler(0, 0, 90);
            }
            for (int i = 0; i < 3; i++)
            {
                Cylinder("Pantry jar", new Vector3(0, 1.05f, -.37f + i * .37f), .115f, .42f, linen, shelf, true);
                Cylinder("Jar cork", new Vector3(0, 1.28f, -.37f + i * .37f), .12f, .04f, timberLight, shelf, true);
            }
            var collider = shelf.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, .85f, 0);
            collider.size = new Vector3(.75f, 1.7f, 1.4f);
            game.Register(InteractionKind.Storage, shelf.gameObject, "查看生活物资");
        }

        static void BuildDesk(WaldenGame game, Transform parent)
        {
            var desk = Group("Window table", parent);
            desk.position = new Vector3(2.55f, .18f, 7.45f);
            LocalCube("Table top", new Vector3(0, .77f, 0), new Vector3(1.65f, .12f, .83f), timberLight, desk);
            foreach (float x in new[] { -.66f, .66f })
                foreach (float z in new[] { -.28f, .28f })
                    LocalCube("Table leg", new Vector3(x, .37f, z), new Vector3(.09f, .74f, .09f), timberDark, desk);
            var tableCollider = desk.gameObject.AddComponent<BoxCollider>();
            tableCollider.center = new Vector3(0, .76f, 0);
            tableCollider.size = new Vector3(1.65f, .12f, .83f);
            var tray = Group("Tea tray", desk);
            tray.localPosition = new Vector3(-.33f, .855f, 0);
            LocalCube("Wooden tea tray", Vector3.zero, new Vector3(.55f, .045f, .46f), timberDark, tray);
            var trayCollider = tray.gameObject.AddComponent<BoxCollider>();
            trayCollider.size = new Vector3(.59f, .32f, .5f);
            trayCollider.center = new Vector3(0, .12f, 0);
            game.Register(InteractionKind.Tea, tray.gameObject, "倒一杯热茶");
            var tea = Group("Cup of tea", tray);
            tea.localPosition = new Vector3(0, .035f, 0);
            Cylinder("Ceramic cup", new Vector3(0, .11f, 0), .105f, .2f, linen, tea, true);
            Cylinder("Tea surface", new Vector3(0, .215f, 0), .088f, .005f, Material("Tea", new Color(.29f, .15f, .055f)), tea, true);
            LocalCube("Cup handle", new Vector3(.12f, .1f, 0), new Vector3(.09f, .1f, .045f), linen, tea);
            game.TeaVisual = tea.gameObject;

            var mealTray = Group("Supper place setting", desk);
            mealTray.localPosition = new Vector3(.47f, .855f, -.08f);
            LocalCube("Linen place mat", Vector3.zero, new Vector3(.54f, .016f, .48f), sage, mealTray);
            var mealCollider = mealTray.gameObject.AddComponent<BoxCollider>();
            mealCollider.size = new Vector3(.53f, .31f, .49f);
            mealCollider.center = new Vector3(0, .13f, 0);
            game.Register(InteractionKind.Meal, mealTray.gameObject, "端起一碗热汤");
            var meal = Group("Bowl of homemade soup", mealTray);
            meal.localPosition = new Vector3(0, .02f, 0);
            MeshObject("Stoneware soup bowl", BowlMesh(), linen, meal);
            Cylinder("Soup surface", new Vector3(0, .15f, 0), .18f, .008f, Material("Golden homemade soup", new Color(.69f, .42f, .16f), .38f), meal, true);
            var vegetables = Material("Turnip in the soup", new Color(.86f, .72f, .43f));
            for (int i = 0; i < 4; i++)
                LocalCube("Diced vegetables", new Vector3(-.09f + i % 2 * .12f, .161f, -.07f + i / 2 * .11f), new Vector3(.043f, .022f, .042f), vegetables, meal);
            LocalCube("Soup herbs", new Vector3(.025f, .164f, .015f), new Vector3(.055f, .012f, .025f), sage, meal);
            Segment("Wooden spoon", new Vector3(.2f, .045f, -.19f), new Vector3(.2f, .045f, .17f), .012f, timberLight, meal, true);
            var spoon = Primitive(PrimitiveType.Sphere, "Spoon bowl", timberLight, meal);
            spoon.transform.localPosition = new Vector3(.2f, .045f, .18f);
            spoon.transform.localScale = new Vector3(.065f, .022f, .09f);
            game.MealVisual = meal.gameObject;
            meal.gameObject.SetActive(false);

            LocalCube("Journal", new Vector3(.15f, .86f, .23f), new Vector3(.28f, .055f, .30f), sage, desk);
            LocalCube("Journal paper", new Vector3(.15f, .891f, .23f), new Vector3(.24f, .012f, .26f), linen, desk);
            Segment("Pencil", new Vector3(.04f, .91f, .24f), new Vector3(.24f, .91f, .33f), .008f, timberDark, desk, true);
            Chair("Window chair", new Vector3(2.55f, .18f, 8.6f), 180, game, parent);
        }

        static void BuildPier(WaldenGame game)
        {
            var pier = Group("Lakeside landing");
            Cube("Pier footing", new Vector3(7, -.08f, -9.9f), new Vector3(2.4f, .25f, 7.5f), timberDark, pier, true);
            for (int i = 0; i < 25; i++)
                Cube("Landing plank", new Vector3(7, .08f, -6.3f - i * .3f), new Vector3(2.35f, .08f, .275f), i % 4 == 0 ? timber : timberLight, pier);
            foreach (float z in new[] { -6.4f, -9.8f, -13.25f })
            {
                Cylinder("Mooring post", new Vector3(5.95f, -.15f, z), .095f, 1.65f, timberDark, pier);
                Cylinder("Mooring post", new Vector3(8.05f, -.15f, z), .095f, 1.65f, timberDark, pier);
            }
            Blocker("Landing west safety edge", new Vector3(5.78f, 1.4f, -10.45f), new Vector3(.18f, 4, 7.4f), pier);
            Blocker("Landing east safety edge", new Vector3(8.22f, 1.4f, -10.45f), new Vector3(.18f, 4, 7.4f), pier);
            Blocker("Landing end safety edge", new Vector3(7, 1.4f, -13.82f), new Vector3(2.6f, 4, .18f), pier);

            var bucket = Group("Fresh water", pier);
            bucket.position = new Vector3(7.66f, .13f, -12.75f);
            Cylinder("Water bucket", new Vector3(0, .22f, 0), .23f, .4f, iron, bucket, true);
            Cylinder("Bucket rim", new Vector3(0, .43f, 0), .25f, .055f, timberLight, bucket, true);
            Cylinder("Bucket water", new Vector3(0, .465f, 0), .205f, .01f, Material("Bucket water", new Color(.28f, .47f, .47f), .6f), bucket, true);
            Segment("Bucket handle left", new Vector3(-.22f, .39f, 0), new Vector3(-.22f, .72f, 0), .018f, iron, bucket, true);
            Segment("Bucket handle right", new Vector3(.22f, .39f, 0), new Vector3(.22f, .72f, 0), .018f, iron, bucket, true);
            Segment("Bucket grip", new Vector3(-.22f, .72f, 0), new Vector3(.22f, .72f, 0), .025f, timberLight, bucket, true);
            var collider = bucket.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, .35f, 0);
            collider.size = new Vector3(.65f, .8f, .65f);
            game.Register(InteractionKind.Water, bucket.gameObject, "取一壶湖水");

            // A short handrail leaves the entire entrance open.
            Segment("Landing handrail", new Vector3(8.07f, .65f, -7), new Vector3(8.07f, .65f, -9.8f), .045f, timberLight, pier);
            BuildFishing(game, pier);
        }

        static void BuildFishing(WaldenGame game, Transform parent)
        {
            var rack = Group("Fishing rod rack", parent);
            rack.position = new Vector3(6.2f, .13f, -11.15f);
            foreach (float z in new[] { -.23f, .23f })
                LocalCube("Rod rack upright", new Vector3(-.08f, .42f, z), new Vector3(.09f, .84f, .09f), timberDark, rack);
            LocalCube("Rod rack crossbar", new Vector3(-.08f, .7f, 0), new Vector3(.1f, .1f, .62f), timberLight, rack);
            LocalCube("Tackle box", new Vector3(.02f, .12f, .11f), new Vector3(.38f, .23f, .39f), sage, rack);
            LocalCube("Tackle box latch", new Vector3(.216f, .13f, .11f), new Vector3(.025f, .08f, .06f), linen, rack);
            Segment("Resting bamboo rod", new Vector3(-.1f, .08f, -.16f), new Vector3(-.24f, 1.85f, .12f), .018f, timberLight, rack, true);
            var collider = rack.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, .51f, 0);
            collider.size = new Vector3(.6f, 1.02f, .7f);
            game.Register(InteractionKind.Fishing, rack.gameObject, "湖边垂钓", new Vector3(6.75f, .13f, -12), 180);

            var stool = Group("Fishing stool", parent);
            stool.position = new Vector3(6.75f, .13f, -12);
            LocalCube("Fishing stool seat", new Vector3(0, .43f, 0), new Vector3(.49f, .08f, .49f), timberLight, stool);
            foreach (float x in new[] { -.18f, .18f })
                foreach (float z in new[] { -.18f, .18f })
                    LocalCube("Fishing stool leg", new Vector3(x, .21f, z), new Vector3(.065f, .42f, .065f), timberDark, stool);

            var rod = Group("Cast fishing rod", parent);
            var butt = new Vector3(7.08f, .88f, -12.1f);
            var middle = new Vector3(7.08f, 1.48f, -14.1f);
            var tip = new Vector3(7.02f, 1.9f, -15.35f);
            Segment("Bamboo fishing rod lower", butt, middle, .021f, timberLight, rod);
            Segment("Bamboo fishing rod tip", middle, tip, .012f, timberLight, rod);
            Segment("Rod cork grip", butt, Vector3.Lerp(butt, middle, .13f), .032f, timberDark, rod);
            var reel = Primitive(PrimitiveType.Cylinder, "Fishing reel", iron, rod);
            reel.transform.position = Vector3.Lerp(butt, middle, .12f) + new Vector3(-.04f, -.06f, 0);
            reel.transform.localScale = new Vector3(.105f, .035f, .105f);
            reel.transform.rotation = Quaternion.Euler(0, 0, 90);
            var tipTransform = Group("Rod tip", rod);
            tipTransform.position = tip;
            game.RodTip = tipTransform;
            game.FishingRod = rod.gameObject;

            var bobber = Group("Fishing float", rod);
            bobber.position = new Vector3(6.8f, -.03f, -18);
            var floatBase = Primitive(PrimitiveType.Sphere, "White float body", linen, bobber);
            floatBase.transform.localPosition = new Vector3(0, -.045f, 0);
            floatBase.transform.localScale = new Vector3(.11f, .12f, .11f);
            var floatRed = Material("Fishing float red", new Color(.79f, .24f, .13f));
            floatRed.EnableKeyword("_EMISSION");
            floatRed.SetColor("_EmissionColor", new Color(.22f, .045f, .015f));
            var floatTip = Primitive(PrimitiveType.Sphere, "Red float cap", floatRed, bobber);
            floatTip.transform.localPosition = new Vector3(0, .025f, 0);
            floatTip.transform.localScale = new Vector3(.09f, .11f, .09f);
            Cylinder("Float antenna", new Vector3(0, .105f, 0), .012f, .09f, floatRed, bobber, true);
            game.FishingBobber = bobber;

            var line = rod.gameObject.AddComponent<LineRenderer>();
            line.name = "Cast fishing rod";
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, tip);
            line.SetPosition(1, bobber.position);
            line.startWidth = .007f;
            line.endWidth = .005f;
            line.numCapVertices = 2;
            line.alignment = LineAlignment.View;
            line.sharedMaterial = Material("Fine fishing line", new Color(.80f, .79f, .66f));
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            game.FishingLine = line;
            rod.gameObject.SetActive(false);
        }

        static void BuildGarden(WaldenGame game)
        {
            var garden = Group("The little kitchen garden");
            Path("Path to the vegetable garden", new[] { new Vector3(2.3f, 0, 3), new Vector3(5.8f, 0, 1.2f), new Vector3(11.8f, 0, 1.2f) }, 1.05f, garden);
            var leaf = Material("Turnip leaves", new Color(.31f, .46f, .24f));
            var youngLeaf = Material("Young turnip leaves", new Color(.47f, .59f, .30f));
            var turnip = Material("Cream turnip", new Color(.89f, .78f, .56f));
            var turnipTop = Material("Turnip shoulders", new Color(.69f, .39f, .32f));
            for (int plotIndex = 0; plotIndex < 3; plotIndex++)
            {
                var plot = Group("Turnip bed " + (plotIndex + 1), garden);
                plot.position = new Vector3(7.1f + plotIndex * 2.1f, 0, 2.5f);
                var drySoil = Material("Garden soil " + (plotIndex + 1), new Color(.37f, .28f, .18f));
                var patch = LocalCube("Turned earth", new Vector3(0, .10f, 0), new Vector3(1.5f, .12f, 1.1f), drySoil, plot);
                game.GardenSoils[plotIndex] = patch.GetComponent<Renderer>();
                foreach (float x in new[] { -.8f, .8f })
                    LocalCube("Garden bed side", new Vector3(x, .10f, 0), new Vector3(.08f, .2f, 1.25f), timberLight, plot);
                foreach (float z in new[] { -.59f, .59f })
                    LocalCube("Garden bed end", new Vector3(0, .10f, z), new Vector3(1.65f, .2f, .08f), timberLight, plot);
                for (int row = 0; row < 3; row++)
                    LocalCube("Sowing furrow", new Vector3(-.48f + row * .48f, .164f, 0), new Vector3(.035f, .009f, .92f), soil, plot);
                LocalCube("Garden marker stake", new Vector3(.62f, .3f, .43f), new Vector3(.045f, .6f, .045f), timberDark, plot);
                LocalCube("Garden marker", new Vector3(.62f, .54f, .43f), new Vector3(.28f, .19f, .035f), linen, plot);
                var markerBulb = Primitive(PrimitiveType.Sphere, "Turnip marker symbol", turnipTop, plot);
                markerBulb.transform.localPosition = new Vector3(.62f, .535f, .406f);
                markerBulb.transform.localScale = new Vector3(.075f, .075f, .012f);

                var seedlings = Group("Young turnips", plot);
                seedlings.localPosition = Vector3.up * .16f;
                var mature = Group("Harvestable turnips", plot);
                for (int plantIndex = 0; plantIndex < 4; plantIndex++)
                {
                    var location = new Vector3(-.38f + plantIndex % 2 * .76f, .16f, -.26f + plantIndex / 2 * .52f);
                    for (int blade = 0; blade < 2; blade++)
                    {
                        var sprout = MeshObject("Small leaf", CrownMesh(), youngLeaf, seedlings);
                        sprout.transform.localPosition = new Vector3(location.x, 0, location.z) + new Vector3(blade == 0 ? -.035f : .035f, .08f, 0);
                        sprout.transform.localScale = new Vector3(.04f, .13f, .025f);
                        sprout.transform.localRotation = Quaternion.Euler(0, plantIndex * 47, blade == 0 ? 28 : -28);
                    }
                    var bulb = Primitive(PrimitiveType.Sphere, "Turnip bulb", turnip, mature);
                    bulb.transform.localPosition = location + Vector3.up * .07f;
                    bulb.transform.localScale = new Vector3(.23f, .24f, .23f);
                    var shoulder = Primitive(PrimitiveType.Sphere, "Turnip shoulder", turnipTop, mature);
                    shoulder.transform.localPosition = location + Vector3.up * .14f;
                    shoulder.transform.localScale = new Vector3(.215f, .12f, .215f);
                    for (int blade = 0; blade < 3; blade++)
                    {
                        float angle = blade * 120 + plantIndex * 23;
                        var vegetableLeaf = MeshObject("Turnip leaf", CrownMesh(), leaf, mature);
                        vegetableLeaf.transform.localPosition = location + new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * .065f, .31f, Mathf.Cos(angle * Mathf.Deg2Rad) * .065f);
                        vegetableLeaf.transform.localScale = new Vector3(.065f, .23f, .035f);
                        vegetableLeaf.transform.localRotation = Quaternion.Euler(25, angle, 18);
                    }
                }
                game.GardenSeedlings[plotIndex] = seedlings.gameObject;
                game.GardenMature[plotIndex] = mature.gameObject;
                seedlings.gameObject.SetActive(false);
                mature.gameObject.SetActive(false);
                var collider = plot.gameObject.AddComponent<BoxCollider>();
                collider.center = new Vector3(0, .12f, 0);
                collider.size = new Vector3(1.7f, .24f, 1.3f);
                game.Register(InteractionKind.GardenPlot, plot.gameObject, "照料小菜畦").resourceId = plotIndex;
            }

            var toolTable = Group("Garden seed table", garden);
            toolTable.position = new Vector3(11.2f, 0, 4.3f);
            LocalCube("Garden worktop", new Vector3(0, .73f, 0), new Vector3(1.1f, .09f, .6f), timberLight, toolTable);
            foreach (float x in new[] { -.43f, .43f })
                foreach (float z in new[] { -.2f, .2f })
                    LocalCube("Garden table leg", new Vector3(x, .35f, z), new Vector3(.07f, .7f, .07f), timberDark, toolTable);
            LocalCube("Seed packet", new Vector3(-.27f, .81f, -.04f), new Vector3(.22f, .055f, .28f), linen, toolTable);
            LocalCube("Seed packet leaf", new Vector3(-.27f, .842f, -.04f), new Vector3(.07f, .009f, .13f), leaf, toolTable);
            var can = Group("Watering can", toolTable);
            can.localPosition = new Vector3(.23f, .8f, .02f);
            Cylinder("Watering can body", new Vector3(0, .17f, 0), .18f, .33f, sage, can, true);
            Segment("Watering can spout", new Vector3(.14f, .10f, 0), new Vector3(.48f, .31f, 0), .038f, sage, can, true);
            var rose = Primitive(PrimitiveType.Sphere, "Watering rose", iron, can);
            rose.transform.localPosition = new Vector3(.49f, .32f, 0);
            rose.transform.localScale = new Vector3(.07f, .12f, .12f);
            Segment("Watering can handle left", new Vector3(-.13f, .23f, -.12f), new Vector3(-.13f, .49f, -.12f), .02f, timberDark, can, true);
            Segment("Watering can handle right", new Vector3(-.13f, .23f, .12f), new Vector3(-.13f, .49f, .12f), .02f, timberDark, can, true);
            Segment("Watering can grip", new Vector3(-.13f, .49f, -.12f), new Vector3(-.13f, .49f, .12f), .025f, timberLight, can, true);
            for (int i = 0; i < 5; i++)
                Cube("Low garden fence post", new Vector3(6.2f + i * 1.65f, .43f, 5), new Vector3(.1f, .86f, .1f), timberDark, garden);
            foreach (float y in new[] { .32f, .66f })
                Cube("Low garden fence rail", new Vector3(9.5f, y, 5), new Vector3(6.7f, .07f, .055f), timberLight, garden);
        }

        static void BuildWoodland(WaldenGame game)
        {
            var forest = Group("Woodland");
            var foliage = new[]
            {
                Material("Pine · deep", new Color(.16f, .28f, .22f)),
                Material("Pine · soft", new Color(.22f, .35f, .26f)),
                Material("Birch · green", new Color(.38f, .47f, .27f)),
                Material("Birch · pale", new Color(.47f, .53f, .31f)),
                Material("Fir · silver", new Color(.29f, .39f, .33f))
            };
            var birch = Material("Birch bark", new Color(.72f, .71f, .60f));
            var closeTrees = new[] { new Vector2(-10, 2), new Vector2(-12, 11), new Vector2(10, 8), new Vector2(12, 15), new Vector2(-7, 21), new Vector2(6, 24), new Vector2(-16, -1), new Vector2(16, -1) };
            for (int i = 0; i < closeTrees.Length; i++)
                Tree(new Vector3(closeTrees[i].x, 0, closeTrees[i].y), 6.5f + i % 3, i % 3 == 0, foliage, birch, forest);
            for (int i = 0; i < 65; i++)
            {
                float x = Next(-35, 35), z = Next(-4, 39);
                if ((Mathf.Abs(x) < 10 && z < 25) || InGarden(x, z)) x = Mathf.Sign(x == 0 ? 1 : x) * Next(13.5f, 34);
                Tree(new Vector3(x, 0, z), Next(5.7f, 10.4f), i % 4 == 0, foliage, birch, forest);
            }
            for (int i = 0; i < 38; i++)
                Tree(new Vector3(-96 + i * 5.2f, .1f, Next(-103, -94)), Next(5.5f, 10.5f), i % 7 == 0, foliage, birch, forest, false);

            for (int i = 0; i < 32; i++)
            {
                float x = Next(-32, 32), z = Next(-5.5f, 33);
                if (InClearing(x, z)) continue;
                var rock = MeshObject("Mossy granite", CrownMesh(), i % 3 == 0 ? grass : stone, forest);
                rock.transform.position = new Vector3(x, -.11f, z);
                rock.transform.localScale = new Vector3(Next(.5f, 1.4f), Next(.35f, .8f), Next(.5f, 1.2f));
                rock.transform.rotation = Quaternion.Euler(0, Next(0, 360), 0);
            }
            var branchPositions = new[]
            {
                new Vector2(2.9f, .15f), new Vector2(-2.5f, 2.7f), new Vector2(-5.7f, 1.0f),
                new Vector2(-7.1f, 5.2f), new Vector2(5.4f, 3.2f), new Vector2(8.8f, 6.8f),
                new Vector2(-6.6f, 16.2f), new Vector2(4.6f, 19.2f)
            };
            for (int i = 0; i < branchPositions.Length; i++)
            {
                var branch = Group("Fallen branches " + (i + 1), forest);
                branch.position = new Vector3(branchPositions[i].x, .11f, branchPositions[i].y);
                branch.rotation = Quaternion.Euler(0, i * 57, 0);
                Segment("Dry branch", new Vector3(-.48f, 0, 0), new Vector3(.46f, .035f, .12f), .055f, timberLight, branch, true);
                Segment("Twig", new Vector3(-.05f, .025f, .06f), new Vector3(.31f, .04f, .4f), .03f, timber, branch, true);
                Segment("Dry branch", new Vector3(-.32f, .05f, .31f), new Vector3(.31f, .08f, -.17f), .043f, timberLight, branch, true);
                var target = branch.gameObject.AddComponent<BoxCollider>();
                target.size = new Vector3(1.15f, .42f, .8f);
                target.center = new Vector3(0, .12f, .1f);
                var interactable = game.Register(InteractionKind.Branch, branch.gameObject, "拾起干柴");
                interactable.resourceId = i;
            }
        }

        static void BuildMeadow()
        {
            var meadow = Group("Grasses and wildflowers");
            var grassMaterials = new[]
            {
                Material("Grass · olive", new Color(.39f, .43f, .24f)),
                Material("Grass · gold", new Color(.57f, .55f, .33f)),
                Material("Grass · fern", new Color(.31f, .40f, .27f))
            };
            foreach (var mat in grassMaterials) mat.SetInt("_Cull", 0);
            for (int batch = 0; batch < 3; batch++)
            {
                var vertices = new List<Vector3>();
                var triangles = new List<int>();
                for (int i = 0; i < 150; i++)
                {
                    float x = Next(-29, 29), z = Next(-5.5f, 32);
                    if (InClearing(x, z)) continue;
                    float height = Next(.16f, .43f);
                    for (int blade = 0; blade < 4; blade++)
                    {
                        float a = Next(0, Mathf.PI * 2);
                        var center = new Vector3(x + Next(-.12f, .12f), .015f, z + Next(-.12f, .12f));
                        var side = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * .047f;
                        int index = vertices.Count;
                        vertices.Add(center - side);
                        vertices.Add(center + side);
                        vertices.Add(center + new Vector3(.06f, height, .02f));
                        triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
                    }
                }
                var mesh = new Mesh { name = "Meadow grass batch" };
                mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals();
                var plants = MeshObject("Meadow grass", mesh, grassMaterials[batch], meadow);
                plants.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            var flowers = Material("Buttercup yellow", new Color(.88f, .72f, .29f));
            var flowerVertices = new List<Vector3>();
            var flowerTriangles = new List<int>();
            for (int i = 0; i < 65; i++)
            {
                float x = Next(-13, 13), z = Next(-5, 6);
                if (InClearing(x, z)) continue;
                var center = new Vector3(x, Next(.17f, .3f), z);
                for (int petal = 0; petal < 5; petal++)
                {
                    float a = petal * Mathf.PI * 2 / 5;
                    int index = flowerVertices.Count;
                    flowerVertices.Add(center);
                    flowerVertices.Add(center + new Vector3(Mathf.Cos(a) * .07f, -.02f, Mathf.Sin(a) * .07f));
                    flowerVertices.Add(center + new Vector3(Mathf.Cos(a + .8f) * .07f, -.02f, Mathf.Sin(a + .8f) * .07f));
                    flowerTriangles.Add(index); flowerTriangles.Add(index + 2); flowerTriangles.Add(index + 1);
                }
            }
            var flowerMesh = new Mesh { name = "Wildflowers" };
            flowerMesh.SetVertices(flowerVertices); flowerMesh.SetTriangles(flowerTriangles, 0); flowerMesh.RecalculateNormals();
            MeshObject("Buttercups", flowerMesh, flowers, meadow).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static bool InClearing(float x, float z)
        {
            if (InGarden(x, z)) return true;
            if (Mathf.Abs(x) < 4.8f && z > 4.4f && z < 14.5f) return true;
            if (Mathf.Abs(x) < 2 && z > -3 && z < 6) return true;
            if (x > 4.7f && x < 8.5f && z < -3) return true;
            if (x > -6.2f && x < -3.8f && z < -3.5f) return true;
            return false;
        }

        static bool InGarden(float x, float z) => x > 5.8f && x < 13.4f && z > .4f && z < 5.6f;

        static void Chair(string name, Vector3 position, float yaw, WaldenGame game, Transform parent)
        {
            var chair = Group(name, parent);
            chair.position = position;
            // Local forward is the seated person's forward; the back is behind them.
            chair.rotation = Quaternion.Euler(0, yaw, 0);
            LocalCube("Seat", new Vector3(0, .48f, 0), new Vector3(.68f, .09f, .65f), timberLight, chair);
            foreach (float x in new[] { -.26f, .26f })
                foreach (float z in new[] { -.24f, .24f })
                    LocalCube("Chair leg", new Vector3(x, .23f, z), new Vector3(.08f, .46f, .08f), timberDark, chair);
            foreach (float x in new[] { -.3f, .3f })
                LocalCube("Chair back post", new Vector3(x, .82f, -.3f), new Vector3(.075f, .75f, .075f), timberDark, chair);
            for (int i = 0; i < 3; i++)
                LocalCube("Chair back slat", new Vector3(0, .72f + i * .17f, -.3f), new Vector3(.65f, .1f, .075f), timberLight, chair);
            var target = chair.gameObject.AddComponent<BoxCollider>();
            target.center = new Vector3(0, .55f, 0);
            target.size = new Vector3(.75f, 1.15f, .75f);
            game.Register(InteractionKind.Seat, chair.gameObject, name == "Lake chair" ? "坐下，听听湖水" : "坐在窗边", position + chair.forward * .07f, yaw);
        }

        static void Tree(Vector3 position, float height, bool broadleaf, Material[] foliage, Material bark, Transform parent, bool collision = true)
        {
            var tree = Group(broadleaf ? "Birch" : "Pine", parent);
            tree.position = position;
            tree.rotation = Quaternion.Euler(0, Next(0, 360), 0);
            float trunkHeight = broadleaf ? height * .7f : height * .65f;
            var trunk = Cylinder("Trunk", new Vector3(0, trunkHeight * .5f, 0), broadleaf ? .15f : .21f, trunkHeight, broadleaf ? bark : timberDark, tree, true);
            if (collision)
            {
                var capsule = trunk.AddComponent<CapsuleCollider>();
                capsule.radius = .5f;
                capsule.height = 2;
            }
            if (broadleaf)
            {
                for (int i = 0; i < 3; i++)
                {
                    var crown = MeshObject("Birch crown", CrownMesh(), foliage[2 + i % 2], tree);
                    crown.transform.localPosition = new Vector3(i == 0 ? 0 : (i == 1 ? -1.15f : 1.05f), height * (.64f + i * .07f), i == 2 ? .4f : 0);
                    crown.transform.localScale = new Vector3(height * .26f, height * .30f, height * .24f);
                    crown.transform.localRotation = Quaternion.Euler(0, i * 47, 0);
                }
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    var crown = MeshObject("Pine crown", ConeMesh(9), foliage[i == 2 ? 4 : i % 2], tree);
                    crown.transform.localPosition = new Vector3(0, height * (.26f + i * .18f), 0);
                    float radius = height * (.27f - i * .055f);
                    crown.transform.localScale = new Vector3(radius, height * .4f, radius);
                }
            }
        }

        static void WallPanel(string name, Vector3 center, Vector3 size, Transform parent)
        {
            Cube(name, center, size, timber, parent, true);
            int count = Mathf.Max(1, Mathf.RoundToInt(size.y / .22f));
            for (int i = 0; i < count; i++)
            {
                var boardSize = size;
                boardSize.y = size.y / count - .014f;
                boardSize.x += size.x > 1 ? .006f : .025f;
                boardSize.z += size.z > 1 ? .006f : .025f;
                Cube("Cedar wall board", center + Vector3.up * (-size.y * .5f + (i + .5f) * size.y / count), boardSize, i % 4 == 0 ? timberLight : timber, parent);
            }
        }

        static void Window(string name, Vector3 center, float width, float height, float yaw, Transform parent)
        {
            var frame = Group(name, parent);
            frame.position = center;
            frame.rotation = Quaternion.Euler(0, yaw, 0);
            foreach (float y in new[] { -height * .5f, height * .5f })
                LocalCube("Window horizontal frame", new Vector3(0, y, 0), new Vector3(width + .13f, .095f, .34f), timberDark, frame);
            foreach (float x in new[] { -width * .5f, width * .5f })
                LocalCube("Window side frame", new Vector3(x, 0, 0), new Vector3(.08f, height, .3f), timberDark, frame);
            LocalCube("Window mullion", Vector3.zero, new Vector3(.045f, height, .12f), timberDark, frame);
            LocalCube("Deep window sill", new Vector3(0, -height * .5f - .04f, .1f), new Vector3(width + .3f, .09f, .55f), timberLight, frame);
            // Linen curtains stay to the sides, leaving the view itself completely open.
            foreach (float x in new[] { -width * .5f + .12f, width * .5f - .12f })
                LocalCube("Linen curtain", new Vector3(x, .01f, .19f), new Vector3(.16f, height * .92f, .06f), linen, frame);
        }

        static void Gable(string name, float z, Transform parent)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = new[] { new Vector3(-4, 3.13f, z), new Vector3(4, 3.13f, z), new Vector3(0, 4.3f, z), new Vector3(-4, 3.13f, z), new Vector3(0, 4.3f, z), new Vector3(4, 3.13f, z) };
            mesh.triangles = new[] { 0, 1, 2, 3, 4, 5 };
            mesh.RecalculateNormals();
            MeshObject(name, mesh, timber, parent);
        }

        static ParticleSystem Steam(Transform parent, Vector3 localPosition)
        {
            var steamObject = new GameObject("Kettle steam");
            steamObject.transform.SetParent(parent, false);
            steamObject.transform.localPosition = localPosition;
            steamObject.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var steam = steamObject.AddComponent<ParticleSystem>();
            var main = steam.main;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.8f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.13f, .23f);
            main.startSize = new ParticleSystem.MinMaxCurve(.035f, .08f);
            main.startColor = new Color(.82f, .85f, .78f, .25f);
            main.maxParticles = 35;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = steam.emission;
            emission.rateOverTime = 12;
            emission.enabled = false;
            var shape = steam.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15;
            shape.radius = .035f;
            var size = steam.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .5f, 1, 2.5f));
            var color = steam.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.2f, .2f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            // A lit mesh particle avoids depending on a separately stripped particle shader.
            var renderer = steamObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = CrownMesh();
            renderer.sharedMaterial = linen;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            // Keep the simulation alive; WaldenGame switches only the emission module.
            steam.Play();
            return steam;
        }

        static void Path(string name, Vector3[] points, float width, Transform parent)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < points.Length; i++)
            {
                var direction = i == points.Length - 1 ? points[i] - points[i - 1] : points[i + 1] - points[Mathf.Max(0, i - 1)];
                var perpendicular = new Vector3(-direction.z, 0, direction.x).normalized * width * .5f;
                vertices.Add(points[i] - perpendicular + Vector3.up * .014f);
                vertices.Add(points[i] + perpendicular + Vector3.up * .014f);
                if (i == 0) continue;
                int a = (i - 1) * 2;
                triangles.Add(a); triangles.Add(a + 1); triangles.Add(a + 2);
                triangles.Add(a + 1); triangles.Add(a + 3); triangles.Add(a + 2);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals();
            var path = MeshObject(name, mesh, sand, parent);
            path.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static Transform Group(string name, Transform parent = null)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent != null ? parent : world, false);
            return group;
        }

        static Material Material(string name, Color color, float smoothness = .08f)
        {
            var template = Resources.Load<Material>("WaldenSurface");
            var material = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = name;
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        static GameObject Primitive(PrimitiveType type, string name, Material material, Transform parent, bool collision = false)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision)
            {
                var collider = item.GetComponent<Collider>();
                if (collider != null)
                {
                    collider.enabled = false;
                    if (Application.isPlaying) UnityEngine.Object.Destroy(collider);
                    else UnityEngine.Object.DestroyImmediate(collider);
                }
            }
            return item;
        }

        static GameObject Cube(string name, Vector3 position, Vector3 scale, Material material, Transform parent, bool collision = false)
        {
            var item = Primitive(PrimitiveType.Cube, name, material, parent, collision);
            item.transform.position = position;
            item.transform.localScale = scale;
            return item;
        }

        static GameObject LocalCube(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            var item = Primitive(PrimitiveType.Cube, name, material, parent);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            return item;
        }

        static GameObject Cylinder(string name, Vector3 position, float radius, float height, Material material, Transform parent, bool local = false)
        {
            var item = Primitive(PrimitiveType.Cylinder, name, material, parent);
            if (local) item.transform.localPosition = position;
            else item.transform.position = position;
            item.transform.localScale = new Vector3(radius * 2, height * .5f, radius * 2);
            return item;
        }

        static void Segment(string name, Vector3 from, Vector3 to, float radius, Material material, Transform parent, bool local = false)
        {
            var item = Cylinder(name, (from + to) * .5f, radius, Vector3.Distance(from, to), material, parent, local);
            var rotation = Quaternion.FromToRotation(Vector3.up, to - from);
            if (local) item.transform.localRotation = rotation;
            else item.transform.rotation = rotation;
        }

        static void Blocker(string name, Vector3 position, Vector3 size, Transform parent)
        {
            var item = new GameObject(name) { layer = 2 };
            item.transform.SetParent(parent, false);
            item.transform.position = position;
            item.AddComponent<BoxCollider>().size = size;
        }

        static GameObject MeshObject(string name, Mesh mesh, Material material, Transform parent)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            item.AddComponent<MeshRenderer>().sharedMaterial = material;
            return item;
        }

        static Mesh BowlMesh()
        {
            const int sides = 16;
            var profile = new[] { new Vector2(.10f, 0), new Vector2(.17f, .05f), new Vector2(.225f, .18f), new Vector2(.198f, .18f), new Vector2(.09f, .035f), new Vector2(0, .035f) };
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int side = 0; side < sides; side++)
            {
                float angle = side * Mathf.PI * 2 / sides;
                float nextAngle = (side + 1) * Mathf.PI * 2 / sides;
                for (int ring = 0; ring < profile.Length - 1; ring++)
                {
                    var lower = profile[ring];
                    var upper = profile[ring + 1];
                    int index = vertices.Count;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * lower.x, lower.y, Mathf.Sin(angle) * lower.x));
                    vertices.Add(new Vector3(Mathf.Cos(angle) * upper.x, upper.y, Mathf.Sin(angle) * upper.x));
                    vertices.Add(new Vector3(Mathf.Cos(nextAngle) * lower.x, lower.y, Mathf.Sin(nextAngle) * lower.x));
                    vertices.Add(new Vector3(Mathf.Cos(nextAngle) * upper.x, upper.y, Mathf.Sin(nextAngle) * upper.x));
                    triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
                    triangles.Add(index + 2); triangles.Add(index + 1); triangles.Add(index + 3);
                }
            }
            var mesh = new Mesh { name = "Open stoneware soup bowl" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals();
            return mesh;
        }

        static Mesh PlaneMesh(float width, float depth)
        {
            var mesh = new Mesh { name = "Water surface" };
            mesh.vertices = new[] { new Vector3(-width / 2, 0, -depth / 2), new Vector3(width / 2, 0, -depth / 2), new Vector3(-width / 2, 0, depth / 2), new Vector3(width / 2, 0, depth / 2) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateNormals();
            return mesh;
        }

        static Mesh ConeMesh(int sides)
        {
            if (cones.TryGetValue(sides, out var cached)) return cached;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                float a = Mathf.PI * 2 * i / sides, b = Mathf.PI * 2 * (i + 1) / sides;
                int index = vertices.Count;
                vertices.Add(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)));
                vertices.Add(Vector3.up);
                vertices.Add(new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)));
                triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
                index = vertices.Count;
                vertices.Add(Vector3.zero);
                vertices.Add(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)));
                vertices.Add(new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)));
                triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
            }
            var mesh = new Mesh { name = "Faceted cone " + sides };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals();
            cones[sides] = mesh;
            return mesh;
        }

        static Mesh CrownMesh()
        {
            if (crownMesh != null) return crownMesh;
            float t = (1f + Mathf.Sqrt(5f)) / 2;
            var points = new[]
            {
                new Vector3(-1,t,0), new Vector3(1,t,0), new Vector3(-1,-t,0), new Vector3(1,-t,0),
                new Vector3(0,-1,t), new Vector3(0,1,t), new Vector3(0,-1,-t), new Vector3(0,1,-t),
                new Vector3(t,0,-1), new Vector3(t,0,1), new Vector3(-t,0,-1), new Vector3(-t,0,1)
            };
            var faces = new[] { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8, 3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
            var vertices = new Vector3[faces.Length];
            var triangles = new int[faces.Length];
            for (int i = 0; i < faces.Length; i++) { vertices[i] = points[faces[i]].normalized; triangles[i] = i; }
            crownMesh = new Mesh { name = "Faceted woodland crown" };
            crownMesh.vertices = vertices; crownMesh.triangles = triangles; crownMesh.RecalculateNormals();
            return crownMesh;
        }

        static float Next(float min, float max) => min + (float)random.NextDouble() * (max - min);
    }
}
