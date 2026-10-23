#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MiniGTABuilder
{
    const string ScenePath = "Assets/Scenes/Main.unity";
    const string GeneratedFolder = "Assets/Generated";
    const string MaterialsFolder = GeneratedFolder + "/Materials";

    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

    [MenuItem("MiniGTA/Rebuild V8 City (6000.6.3f1)")]
    public static void BuildEnhancedCity()
    {
        EnsureFolders();
        CityLOD.PrepareImports();
        AssetStoreAssetResolver.ConfigureKenneyCharacterAnimations();
        mats.Clear();
        CreateMaterials();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Random.InitState(31027);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 110f;
        RenderSettings.fogEndDistance = 260f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.62f, 0.66f, 0.72f);

        Material sky = GetMat("Sky");
        if (sky != null) RenderSettings.skybox = sky;

        GameObject city = new GameObject("CITY");
        GameObject ground = new GameObject("Ground & Roads"); ground.transform.SetParent(city.transform);
        GameObject buildings = new GameObject("Buildings"); buildings.transform.SetParent(city.transform);
        GameObject props = new GameObject("Street Props"); props.transform.SetParent(city.transform);
        GameObject trafficRoot = new GameObject("Traffic"); trafficRoot.transform.SetParent(city.transform);
        GameObject npcRoot = new GameObject("NPCs"); npcRoot.transform.SetParent(city.transform);
        GameObject systems = new GameObject("Systems");

        BuildGroundAndRoads(ground.transform);
        BuildCityBlocks(buildings.transform, props.transform);
        // Imported buildings are placed once per lot, without overlapping duplicates.
        BuildDistrictLandmarks(buildings.transform, props.transform);
        GarageZone garage = BuildGarageInterior(new Vector3(120f, 0f, -40f), buildings.transform, props.transform);
        BuildCafeInterior(new Vector3(-120f, 0f, 40f), buildings.transform, props.transform);
        List<Light> streetLights = BuildStreetFurniture(props.transform);
        TrafficLightController lights = BuildCentralTrafficLights(props.transform);

        Transform[] routeA = BuildRoute("TrafficRoute_A", new [] {
            new Vector3(3.5f,0.03f,-76.5f), new Vector3(3.5f,0.03f,-8f), new Vector3(8f,0.03f,-3.5f),
            new Vector3(76.5f,0.03f,-3.5f), new Vector3(76.5f,0.03f,-76.5f)
        }, trafficRoot.transform);
        Transform[] routeB = BuildRoute("TrafficRoute_B", new [] {
            new Vector3(-76.5f,0.03f,3.5f), new Vector3(-8f,0.03f,3.5f), new Vector3(-3.5f,0.03f,8f),
            new Vector3(-3.5f,0.03f,76.5f), new Vector3(-76.5f,0.03f,76.5f)
        }, trafficRoot.transform);
        Transform[] routeC = BuildRoute("TrafficRoute_C", new [] {
            new Vector3(83.5f,0.03f,76.5f), new Vector3(83.5f,0.03f,3.5f), new Vector3(88f,0.03f,-3.5f),
            new Vector3(156.5f,0.03f,-3.5f), new Vector3(156.5f,0.03f,76.5f)
        }, trafficRoot.transform);
        Transform[] routeD = BuildRoute("TrafficRoute_D", new [] {
            new Vector3(-156.5f,0.03f,-76.5f), new Vector3(-83.5f,0.03f,-76.5f), new Vector3(-83.5f,0.03f,-3.5f),
            new Vector3(-88f,0.03f,3.5f), new Vector3(-156.5f,0.03f,3.5f)
        }, trafficRoot.transform);
        Transform[] routeE = BuildRoute("TrafficRoute_Outer_E", new [] {
            new Vector3(163.5f,0.03f,-156f), new Vector3(163.5f,0.03f,-83.5f), new Vector3(168f,0.03f,-76.5f),
            new Vector3(226f,0.03f,-76.5f), new Vector3(226f,0.03f,76.5f), new Vector3(163.5f,0.03f,76.5f)
        }, trafficRoot.transform);
        Transform[] routeF = BuildRoute("TrafficRoute_Outer_W", new [] {
            new Vector3(-163.5f,0.03f,156f), new Vector3(-163.5f,0.03f,83.5f), new Vector3(-168f,0.03f,76.5f),
            new Vector3(-226f,0.03f,76.5f), new Vector3(-226f,0.03f,-76.5f), new Vector3(-163.5f,0.03f,-76.5f)
        }, trafficRoot.transform);

        SpawnTraffic(trafficRoot.transform, routeA, routeB, routeC, routeD, routeE, routeF);
        SpawnNPCs(npcRoot.transform);
        BuildParkedCars(trafficRoot.transform);

        GameObject player = BuildPlayer(new Vector3(-18f, 0.28f, -18f));
        GameObject playerCar = BuildDriveableCar("PlayerCar", new Vector3(-7f, 0.03f, -24f), Quaternion.Euler(0f, 15f, 0f), trafficRoot.transform, mats["Red"], 0, true, 0);
        GameObject blueCar = BuildDriveableCar("GarageCar_Blue", new Vector3(112f, 0.03f, -34f), Quaternion.Euler(0f, 180f, 0f), trafficRoot.transform, mats["Blue"], 1, false, 900);
        GameObject sportCar = BuildDriveableCar("GarageCar_Sport", new Vector3(128f, 0.03f, -34f), Quaternion.Euler(0f, 180f, 0f), trafficRoot.transform, mats["Yellow"], 3, false, 1450);
        BuildPlayerAircraft(new Vector3(205f, 0.7f, 125f), Quaternion.Euler(0f, 180f, 0f), trafficRoot.transform);
        Camera cam = BuildCamera(player.transform);

        PlayerController playerController = player.GetComponent<PlayerController>();
        playerController.cameraTransform = cam.transform;
        VehicleInteractor interactor = player.GetComponent<VehicleInteractor>();
        interactor.followCamera = cam.GetComponent<ThirdPersonCamera>();
        WeaponInventory inventory = player.AddComponent<WeaponInventory>();
        inventory.aimCamera = cam;
        inventory.weaponVisuals = BuildWeaponVisuals(player.transform);

        EconomySystem economy = systems.AddComponent<EconomySystem>();
        interactor.economy = economy;
        garage.player = interactor;
        garage.economy = economy;

        NPCWalker[] walkers = Object.FindObjectsByType<NPCWalker>();
        foreach (NPCWalker walker in walkers) walker.player = interactor;

        PoliceCarAI[] police = BuildPoliceFleet(trafficRoot.transform, routeA, routeB, routeC, routeD, routeE, routeF);
        WantedSystem wanted = systems.AddComponent<WantedSystem>();
        wanted.player = interactor;
        wanted.economy = economy;
        wanted.policeCars = police;
        inventory.wanted = wanted;
        BuildFootPolice(npcRoot.transform, wanted);
        foreach (PoliceCarAI policeCar in police) policeCar.wanted = wanted;

        MinimapOverlay minimap = systems.AddComponent<MinimapOverlay>();
        minimap.target = player.transform;
        minimap.worldSize = 78f;
        minimap.fullMapWorldSize = 265f;
        minimap.size = 170f;

        DayNightCycle dayNight = systems.AddComponent<DayNightCycle>();
        dayNight.sun = BuildSun();
        dayNight.streetLights = streetLights.ToArray();
        dayNight.dayLengthSeconds = 300f;
        dayNight.startTime = 0.40f;

        WeatherSystem weather = systems.AddComponent<WeatherSystem>();
        weather.target = player.transform;
        weather.wettableMaterials = new[] { mats["Asphalt"] };
        weather.weatherDuration = 80f;

        StatusOverlay status = systems.AddComponent<StatusOverlay>();
        status.wanted = wanted;
        status.economy = economy;
        status.player = interactor;

        WorldRecovery recovery = systems.AddComponent<WorldRecovery>();
        recovery.player = interactor;
        recovery.playerSpawn = new Vector3(-18f, 0.3f, -18f);
        systems.AddComponent<RuntimeSmokeCapture>();

        BuildMissions(systems.transform, interactor, economy);

        BuildWorldBorder(city.transform);
        CityUpgradePipeline.Apply(scene);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeGameObject = player;
        Debug.Log("MiniGTA V8 city generated for Unity 6000.6.3f1. V8 uses imported Unity Asset Store art when available and always keeps safe local fallbacks.");
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
        if (!AssetDatabase.IsValidFolder(GeneratedFolder)) AssetDatabase.CreateFolder("Assets", "Generated");
        if (!AssetDatabase.IsValidFolder(MaterialsFolder)) AssetDatabase.CreateFolder(GeneratedFolder, "Materials");
    }

    static void CreateMaterials()
    {
        Mat("Grass", new Color(0.20f, 0.38f, 0.18f));
        Mat("GrassBright", new Color(0.30f, 0.50f, 0.22f));
        Mat("Asphalt", new Color(0.105f, 0.115f, 0.125f), 0.08f);
        Mat("RoadMark", new Color(0.92f, 0.90f, 0.72f));
        Mat("Concrete", new Color(0.53f, 0.55f, 0.56f));
        Mat("ConcreteLight", new Color(0.70f, 0.70f, 0.68f));
        Mat("ConcreteDark", new Color(0.30f, 0.31f, 0.32f));
        Mat("Glass", new Color(0.09f, 0.22f, 0.30f), 0.75f, 0.45f);
        Mat("WindowWarm", new Color(0.58f, 0.45f, 0.21f), 0.55f, 0.12f);
        Mat("Metal", new Color(0.20f, 0.22f, 0.24f), 0.75f);
        Mat("Wood", new Color(0.35f, 0.20f, 0.10f), 0.18f);
        Mat("Trunk", new Color(0.25f, 0.14f, 0.065f));
        Mat("Leaves", new Color(0.12f, 0.36f, 0.13f));
        Mat("Water", new Color(0.08f, 0.34f, 0.52f), 0.8f, 0.1f);
        Mat("White", new Color(0.86f, 0.87f, 0.88f));
        Mat("Black", new Color(0.03f, 0.035f, 0.04f));
        Mat("Red", new Color(0.65f, 0.06f, 0.045f), 0.28f);
        Mat("Blue", new Color(0.055f, 0.19f, 0.48f), 0.28f);
        Mat("Yellow", new Color(0.90f, 0.62f, 0.08f), 0.25f);
        Mat("Green", new Color(0.08f, 0.42f, 0.17f), 0.2f);
        Mat("Orange", new Color(0.82f, 0.28f, 0.055f), 0.25f);
        Mat("Silver", new Color(0.50f, 0.53f, 0.56f), 0.6f);
        Mat("SkinA", new Color(0.64f, 0.43f, 0.30f));
        Mat("SkinB", new Color(0.82f, 0.62f, 0.44f));
        Mat("SkinC", new Color(0.43f, 0.27f, 0.19f));
        Mat("HairA", new Color(0.055f, 0.035f, 0.025f));
        Mat("HairB", new Color(0.23f, 0.12f, 0.045f));
        Mat("HairC", new Color(0.48f, 0.34f, 0.12f));
        Mat("ShirtA", new Color(0.12f, 0.32f, 0.58f));
        Mat("ShirtB", new Color(0.54f, 0.11f, 0.12f));
        Mat("ShirtC", new Color(0.12f, 0.48f, 0.31f));
        Mat("ShirtD", new Color(0.50f, 0.34f, 0.08f));
        Mat("Pants", new Color(0.08f, 0.10f, 0.15f));
        Mat("Shoes", new Color(0.05f, 0.05f, 0.055f));
        Mat("BuildingA", new Color(0.55f, 0.47f, 0.39f));
        Mat("BuildingB", new Color(0.36f, 0.43f, 0.50f));
        Mat("BuildingC", new Color(0.60f, 0.58f, 0.51f));
        Mat("BuildingD", new Color(0.47f, 0.34f, 0.30f));
        Mat("BuildingE", new Color(0.30f, 0.38f, 0.35f));
        Mat("BuildingF", new Color(0.52f, 0.52f, 0.58f));
        Mat("RedOn", new Color(1f, 0.02f, 0.01f), 0.3f, 0f, new Color(1f,0.02f,0.01f) * 2.8f);
        Mat("RedOff", new Color(0.16f, 0.01f, 0.01f));
        Mat("GreenOn", new Color(0.02f, 1f, 0.12f), 0.3f, 0f, new Color(0.02f,1f,0.12f) * 2.2f);
        Mat("GreenOff", new Color(0.01f, 0.14f, 0.025f));
        Mat("Marker", new Color(0.12f, 0.78f, 1f), 0.3f, 0f, new Color(0.12f,0.78f,1f) * 1.2f);
        Mat("Invisible", new Color(1f,1f,1f,0f));

        string skyPath = MaterialsFolder + "/Sky.mat";
        Material sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        if (sky == null)
        {
            Shader s = Shader.Find("Skybox/Procedural");
            if (s != null)
            {
                sky = new Material(s);
                sky.SetFloat("_SunSize", 0.04f);
                sky.SetFloat("_AtmosphereThickness", 0.9f);
                sky.SetColor("_SkyTint", new Color(0.45f, 0.58f, 0.72f));
                sky.SetColor("_GroundColor", new Color(0.22f, 0.24f, 0.25f));
                AssetDatabase.CreateAsset(sky, skyPath);
            }
        }
        mats["Sky"] = sky;
    }

    static Material Mat(string name, Color color, float smooth = 0.2f, float metallic = 0f, Color? emission = null)
    {
        string path = MaterialsFolder + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            Shader shader = Shader.Find("Standard");
            m = new Material(shader);
            m.name = name;
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = color;
        m.SetFloat("_Glossiness", smooth);
        m.SetFloat("_Metallic", metallic);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
        }
        EditorUtility.SetDirty(m);
        mats[name] = m;
        return m;
    }

    static Material GetMat(string name) => mats.TryGetValue(name, out Material m) ? m : null;

    static void BuildGroundAndRoads(Transform parent)
    {
        CreateCube("Ground", new Vector3(0f, -0.35f, 0f), new Vector3(500f, 0.7f, 500f), mats["Grass"], parent, true);

        float[] coords = { -226f, -160f, -80f, 0f, 80f, 160f, 226f };
        foreach (float x in coords)
        {
            CreateCube("Road_V_" + x, new Vector3(x, 0.02f, 0f), new Vector3(14f, 0.12f, 480f), mats["Asphalt"], parent, true);
            foreach (float block in new[] { -193f, -120f, -40f, 40f, 120f, 193f })
            {
                CreateCube("SidewalkL_V_" + x, new Vector3(x - 9f, 0.12f, block), new Vector3(4f, 0.24f, Mathf.Abs(block)>190f?52f:66f), mats["Concrete"], parent, true);
                CreateCube("SidewalkR_V_" + x, new Vector3(x + 9f, 0.12f, block), new Vector3(4f, 0.24f, Mathf.Abs(block)>190f?52f:66f), mats["Concrete"], parent, true);
            }
            for (float z = -230f; z <= 230f; z += 12f)
                CreateCube("Dash", new Vector3(x, 0.10f, z), new Vector3(0.18f, 0.025f, 5f), mats["RoadMark"], parent, false);
        }
        foreach (float z in coords)
        {
            CreateCube("Road_H_" + z, new Vector3(0f, 0.025f, z), new Vector3(480f, 0.13f, 14f), mats["Asphalt"], parent, true);
            foreach (float block in new[] { -193f, -120f, -40f, 40f, 120f, 193f })
            {
                CreateCube("SidewalkL_H_" + z, new Vector3(block, 0.12f, z - 9f), new Vector3(Mathf.Abs(block)>190f?52f:66f, 0.24f, 4f), mats["Concrete"], parent, true);
                CreateCube("SidewalkR_H_" + z, new Vector3(block, 0.12f, z + 9f), new Vector3(Mathf.Abs(block)>190f?52f:66f, 0.24f, 4f), mats["Concrete"], parent, true);
            }
            for (float x = -230f; x <= 230f; x += 12f)
                CreateCube("Dash", new Vector3(x, 0.105f, z), new Vector3(5f, 0.025f, 0.18f), mats["RoadMark"], parent, false);
        }

        BuildRoadCurbs(parent, coords);

        foreach (float x in coords)
            foreach (float z in coords)
                BuildCrosswalk(new Vector3(x, 0.12f, z), parent);

        // Secondary alleys and parking access roads make the grid less empty.
        float[] alleys = { }; // No continuous alley strips through occupied building lots.
        foreach (float a in alleys)
        {
            CreateCube("AlleyV_" + a, new Vector3(a, 0.035f, 0f), new Vector3(6f, 0.10f, 460f), mats["Asphalt"], parent, true);
            CreateCube("AlleyH_" + a, new Vector3(0f, 0.04f, a), new Vector3(460f, 0.10f, 6f), mats["Asphalt"], parent, true);
        }
    }

    static void BuildRoadCurbs(Transform parent, float[] roadCoords)
    {
        // Each curb is split per city block, leaving wide openings at intersections.
        float[] blockCenters = { -193f, -120f, -40f, 40f, 120f, 193f };
        foreach (float road in roadCoords)
        {
            foreach (float center in blockCenters)
            {
                CreateCube("Curb_V_L", new Vector3(road - 7.15f, 0.22f, center), new Vector3(0.30f, 0.30f, Mathf.Abs(center)>190f?43f:57f), mats["ConcreteLight"], parent, true);
                CreateCube("Curb_V_R", new Vector3(road + 7.15f, 0.22f, center), new Vector3(0.30f, 0.30f, Mathf.Abs(center)>190f?43f:57f), mats["ConcreteLight"], parent, true);
                CreateCube("Curb_H_L", new Vector3(center, 0.22f, road - 7.15f), new Vector3(Mathf.Abs(center)>190f?43f:57f, 0.30f, 0.30f), mats["ConcreteLight"], parent, true);
                CreateCube("Curb_H_R", new Vector3(center, 0.22f, road + 7.15f), new Vector3(Mathf.Abs(center)>190f?43f:57f, 0.30f, 0.30f), mats["ConcreteLight"], parent, true);
            }
        }
    }

    static void BuildWorldBorder(Transform parent)
    {
        float edge = 246f;
        CreateCube("MapBorder_N", new Vector3(0f, 0.65f, edge), new Vector3(492f, 1.30f, 0.8f), mats["ConcreteDark"], parent, true);
        CreateCube("MapBorder_S", new Vector3(0f, 0.65f, -edge), new Vector3(492f, 1.30f, 0.8f), mats["ConcreteDark"], parent, true);
        CreateCube("MapBorder_E", new Vector3(edge, 0.65f, 0f), new Vector3(0.8f, 1.30f, 492f), mats["ConcreteDark"], parent, true);
        CreateCube("MapBorder_W", new Vector3(-edge, 0.65f, 0f), new Vector3(0.8f, 1.30f, 492f), mats["ConcreteDark"], parent, true);
    }

    static void BuildCrosswalk(Vector3 center, Transform parent)
    {
        for (int i = -3; i <= 3; i++)
        {
            CreateCube("Crosswalk", center + new Vector3(i * 1.35f, 0f, 8f), new Vector3(0.75f, 0.02f, 3.2f), mats["White"], parent, false);
            CreateCube("Crosswalk", center + new Vector3(8f, 0f, i * 1.35f), new Vector3(3.2f, 0.02f, 0.75f), mats["White"], parent, false);
        }
    }

    static void BuildCityBlocks(Transform buildings, Transform props)
    {
        BuildPark(new Vector3(40f, 0f, 40f), props);
        BuildGasStation(new Vector3(-40f, 0f, -40f), buildings, props);
        BuildParkingLot(new Vector3(40f, 0f, -40f), props);
        BuildPlaza(new Vector3(-40f, 0f, 40f), props);

        float[] blockCenters = { -193f, -120f, -40f, 40f, 120f, 193f };
        int id = 0;
        foreach (float bx in blockCenters)
        foreach (float bz in blockCenters)
        {
            // Keep the four central special-purpose blocks open.
            if (Mathf.Abs(bx) == 40f && Mathf.Abs(bz) == 40f) continue;
            if ((bz==193f && Mathf.Abs(bx)<=200f) || (bx==193f && bz==120f) || (bx==120f && bz==-40f) || (bx==-120f && bz==40f)) continue;

            float angle = Random.value > 0.5f ? 0f : 90f;
            float w1 = Mathf.Abs(bx)>190f?18f:Random.Range(18f, 30f);
            float d1 = Mathf.Abs(bz)>190f?18f:Random.Range(17f, 28f);
            float h1 = Random.Range(10f, (Mathf.Abs(bx) <= 120f && Mathf.Abs(bz) <= 120f) ? 38f : 25f);
            BuildDetailedBuilding("Building_" + id++, new Vector3(bx - 14f, 0f, bz + Random.Range(-5f, 5f)), w1, d1, h1, angle, buildings);

            float w2 = Mathf.Abs(bx)>190f?18f:Random.Range(16f, 27f);
            float d2 = Mathf.Abs(bz)>190f?18f:Random.Range(16f, 25f);
            float h2 = Random.Range(9f, (Mathf.Abs(bx) <= 120f && Mathf.Abs(bz) <= 120f) ? 32f : 22f);
            BuildDetailedBuilding("Building_" + id++, new Vector3(bx + 16f, 0f, bz + Random.Range(-7f, 7f)), w2, d2, h2, angle, buildings);

            if (Random.value < 0.35f && Mathf.Abs(bx)<190f && Mathf.Abs(bz)<190f)
            {
                // Small service building / shop adds variation between towers.
                BuildDetailedBuilding("Shop_" + id++, new Vector3(bx, 0f, bz - 22f), Random.Range(12f,18f), Random.Range(10f,15f), Random.Range(6f,10f), angle, buildings);
            }
        }
    }

    static void PlaceAssetStoreEnvironment(Transform parent)
    {
        if (AssetStoreAssetResolver.CountEnvironmentCandidates() == 0) return;
        Vector3[] spots = {
            new Vector3(-200f,0f,-200f), new Vector3(-120f,0f,-200f), new Vector3(-40f,0f,-200f), new Vector3(40f,0f,-200f), new Vector3(120f,0f,-200f), new Vector3(200f,0f,-200f),
            new Vector3(-200f,0f,200f), new Vector3(-120f,0f,200f), new Vector3(-40f,0f,200f), new Vector3(40f,0f,200f), new Vector3(120f,0f,200f), new Vector3(200f,0f,200f),
            new Vector3(-200f,0f,-120f), new Vector3(-200f,0f,120f), new Vector3(200f,0f,-120f), new Vector3(200f,0f,120f)
        };
        for (int i = 0; i < spots.Length; i++)
        {
            GameObject asset = AssetStoreAssetResolver.GetEnvironment(i);
            if (asset == null) break;
            GameObject wrapper = new GameObject("AssetStoreBuilding_" + i);
            wrapper.transform.SetParent(parent);
            wrapper.transform.position = spots[i];
            wrapper.transform.rotation = Quaternion.Euler(0f, (i % 4) * 90f, 0f);
            GameObject model = AssetStoreAssetResolver.InstantiateAsset(asset, wrapper.transform, "ImportedBuilding", false);
            if (model == null) { Object.DestroyImmediate(wrapper); continue; }
            Renderer[] rr = model.GetComponentsInChildren<Renderer>(true);
            if (rr.Length == 0) { Object.DestroyImmediate(wrapper); continue; }
            Bounds b = rr[0].bounds;
            for (int r = 1; r < rr.Length; r++) b.Encapsulate(rr[r].bounds);
            float footprint = Mathf.Max(b.size.x, b.size.z);
            if (footprint < 0.01f) { Object.DestroyImmediate(wrapper); continue; }
            float target = Mathf.Lerp(18f, 30f, (i % 5) / 4f);
            model.transform.localScale *= target / footprint;
            rr = model.GetComponentsInChildren<Renderer>(true);
            b = rr[0].bounds;
            for (int r = 1; r < rr.Length; r++) b.Encapsulate(rr[r].bounds);
            model.transform.position += Vector3.up * (wrapper.transform.position.y - b.min.y);
        }
    }

    static void BuildDistrictLandmarks(Transform buildings, Transform props)
    {
        Vector3[] commercial = { }; // Reserved lots are populated once; keep roads and bridge approaches clear.
        for(int i=0;i<commercial.Length;i++)
        {
            GameObject asset=AssetStoreAssetResolver.GetEnvironment(i+18); if(asset==null) continue;
            GameObject root=new GameObject("DistrictLandmark_"+i); root.transform.SetParent(buildings); root.transform.position=commercial[i]; root.transform.rotation=Quaternion.Euler(0f,(i%4)*90f,0f);
            GameObject model=AssetStoreAssetResolver.InstantiateAsset(asset,root.transform,"CommercialBuilding",false); if(model==null) continue;
            Renderer[] rr=model.GetComponentsInChildren<Renderer>(true); if(rr.Length==0) continue;
            Bounds b=rr[0].bounds;for(int r=1;r<rr.Length;r++)b.Encapsulate(rr[r].bounds);
            float footprint=Mathf.Max(b.size.x,b.size.z);if(footprint>0.01f)model.transform.localScale*=Mathf.Lerp(22f,34f,(i%4)/3f)/footprint;
            rr=model.GetComponentsInChildren<Renderer>(true);b=rr[0].bounds;for(int r=1;r<rr.Length;r++)b.Encapsulate(rr[r].bounds);model.transform.position+=Vector3.up*(root.transform.position.y-b.min.y);
            BoxCollider collider=root.AddComponent<BoxCollider>();collider.center=root.transform.InverseTransformPoint(b.center);collider.size=b.size;
        }
        CreateDistrictLabel("DOWNTOWN",new Vector3(120f,8f,82f),props);
        CreateDistrictLabel("OLD TOWN",new Vector3(-120f,7f,82f),props);
        CreateDistrictLabel("INDUSTRIAL",new Vector3(120f,7f,-82f),props);
        CreateDistrictLabel("WEST PARK",new Vector3(-120f,7f,-82f),props);
    }

    static void CreateDistrictLabel(string text, Vector3 position, Transform parent)
    {
        GameObject sign=new GameObject("District_"+text.Replace(" ","_"));sign.transform.SetParent(parent);sign.transform.position=position;sign.transform.rotation=Quaternion.Euler(0f,180f,0f);
        TextMesh tm=sign.AddComponent<TextMesh>();tm.text=text;tm.fontSize=64;tm.characterSize=0.22f;tm.anchor=TextAnchor.MiddleCenter;tm.color=new Color(1f,0.74f,0.18f);
    }

    static void BuildFootPolice(Transform parent, WantedSystem wanted)
    {
        Vector3[] positions={new Vector3(-8f,0.3f,8f),new Vector3(8f,0.3f,8f),new Vector3(112f,0.3f,90f),new Vector3(-112f,0.3f,90f),new Vector3(112f,0.3f,-90f),new Vector3(-112f,0.3f,-90f)};
        for(int i=0;i<positions.Length;i++)
        {
            GameObject anchor=new GameObject("PolicePatrolAnchor_"+i);anchor.transform.SetParent(parent);anchor.transform.position=positions[i];
            GameObject officer=new GameObject("PoliceOfficer_"+(i+1));officer.transform.SetParent(parent);officer.transform.position=positions[i];
            CharacterController cc=officer.AddComponent<CharacterController>();cc.height=1.8f;cc.radius=0.33f;cc.center=new Vector3(0f,0.9f,0f);cc.stepOffset=0.35f;
            HumanoidVisual fallback=BuildHumanoid(officer.transform,12+i,false);AnimatorMotionBridge bridge=BuildAssetStoreHumanoid(officer.transform,12+i,false);if(bridge!=null)fallback.gameObject.SetActive(false);
            PoliceOfficerAI ai=officer.AddComponent<PoliceOfficerAI>();ai.home=anchor.transform;ai.animatorBridge=bridge;
            NPCHealth hp=officer.AddComponent<NPCHealth>();hp.maxHealth=140f;
        }
    }

    static void BuildDetailedBuilding(string name, Vector3 pos, float width, float depth, float height, float yaw, Transform parent)
    {
        if (CityUpgradePipeline.PlaceBuilding(name, pos, width, depth, height, yaw, parent)) return;
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent);
        root.transform.position = pos;
        root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        string[] facadeNames = { "BuildingA", "BuildingB", "BuildingC", "BuildingD", "BuildingE", "BuildingF" };
        Material facade = mats[facadeNames[Random.Range(0, facadeNames.Length)]];
        CreateCube("Shell", new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth), facade, root.transform, true, true);
        CreateCube("RoofLip", new Vector3(0f, height + 0.35f, 0f), new Vector3(width + 0.8f, 0.7f, depth + 0.8f), mats["ConcreteDark"], root.transform, false, true);

        int floors = Mathf.Clamp(Mathf.RoundToInt(height / 3.4f), 3, 9);
        int colsFront = Mathf.Clamp(Mathf.FloorToInt(width / 4f), 3, 7);
        float sx = width / colsFront;
        for (int f = 0; f < floors; f++)
        {
            float y = 2.2f + f * ((height - 3f) / Mathf.Max(1, floors - 1));
            for (int c = 0; c < colsFront; c++)
            {
                float x = -width * 0.5f + sx * (c + 0.5f);
                Material win = Random.value < 0.23f ? mats["WindowWarm"] : mats["Glass"];
                CreateCube("Window", new Vector3(x, y, depth * 0.5f + 0.04f), new Vector3(Mathf.Min(2.15f, sx * 0.62f), 1.55f, 0.10f), win, root.transform, false);
                CreateCube("Window", new Vector3(x, y, -depth * 0.5f - 0.04f), new Vector3(Mathf.Min(2.15f, sx * 0.62f), 1.55f, 0.10f), win, root.transform, false);
            }
        }

        int colsSide = Mathf.Clamp(Mathf.FloorToInt(depth / 5f), 2, 5);
        float sz = depth / colsSide;
        for (int f = 0; f < floors; f += 2)
        {
            float y = 2.2f + f * ((height - 3f) / Mathf.Max(1, floors - 1));
            for (int c = 0; c < colsSide; c++)
            {
                float z = -depth * 0.5f + sz * (c + 0.5f);
                CreateCube("SideWindow", new Vector3(width * 0.5f + 0.04f, y, z), new Vector3(0.10f, 1.45f, Mathf.Min(2.1f, sz * 0.58f)), mats["Glass"], root.transform, false);
                CreateCube("SideWindow", new Vector3(-width * 0.5f - 0.04f, y, z), new Vector3(0.10f, 1.45f, Mathf.Min(2.1f, sz * 0.58f)), mats["Glass"], root.transform, false);
            }
        }

        CreateCube("DoorFrame", new Vector3(0f, 1.5f, depth * 0.5f + 0.15f), new Vector3(2.7f, 3f, 0.35f), mats["Metal"], root.transform, false);
        CreateCube("DoorGlass", new Vector3(0f, 1.45f, depth * 0.5f + 0.36f), new Vector3(1.9f, 2.45f, 0.10f), mats["Glass"], root.transform, false);
        CreateCube("Awning", new Vector3(0f, 3.25f, depth * 0.5f + 1.0f), new Vector3(5.2f, 0.20f, 2f), mats[Random.value > 0.5f ? "Red" : "Blue"], root.transform, false);

        // Extra facade depth so blocks read less like plain boxes.
        CreateCube("CornerTrimL", new Vector3(-width * 0.5f - 0.08f, height * 0.5f, depth * 0.5f + 0.08f), new Vector3(0.32f, height, 0.32f), mats["ConcreteLight"], root.transform, false);
        CreateCube("CornerTrimR", new Vector3(width * 0.5f + 0.08f, height * 0.5f, depth * 0.5f + 0.08f), new Vector3(0.32f, height, 0.32f), mats["ConcreteLight"], root.transform, false);

        if (height > 14f && Random.value < 0.65f)
        {
            int balconyCount = Mathf.Min(3, Mathf.Max(1, floors / 3));
            for (int b = 0; b < balconyCount; b++)
            {
                float by = 5.0f + b * 5.2f;
                if (by > height - 1.5f) break;
                float bx = (b % 2 == 0 ? -1f : 1f) * width * 0.24f;
                CreateCube("BalconySlab", new Vector3(bx, by, depth * 0.5f + 0.75f), new Vector3(Mathf.Min(5.8f, width * 0.32f), 0.16f, 1.5f), mats["ConcreteDark"], root.transform, false);
                CreateCube("BalconyRail", new Vector3(bx, by + 0.55f, depth * 0.5f + 1.43f), new Vector3(Mathf.Min(5.6f, width * 0.30f), 0.72f, 0.08f), mats["Metal"], root.transform, false);
            }
        }

        if (height > 18f)
        {
            CreateCube("RooftopUnit", new Vector3(-width * 0.22f, height + 1.2f, 0f), new Vector3(3f, 1.7f, 2.3f), mats["Metal"], root.transform, false);
            CreateCube("RooftopUnit", new Vector3(width * 0.18f, height + 1.0f, depth * 0.15f), new Vector3(2.2f, 1.2f, 2f), mats["Metal"], root.transform, false);
        }
    }

    static void BuildPark(Vector3 center, Transform parent)
    {
        GameObject root = new GameObject("Central Park"); root.transform.SetParent(parent); root.transform.position = center;
        CreateCube("Lawn", new Vector3(0f, 0.08f, 0f), new Vector3(58f, 0.16f, 58f), mats["GrassBright"], root.transform, true);
        CreateCube("PathA", new Vector3(0f, 0.18f, 0f), new Vector3(5f, 0.10f, 58f), mats["ConcreteLight"], root.transform, false);
        CreateCube("PathB", new Vector3(0f, 0.18f, 0f), new Vector3(58f, 0.10f, 5f), mats["ConcreteLight"], root.transform, false);
        for (int i = 0; i < 12; i++)
        {
            float x = Random.Range(-25f, 25f); float z = Random.Range(-25f, 25f);
            if (Mathf.Abs(x) < 4f || Mathf.Abs(z) < 4f) { i--; continue; }
            BuildTree(new Vector3(x, 0.2f, z), root.transform, Random.Range(0.8f, 1.25f));
        }
        BuildBench(new Vector3(-7f,0.3f,8f), 90f, root.transform);
        BuildBench(new Vector3(7f,0.3f,-8f), -90f, root.transform);
        BuildFountain(new Vector3(0f,0.2f,0f), root.transform);
    }

    static void BuildPlaza(Vector3 center, Transform parent)
    {
        GameObject root = new GameObject("Plaza"); root.transform.SetParent(parent); root.transform.position = center;
        CreateCube("PlazaFloor", new Vector3(0f,0.09f,0f), new Vector3(58f,0.18f,58f), mats["ConcreteLight"], root.transform, true);
        CreateCylinder("SculptureBase", new Vector3(0f,0.55f,0f), new Vector3(4.5f,0.55f,4.5f), mats["ConcreteDark"], root.transform, false);
        CreateCylinder("Sculpture", new Vector3(0f,4.3f,0f), new Vector3(0.9f,3.7f,0.9f), mats["Silver"], root.transform, false);
        BuildBench(new Vector3(-9f,0.3f,7f), 35f, root.transform);
        BuildBench(new Vector3(9f,0.3f,-7f), 215f, root.transform);
        for (int i = 0; i < 5; i++) BuildTree(new Vector3(i < 2 ? -22f : 22f, 0.2f, (i % 2 == 0 ? -20f : 20f)), root.transform, 0.85f);
    }

    static void BuildGasStation(Vector3 center, Transform buildings, Transform props)
    {
        GameObject root = new GameObject("Gas Station"); root.transform.SetParent(props); root.transform.position = center;
        CreateCube("Lot", new Vector3(0f,0.07f,0f), new Vector3(58f,0.14f,58f), mats["Asphalt"], root.transform, true);
        GameObject store = new GameObject("Store"); store.transform.SetParent(root.transform);
        CreateCube("StoreShell", new Vector3(-12f,3.5f,15f), new Vector3(24f,7f,15f), mats["BuildingC"], store.transform, true);
        CreateCube("StoreGlass", new Vector3(-12f,3.1f,7.4f), new Vector3(15f,4.2f,0.12f), mats["Glass"], store.transform, false);
        CreateCube("Canopy", new Vector3(10f,5f,-3f), new Vector3(30f,0.65f,20f), mats["White"], root.transform, false);
        for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
                CreateCylinder("CanopyPole", new Vector3(10f + x*12f,2.5f,-3f + z*7f), new Vector3(0.38f,2.5f,0.38f), mats["Metal"], root.transform, false);
        for (int i = -1; i <= 1; i++)
        {
            CreateCube("Pump", new Vector3(10f + i*7f,1.15f,-3f), new Vector3(1.3f,2.3f,1.1f), mats["Red"], root.transform, false);
            CreateCube("PumpScreen", new Vector3(10f + i*7f,1.45f,-3.58f), new Vector3(0.65f,0.48f,0.05f), mats["Glass"], root.transform, false);
        }
    }

    static void BuildParkingLot(Vector3 center, Transform parent)
    {
        GameObject root = new GameObject("Parking Lot"); root.transform.SetParent(parent); root.transform.position = center;
        CreateCube("ParkingAsphalt", new Vector3(0f,0.07f,0f), new Vector3(58f,0.14f,58f), mats["Asphalt"], root.transform, true);
        for (int r = -2; r <= 2; r++)
        {
            CreateCube("ParkingLine", new Vector3(r*9f,0.15f,13f), new Vector3(0.12f,0.02f,18f), mats["White"], root.transform, false);
            CreateCube("ParkingLine", new Vector3(r*9f,0.15f,-13f), new Vector3(0.12f,0.02f,18f), mats["White"], root.transform, false);
        }
    }

    static List<Light> BuildStreetFurniture(Transform parent)
    {
        List<Light> lights = new List<Light>();
        float[] roadCoords = { -80f, 0f, 80f };
        foreach (float x in roadCoords)
        {
            for (float z = -145f; z <= 145f; z += 29f)
            {
                lights.Add(BuildStreetLight(new Vector3(x - 11.2f, 0.2f, z), 0f, parent));
                if (((int)z) % 58 == 0) BuildTrashCan(new Vector3(x + 11f,0.25f,z+5f), parent);
            }
        }
        foreach (float z in roadCoords)
        {
            for (float x = -145f; x <= 145f; x += 29f)
            {
                lights.Add(BuildStreetLight(new Vector3(x, 0.2f, z + 11.2f), 90f, parent));
                if (((int)x) % 58 == 0) BuildBench(new Vector3(x+5f,0.3f,z-11f), 0f, parent);
            }
        }
        return lights;
    }

    static Light BuildStreetLight(Vector3 pos, float yaw, Transform parent)
    {
        GameObject root = new GameObject("StreetLight"); root.transform.SetParent(parent); root.transform.position = pos; root.transform.rotation = Quaternion.Euler(0f,yaw,0f);
        CreateCylinder("Pole", new Vector3(0f,3f,0f), new Vector3(0.15f,3f,0.15f), mats["Metal"], root.transform, false);
        CreateCube("Arm", new Vector3(0.7f,5.75f,0f), new Vector3(1.4f,0.12f,0.12f), mats["Metal"], root.transform, false);
        GameObject lamp = CreateCube("Lamp", new Vector3(1.35f,5.62f,0f), new Vector3(0.55f,0.22f,0.35f), mats["White"], root.transform, false);
        Light l = lamp.AddComponent<Light>(); l.type = LightType.Point; l.range = 13f; l.intensity = 1.8f; l.color = new Color(1f,0.82f,0.58f); l.enabled = false; l.shadows = LightShadows.None;
        return l;
    }

    static void BuildTree(Vector3 pos, Transform parent, float scale)
    {
        GameObject root = new GameObject("Tree"); root.transform.SetParent(parent); root.transform.localPosition = pos;
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/Kenney/CitySuburban/Models/FBX format/tree-large.fbx");
        if(asset!=null)
        {
            var imported=AssetStoreAssetResolver.InstantiateAsset(asset,root.transform,"ImportedTree");
            AssetStoreAssetResolver.NormalizeHeight(imported,5.5f*scale);return;
        }
        CreateCylinder("Trunk", new Vector3(0f,1.6f,0f), new Vector3(0.35f*scale,1.6f*scale,0.35f*scale), mats["Trunk"], root.transform, false);
        CreateSphere("Crown", new Vector3(0f,4.0f*scale,0f), new Vector3(2.5f*scale,2.2f*scale,2.5f*scale), mats["Leaves"], root.transform, false);
        CreateSphere("Crown2", new Vector3(1.1f*scale,3.5f*scale,0f), new Vector3(1.5f*scale,1.45f*scale,1.5f*scale), mats["Leaves"], root.transform, false);
    }

    static void BuildBench(Vector3 pos, float yaw, Transform parent)
    {
        GameObject root = new GameObject("Bench"); root.transform.SetParent(parent); root.transform.localPosition = pos; root.transform.localRotation = Quaternion.Euler(0f,yaw,0f);
        CreateCube("Seat", new Vector3(0f,0.55f,0f), new Vector3(2.8f,0.18f,0.65f), mats["Wood"], root.transform, false);
        CreateCube("Back", new Vector3(0f,1.05f,0.28f), new Vector3(2.8f,0.75f,0.15f), mats["Wood"], root.transform, false);
        CreateCube("LegL", new Vector3(-1.05f,0.28f,0f), new Vector3(0.16f,0.55f,0.55f), mats["Metal"], root.transform, false);
        CreateCube("LegR", new Vector3(1.05f,0.28f,0f), new Vector3(0.16f,0.55f,0.55f), mats["Metal"], root.transform, false);
    }

    static void BuildTrashCan(Vector3 pos, Transform parent)
    {
        GameObject root = new GameObject("TrashCan"); root.transform.SetParent(parent); root.transform.localPosition = pos;
        CreateCylinder("Bin", new Vector3(0f,0.55f,0f), new Vector3(0.45f,0.55f,0.45f), mats["Metal"], root.transform, false);
    }

    static void BuildFountain(Vector3 pos, Transform parent)
    {
        GameObject root = new GameObject("Fountain"); root.transform.SetParent(parent); root.transform.localPosition = pos;
        CreateCylinder("Basin", new Vector3(0f,0.35f,0f), new Vector3(4.3f,0.35f,4.3f), mats["ConcreteDark"], root.transform, false);
        CreateCylinder("Water", new Vector3(0f,0.72f,0f), new Vector3(3.7f,0.03f,3.7f), mats["Water"], root.transform, false);
        CreateCylinder("Center", new Vector3(0f,1.5f,0f), new Vector3(0.45f,0.9f,0.45f), mats["ConcreteLight"], root.transform, false);
    }

    static GarageZone BuildGarageInterior(Vector3 center, Transform buildings, Transform props)
    {
        GameObject root = new GameObject("Open Garage");
        root.transform.SetParent(buildings);
        root.transform.position = center;

        CreateCube("GarageLot", new Vector3(0f,0.07f,0f), new Vector3(58f,0.14f,58f), mats["Asphalt"], root.transform, true);
        CreateCube("GarageFloor", new Vector3(0f,0.18f,13f), new Vector3(40f,0.22f,25f), mats["ConcreteDark"], root.transform, true);
        CreateCube("GarageBack", new Vector3(0f,4.5f,25f), new Vector3(40f,9f,0.7f), mats["BuildingB"], root.transform, true);
        CreateCube("GarageLeft", new Vector3(-19.65f,4.5f,13f), new Vector3(0.7f,9f,24f), mats["BuildingB"], root.transform, true);
        CreateCube("GarageRight", new Vector3(19.65f,4.5f,13f), new Vector3(0.7f,9f,24f), mats["BuildingB"], root.transform, true);
        CreateCube("GarageRoof", new Vector3(0f,9.0f,13f), new Vector3(40f,0.7f,25f), mats["Metal"], root.transform, true);
        CreateCube("FrontPillarL", new Vector3(-17.5f,4.2f,0.8f), new Vector3(4.5f,8.4f,1f), mats["BuildingB"], root.transform, true);
        CreateCube("FrontPillarR", new Vector3(17.5f,4.2f,0.8f), new Vector3(4.5f,8.4f,1f), mats["BuildingB"], root.transform, true);
        CreateCube("Office", new Vector3(-12f,2.2f,19f), new Vector3(11f,4.4f,8f), mats["BuildingC"], root.transform, true);
        CreateCube("OfficeGlass", new Vector3(-12f,2.2f,14.95f), new Vector3(7.5f,2.8f,0.12f), mats["Glass"], root.transform, false);

        for (int i = -2; i <= 2; i++)
            CreateCube("ServiceStripe", new Vector3(i * 7f,0.31f,8f), new Vector3(0.16f,0.02f,13f), mats["Yellow"], root.transform, false);

        GameObject marker = new GameObject("GarageServiceZone");
        marker.transform.SetParent(root.transform);
        marker.transform.localPosition = new Vector3(0f,0.3f,8f);
        CreateCylinder("ServiceRing", new Vector3(0f,0.05f,0f), new Vector3(5.2f,0.035f,5.2f), mats["GreenOn"], marker.transform, false);
        GarageZone zone = marker.AddComponent<GarageZone>();
        zone.radius = 7f;
        zone.servicePrice = 75;
        GameObject servicePoint = new GameObject("ServicePoint");
        servicePoint.transform.SetParent(marker.transform);
        servicePoint.transform.localPosition = new Vector3(0f,0f,4.5f);
        servicePoint.transform.localRotation = Quaternion.Euler(0f,180f,0f);
        zone.servicePoint = servicePoint.transform;
        return zone;
    }

    static void BuildCafeInterior(Vector3 center, Transform buildings, Transform props)
    {
        GameObject root = new GameObject("Walk-In Cafe");
        root.transform.SetParent(buildings);
        root.transform.position = center;
        CreateCube("CafePatio", new Vector3(0f,0.08f,0f), new Vector3(58f,0.16f,58f), mats["ConcreteLight"], root.transform, true);
        CreateCube("CafeFloor", new Vector3(0f,0.18f,12f), new Vector3(34f,0.25f,26f), mats["Wood"], root.transform, true);
        CreateCube("BackWall", new Vector3(0f,4.2f,24.5f), new Vector3(34f,8.4f,0.6f), mats["BuildingD"], root.transform, true);
        CreateCube("LeftWall", new Vector3(-16.7f,4.2f,12f), new Vector3(0.6f,8.4f,25f), mats["BuildingD"], root.transform, true);
        CreateCube("RightWall", new Vector3(16.7f,4.2f,12f), new Vector3(0.6f,8.4f,25f), mats["BuildingD"], root.transform, true);
        CreateCube("Roof", new Vector3(0f,8.4f,12f), new Vector3(34f,0.5f,26f), mats["ConcreteDark"], root.transform, true);
        CreateCube("Counter", new Vector3(0f,1.0f,20f), new Vector3(15f,2f,2.2f), mats["Wood"], root.transform, true);
        CreateCube("CounterGlass", new Vector3(0f,2.0f,18.85f), new Vector3(9f,1.25f,0.08f), mats["Glass"], root.transform, false);
        for (int i = -2; i <= 2; i++)
        {
            float x = i * 5.2f;
            CreateCylinder("Table", new Vector3(x,0.85f,8f + (i%2)*5f), new Vector3(1.25f,0.12f,1.25f), mats["Wood"], root.transform, true);
            CreateCylinder("TableLeg", new Vector3(x,0.43f,8f + (i%2)*5f), new Vector3(0.16f,0.43f,0.16f), mats["Metal"], root.transform, true);
        }
        BuildTree(new Vector3(-22f,0.2f,-15f), root.transform, 0.9f);
        BuildTree(new Vector3(22f,0.2f,-15f), root.transform, 0.9f);
        BuildBench(new Vector3(-10f,0.3f,-10f), 0f, root.transform);
        BuildBench(new Vector3(10f,0.3f,-10f), 180f, root.transform);
    }

    static TrafficLightController BuildCentralTrafficLights(Transform parent)
    {
        GameObject system = new GameObject("Central Traffic Lights"); system.transform.SetParent(parent);
        TrafficLightController ctl = system.AddComponent<TrafficLightController>();
        List<MeshRenderer> nsR = new List<MeshRenderer>(), nsG = new List<MeshRenderer>(), ewR = new List<MeshRenderer>(), ewG = new List<MeshRenderer>();

        BuildTrafficPole(new Vector3(-9f,0.2f,-9f), 0f, true, system.transform, nsR, nsG, ewR, ewG);
        BuildTrafficPole(new Vector3(9f,0.2f,9f), 180f, true, system.transform, nsR, nsG, ewR, ewG);
        BuildTrafficPole(new Vector3(-9f,0.2f,9f), 90f, false, system.transform, nsR, nsG, ewR, ewG);
        BuildTrafficPole(new Vector3(9f,0.2f,-9f), -90f, false, system.transform, nsR, nsG, ewR, ewG);

        ctl.northSouthRed = nsR.ToArray(); ctl.northSouthGreen = nsG.ToArray(); ctl.eastWestRed = ewR.ToArray(); ctl.eastWestGreen = ewG.ToArray();
        ctl.redOn = mats["RedOn"]; ctl.redOff = mats["RedOff"]; ctl.greenOn = mats["GreenOn"]; ctl.greenOff = mats["GreenOff"];

        CreateStopZone("Stop_NS_S", new Vector3(3.5f,1f,-12f), new Vector3(6.5f,2f,0.6f), true, ctl, system.transform);
        CreateStopZone("Stop_NS_N", new Vector3(-3.5f,1f,12f), new Vector3(6.5f,2f,0.6f), true, ctl, system.transform);
        CreateStopZone("Stop_EW_W", new Vector3(-12f,1f,-3.5f), new Vector3(0.6f,2f,6.5f), false, ctl, system.transform);
        CreateStopZone("Stop_EW_E", new Vector3(12f,1f,3.5f), new Vector3(0.6f,2f,6.5f), false, ctl, system.transform);
        return ctl;
    }

    static void BuildTrafficPole(Vector3 pos, float yaw, bool northSouth, Transform parent, List<MeshRenderer> nsR, List<MeshRenderer> nsG, List<MeshRenderer> ewR, List<MeshRenderer> ewG)
    {
        GameObject root = new GameObject("TrafficLightPole"); root.transform.SetParent(parent); root.transform.position = pos; root.transform.rotation = Quaternion.Euler(0f,yaw,0f);
        CreateCylinder("Pole", new Vector3(0f,2.6f,0f), new Vector3(0.16f,2.6f,0.16f), mats["Metal"], root.transform, false);
        CreateCube("SignalBox", new Vector3(0f,4.6f,0.25f), new Vector3(0.75f,1.55f,0.5f), mats["Black"], root.transform, false);
        MeshRenderer r = CreateSphere("RedLamp", new Vector3(0f,5.0f,0.53f), new Vector3(0.24f,0.24f,0.14f), mats["RedOn"], root.transform, false).GetComponent<MeshRenderer>();
        MeshRenderer g = CreateSphere("GreenLamp", new Vector3(0f,4.2f,0.53f), new Vector3(0.24f,0.24f,0.14f), mats["GreenOff"], root.transform, false).GetComponent<MeshRenderer>();
        if (northSouth) { nsR.Add(r); nsG.Add(g); } else { ewR.Add(r); ewG.Add(g); }
    }

    static void CreateStopZone(string name, Vector3 pos, Vector3 scale, bool ns, TrafficLightController ctl, Transform parent)
    {
        GameObject g = CreateCube(name, pos, scale, mats["Invisible"], parent, true);
        g.GetComponent<MeshRenderer>().enabled = false;
        BoxCollider bc = g.GetComponent<BoxCollider>(); bc.isTrigger = true;
        TrafficStopZone zone = g.AddComponent<TrafficStopZone>(); zone.controller = ctl; zone.northSouth = ns;
    }

    static Transform[] BuildRoute(string name, Vector3[] points, Transform parent)
    {
        GameObject root = new GameObject(name); root.transform.SetParent(parent);
        List<Transform> result = new List<Transform>();
        foreach (Vector3 p in points)
        {
            GameObject w = new GameObject("WP_" + result.Count); w.transform.SetParent(root.transform); w.transform.position = p; result.Add(w.transform);
        }
        return result.ToArray();
    }

    static void SpawnTraffic(Transform parent, params Transform[][] routes)
    {
        string[] colors = { "Red", "Blue", "Yellow", "Green", "Orange", "Silver", "White" };
        int id = 0;
        foreach (Transform[] route in routes)
        {
            for (int i = 0; i < 5; i++)
            {
                int wp = Mathf.Min(i, route.Length - 1);
                Vector3 p = Vector3.Lerp(route[wp].position, route[(wp + 1) % route.Length].position, 0.28f);
                int style = id % 7;
                GameObject car = new GameObject("TrafficCar_" + id++);
                car.transform.SetParent(parent);
                car.transform.position = p;
                int nextWp = (wp + 1) % route.Length;
                Vector3 startDir = route[nextWp].position - route[wp].position;
                startDir.y = 0f;
                if (startDir.sqrMagnitude > 0.01f) car.transform.rotation = Quaternion.LookRotation(startDir.normalized, Vector3.up);

                Transform[] wheels = BuildCarVisual(car.transform, mats[colors[Random.Range(0, colors.Length)]], style);
                BoxCollider bc = car.AddComponent<BoxCollider>();
                bc.size = new Vector3(style == 2 ? 2.15f : 1.95f, style == 2 ? 1.45f : 1.25f, style == 3 ? 4.75f : 4.25f);
                bc.center = new Vector3(0f,0.68f,0f);

                Rigidbody rb = car.AddComponent<Rigidbody>();
                rb.mass = style == 2 ? 1450f : 1180f;
                rb.linearDamping = 0.12f;
                rb.angularDamping = 2.7f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.isKinematic = true;

                TrafficCarAI ai = car.AddComponent<TrafficCarAI>();
                ai.waypoints = route;
                ai.wheels = wheels;
                ai.speed = Random.Range(5.8f,8.2f);

                DriveableVehicle dv = car.AddComponent<DriveableVehicle>();
                dv.wheels = wheels;
                dv.owned = true;
                dv.purchasePrice = 0;
                dv.acceleration = style == 3 ? 27f : style == 2 ? 18f : 22f;
                dv.maxSpeed = style == 3 ? 30f : style == 2 ? 23f : 26f;
                dv.steering = style == 2 ? 45f : 53f;
                dv.npcDriverVisual = BuildNPCDriverVisual(car.transform, id);
                dv.npcExitPedestrianTemplate = BuildNPCExitTemplate(car.transform, id + 300);
                AddVehicleSystems(car, dv);

                GameObject target = new GameObject("CameraTarget");
                target.transform.SetParent(car.transform);
                target.transform.localPosition = new Vector3(0f,1.1f,0f);
                dv.cameraTarget = target.transform;
            }
        }
    }

    static void BuildParkedCars(Transform parent)
    {
        string[] colors = { "Red", "Blue", "Yellow", "Green", "Orange", "Silver", "White" };
        Vector3[] spots = {
            new Vector3(28f,0.03f,-28f), new Vector3(38f,0.03f,-28f), new Vector3(48f,0.03f,-28f),
            new Vector3(28f,0.03f,-52f), new Vector3(38f,0.03f,-52f), new Vector3(48f,0.03f,-52f),
            new Vector3(-120f,0.03f,11.5f), new Vector3(-95f,0.03f,11.5f), new Vector3(112f,0.03f,-11.5f),
            new Vector3(11.5f,0.03f,112f), new Vector3(-11.5f,0.03f,-118f),
            new Vector3(92f,0.03f,11.5f), new Vector3(132f,0.03f,11.5f),
            new Vector3(-92f,0.03f,-11.5f), new Vector3(-132f,0.03f,-11.5f),
            new Vector3(11.5f,0.03f,-92f)
        };
        for (int i = 0; i < spots.Length; i++)
        {
            int style = Random.Range(0,4);
            GameObject car = new GameObject("ParkedCar_" + i);
            car.transform.SetParent(parent);
            car.transform.position = spots[i];
            car.transform.rotation = Quaternion.Euler(0f, i < 6 ? 0f : (i%2==0?90f:0f),0f);

            Transform[] wheels = BuildCarVisual(car.transform, mats[colors[Random.Range(0, colors.Length)]], style);
            BoxCollider bc = car.AddComponent<BoxCollider>();
            bc.size = new Vector3(style == 2 ? 2.15f : 1.95f, style == 2 ? 1.45f : 1.25f, style == 3 ? 4.75f : 4.25f);
            bc.center = new Vector3(0f,0.68f,0f);

            Rigidbody rb = car.AddComponent<Rigidbody>();
            rb.mass = style == 2 ? 1450f : 1180f;
            rb.linearDamping = 0.15f;
            rb.angularDamping = 2.8f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            DriveableVehicle dv = car.AddComponent<DriveableVehicle>();
            dv.wheels = wheels;
            dv.owned = true;
            dv.purchasePrice = 0;
            dv.acceleration = style == 3 ? 27f : style == 2 ? 18f : 22f;
            dv.maxSpeed = style == 3 ? 30f : style == 2 ? 23f : 26f;
            dv.steering = style == 2 ? 45f : 53f;
            AddVehicleSystems(car, dv);

            GameObject target = new GameObject("CameraTarget");
            target.transform.SetParent(car.transform);
            target.transform.localPosition = new Vector3(0f,1.1f,0f);
            dv.cameraTarget = target.transform;
        }
    }

    static void SpawnNPCs(Transform parent)
    {
        List<Transform[]> loops = new List<Transform[]>();
        loops.Add(BuildPedLoop("PedLoop_NE", new Vector3(9f,0.3f,9f), new Vector3(71f,0.3f,71f), parent));
        loops.Add(BuildPedLoop("PedLoop_NW", new Vector3(-71f,0.3f,9f), new Vector3(-9f,0.3f,71f), parent));
        loops.Add(BuildPedLoop("PedLoop_SE", new Vector3(9f,0.3f,-71f), new Vector3(71f,0.3f,-9f), parent));
        loops.Add(BuildPedLoop("PedLoop_SW", new Vector3(-71f,0.3f,-71f), new Vector3(-9f,0.3f,-9f), parent));
        loops.Add(BuildPedLoop("PedLoop_N", new Vector3(-71f,0.3f,89f), new Vector3(-9f,0.3f,151f), parent));
        loops.Add(BuildPedLoop("PedLoop_S", new Vector3(-71f,0.3f,-151f), new Vector3(-9f,0.3f,-89f), parent));
        loops.Add(BuildPedLoop("PedLoop_OuterNE", new Vector3(89f,0.3f,89f), new Vector3(151f,0.3f,151f), parent));
        loops.Add(BuildPedLoop("PedLoop_OuterNW", new Vector3(-151f,0.3f,89f), new Vector3(-89f,0.3f,151f), parent));
        loops.Add(BuildPedLoop("PedLoop_OuterSE", new Vector3(89f,0.3f,-151f), new Vector3(151f,0.3f,-89f), parent));
        loops.Add(BuildPedLoop("PedLoop_OuterSW", new Vector3(-151f,0.3f,-151f), new Vector3(-89f,0.3f,-89f), parent));

        for (int i = 0; i < 64; i++)
        {
            Transform[] loop = loops[i % loops.Count];
            int segment=i % loop.Length;
            Vector3 pos = Vector3.Lerp(loop[segment].position,loop[(segment+1)%loop.Length].position,0.12f + (i/loop.Length%5)*0.16f);
            GameObject npc = new GameObject("NPC_" + (i+1)); npc.transform.SetParent(parent); npc.transform.position = pos;
            CharacterController cc = npc.AddComponent<CharacterController>(); cc.height = 1.8f; cc.radius = 0.32f; cc.center = new Vector3(0f,0.9f,0f); cc.stepOffset = 0.35f;
            HumanoidVisual fallback = BuildHumanoid(npc.transform, i + 1, false);
            AnimatorMotionBridge bridge = BuildAssetStoreHumanoid(npc.transform, i + 1, false);
            HumanoidVisual visual = fallback;
            if (bridge != null) { fallback.gameObject.SetActive(false); visual = null; }
            NPCWalker walker = npc.AddComponent<NPCWalker>();
            walker.waypoints = loop;
            walker.visual = visual;
            walker.animatorBridge = bridge;
            walker.speed = Random.Range(1.2f,1.9f);
            npc.AddComponent<NPCHealth>();
        }
    }

    static Transform[] BuildPedLoop(string name, Vector3 a, Vector3 b, Transform parent)
    {
        GameObject root = new GameObject(name); root.transform.SetParent(parent);
        Vector3[] pts = { new Vector3(a.x,0.3f,a.z), new Vector3(b.x,0.3f,a.z), new Vector3(b.x,0.3f,b.z), new Vector3(a.x,0.3f,b.z) };
        Transform[] t = new Transform[4];
        for (int i = 0; i < 4; i++) { GameObject w = new GameObject("P"+i); w.transform.SetParent(root.transform); w.transform.position = pts[i]; t[i]=w.transform; }
        return t;
    }

    static GameObject BuildPlayer(Vector3 pos)
    {
        GameObject player = new GameObject("Player"); player.transform.position = pos;
        CharacterController cc = player.AddComponent<CharacterController>(); cc.height=1.8f; cc.radius=0.34f; cc.center=new Vector3(0f,0.9f,0f); cc.stepOffset=0.35f; cc.slopeLimit=50f;
        HumanoidVisual fallback = BuildHumanoid(player.transform, 0, true);
        AnimatorMotionBridge bridge = BuildAssetStoreHumanoid(player.transform, 0, true);
        HumanoidVisual visual = fallback;
        if (bridge != null) { fallback.gameObject.SetActive(false); visual = null; }
        PlayerController pc = player.AddComponent<PlayerController>();
        pc.visual = visual;
        pc.animatorBridge = bridge;
        VehicleInteractor vi = player.AddComponent<VehicleInteractor>();
        vi.playerController=pc; vi.characterController=cc; vi.visual=visual;
        vi.animatorVisualRoot = bridge != null ? bridge.gameObject : null;
        return player;
    }

    static AnimatorMotionBridge BuildAssetStoreHumanoid(Transform parent, int variant, bool player)
    {
        GameObject asset = player ? AssetStoreAssetResolver.GetCharacter(0) : CityUpgradePipeline.Civilian(variant);
        if (asset == null) return null;
        GameObject wrapper = new GameObject(player ? "PlayerModel_AssetStore" : "NPCModel_AssetStore");
        wrapper.transform.SetParent(parent);
        wrapper.transform.localPosition = Vector3.zero;
        wrapper.transform.localRotation = Quaternion.identity;
        wrapper.transform.localScale = Vector3.one;
        GameObject model = AssetStoreAssetResolver.InstantiateAsset(asset, wrapper.transform, "AssetStoreCharacter");
        if (model == null || !AssetStoreAssetResolver.NormalizeHeight(model, 1.80f))
        {
            Object.DestroyImmediate(wrapper);
            return null;
        }
        Animator animator = model.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            // Kenney's generic FBX characters contain animation clips but Unity does not
            // always add an Animator component to the imported model root automatically.
            animator = model.AddComponent<Animator>();
        }
        RuntimeAnimatorController controller = AssetStoreAssetResolver.GetCharacterController(asset);
        if (controller != null) animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        foreach (SkinnedMeshRenderer skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            skin.updateWhenOffscreen = false;
        AnimatorMotionBridge bridge = wrapper.AddComponent<AnimatorMotionBridge>();
        bridge.animator = animator;
        return bridge;
    }

    static GameObject[] BuildWeaponVisuals(Transform player)
    {
        GameObject mount = new GameObject("WeaponMount");
        mount.transform.SetParent(player);
        mount.transform.localPosition = new Vector3(0.38f, 1.16f, 0.30f);
        mount.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        GameObject[] result = new GameObject[3];
        int[] variants = { 0, 7, 14 };
        for (int i = 0; i < result.Length; i++)
        {
            GameObject asset = AssetStoreAssetResolver.GetWeapon(variants[i]);
            if (asset == null) continue;
            GameObject model = AssetStoreAssetResolver.InstantiateAsset(asset, mount.transform, "Weapon_" + (i + 1));
            if (model == null) continue;
            AssetStoreAssetResolver.NormalizeLength(model, i == 0 ? 0.55f : 0.82f, 0f);
            model.transform.localPosition += new Vector3(0f, 0f, i == 0 ? 0.10f : 0.22f);
            result[i] = model;
            model.SetActive(i == 0);
        }
        return result;
    }

    static HumanoidVisual BuildHumanoid(Transform parent, int variant, bool player)
    {
        GameObject root = new GameObject(player ? "PlayerModel" : "NPCModel"); root.transform.SetParent(parent); root.transform.localPosition = Vector3.zero;
        Material skin = mats[variant % 3 == 0 ? "SkinA" : variant % 3 == 1 ? "SkinB" : "SkinC"];
        Material shirt = mats[player ? "ShirtA" : (variant % 4 == 0 ? "ShirtA" : variant % 4 == 1 ? "ShirtB" : variant % 4 == 2 ? "ShirtC" : "ShirtD")];
        Material hair = mats[variant % 3 == 0 ? "HairA" : variant % 3 == 1 ? "HairB" : "HairC"];

        GameObject torsoPivot = new GameObject("TorsoPivot"); torsoPivot.transform.SetParent(root.transform); torsoPivot.transform.localPosition = new Vector3(0f,1.02f,0f);
        CreateCube("Torso", new Vector3(0f,0.39f,0f), new Vector3(0.62f,0.78f,0.30f), shirt, torsoPivot.transform, false);
        CreateCube("Waist", new Vector3(0f,-0.05f,0f), new Vector3(0.52f,0.22f,0.28f), mats["Pants"], torsoPivot.transform, false);

        GameObject headPivot = new GameObject("HeadPivot"); headPivot.transform.SetParent(torsoPivot.transform); headPivot.transform.localPosition = new Vector3(0f,0.93f,0f);
        CreateSphere("Head", new Vector3(0f,0f,0f), new Vector3(0.42f,0.48f,0.42f), skin, headPivot.transform, false);
        CreateSphere("Hair", new Vector3(0f,0.20f,-0.015f), new Vector3(0.44f,0.24f,0.43f), hair, headPivot.transform, false);
        CreateCube("Nose", new Vector3(0f,-0.02f,0.22f), new Vector3(0.08f,0.11f,0.09f), skin, headPivot.transform, false);

        GameObject lArm = new GameObject("LeftArmPivot"); lArm.transform.SetParent(torsoPivot.transform); lArm.transform.localPosition = new Vector3(-0.39f,0.68f,0f);
        CreateCylinder("UpperArm", new Vector3(0f,-0.28f,0f), new Vector3(0.16f,0.31f,0.16f), shirt, lArm.transform, false);
        CreateCylinder("ForeArm", new Vector3(0f,-0.78f,0f), new Vector3(0.14f,0.27f,0.14f), skin, lArm.transform, false);
        CreateSphere("Hand", new Vector3(0f,-1.09f,0f), new Vector3(0.18f,0.18f,0.18f), skin, lArm.transform, false);

        GameObject rArm = new GameObject("RightArmPivot"); rArm.transform.SetParent(torsoPivot.transform); rArm.transform.localPosition = new Vector3(0.39f,0.68f,0f);
        CreateCylinder("UpperArm", new Vector3(0f,-0.28f,0f), new Vector3(0.16f,0.31f,0.16f), shirt, rArm.transform, false);
        CreateCylinder("ForeArm", new Vector3(0f,-0.78f,0f), new Vector3(0.14f,0.27f,0.14f), skin, rArm.transform, false);
        CreateSphere("Hand", new Vector3(0f,-1.09f,0f), new Vector3(0.18f,0.18f,0.18f), skin, rArm.transform, false);

        GameObject lLeg = new GameObject("LeftLegPivot"); lLeg.transform.SetParent(root.transform); lLeg.transform.localPosition = new Vector3(-0.17f,0.92f,0f);
        CreateCylinder("Thigh", new Vector3(0f,-0.30f,0f), new Vector3(0.19f,0.32f,0.19f), mats["Pants"], lLeg.transform, false);
        CreateCylinder("Shin", new Vector3(0f,-0.78f,0f), new Vector3(0.16f,0.28f,0.16f), mats["Pants"], lLeg.transform, false);
        CreateCube("Shoe", new Vector3(0f,-1.08f,0.08f), new Vector3(0.30f,0.16f,0.52f), mats["Shoes"], lLeg.transform, false);

        GameObject rLeg = new GameObject("RightLegPivot"); rLeg.transform.SetParent(root.transform); rLeg.transform.localPosition = new Vector3(0.17f,0.92f,0f);
        CreateCylinder("Thigh", new Vector3(0f,-0.30f,0f), new Vector3(0.19f,0.32f,0.19f), mats["Pants"], rLeg.transform, false);
        CreateCylinder("Shin", new Vector3(0f,-0.78f,0f), new Vector3(0.16f,0.28f,0.16f), mats["Pants"], rLeg.transform, false);
        CreateCube("Shoe", new Vector3(0f,-1.08f,0.08f), new Vector3(0.30f,0.16f,0.52f), mats["Shoes"], rLeg.transform, false);

        HumanoidVisual hv = root.AddComponent<HumanoidVisual>(); hv.visualRoot=root.transform; hv.torso=torsoPivot.transform; hv.head=headPivot.transform; hv.leftArm=lArm.transform; hv.rightArm=rArm.transform; hv.leftLeg=lLeg.transform; hv.rightLeg=rLeg.transform; hv.CachePose();
        return hv;
    }

    static PoliceCarAI[] BuildPoliceFleet(Transform parent, params Transform[][] routes)
    {
        List<PoliceCarAI> result = new List<PoliceCarAI>();
        Vector3[] starts = {
            new Vector3(-72f,0.03f,-72f), new Vector3(72f,0.03f,72f),
            new Vector3(-72f,0.03f,72f), new Vector3(72f,0.03f,-72f),
            new Vector3(0f,0.03f,128f)
        };

        for (int i = 0; i < starts.Length; i++)
        {
            GameObject car = new GameObject("PoliceCar_" + (i + 1));
            car.transform.SetParent(parent);
            car.transform.position = starts[i];
            Transform[] wheels = BuildPoliceCarVisual(car.transform);

            BoxCollider bc = car.AddComponent<BoxCollider>();
            bc.size = new Vector3(1.95f,1.25f,4.3f);
            bc.center = new Vector3(0f,0.68f,0f);

            Rigidbody rb = car.AddComponent<Rigidbody>();
            rb.mass = 1250f;
            rb.linearDamping = 0.12f;
            rb.angularDamping = 2.7f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.isKinematic = true;

            PoliceCarAI ai = car.AddComponent<PoliceCarAI>();
            ai.wheels = wheels;
            ai.patrolWaypoints = routes[i % routes.Length];
            ai.patrolSpeed = 6.2f + i * 0.25f;
            ai.chaseSpeed = 11.4f + i * 0.35f;

            DriveableVehicle dv = car.AddComponent<DriveableVehicle>();
            dv.wheels = wheels;
            dv.owned = true;
            dv.purchasePrice = 0;
            dv.acceleration = 26f;
            dv.maxSpeed = 30f;
            dv.steering = 55f;
            dv.npcDriverVisual = BuildNPCDriverVisual(car.transform, 100 + i);
            dv.npcExitPedestrianTemplate = BuildNPCExitTemplate(car.transform, 500 + i);
            AddVehicleSystems(car, dv);
            AddPoliceLightbar(car, ai);

            GameObject target = new GameObject("CameraTarget");
            target.transform.SetParent(car.transform);
            target.transform.localPosition = new Vector3(0f,1.1f,0f);
            dv.cameraTarget = target.transform;

            result.Add(ai);
        }
        return result.ToArray();
    }

    static Transform[] BuildPoliceCarVisual(Transform parent)
    {
        Transform[] wheels = BuildCarVisual(parent, mats["White"], 0, true);
        if (parent.Find("CarModel_HD") == null)
        {
            CreateCube("PoliceSideL", new Vector3(-0.94f,0.72f,0f), new Vector3(0.04f,0.55f,2.4f), mats["Blue"], parent, false);
            CreateCube("PoliceSideR", new Vector3(0.94f,0.72f,0f), new Vector3(0.04f,0.55f,2.4f), mats["Blue"], parent, false);
            CreateCube("LightbarBase", new Vector3(0f,1.72f,-0.18f), new Vector3(1.25f,0.10f,0.35f), mats["Black"], parent, false);
            CreateCube("LightbarRed", new Vector3(-0.32f,1.82f,-0.18f), new Vector3(0.52f,0.16f,0.28f), mats["RedOn"], parent, false);
            CreateCube("LightbarBlue", new Vector3(0.32f,1.82f,-0.18f), new Vector3(0.52f,0.16f,0.28f), mats["Blue"], parent, false);
        }
        return wheels;
    }

    static GameObject BuildDriveableCar(string name, Vector3 pos, Quaternion rot, Transform parent, Material body, int style, bool owned, int price)
    {
        GameObject car = new GameObject(name);
        car.transform.SetParent(parent);
        car.transform.position = pos;
        car.transform.rotation = rot;
        Transform[] wheels = BuildCarVisual(car.transform, body, style);
        BoxCollider bc = car.AddComponent<BoxCollider>();
        bc.size = new Vector3(style == 2 ? 2.15f : 1.95f, style == 2 ? 1.45f : 1.25f, style == 3 ? 4.75f : 4.25f);
        bc.center = new Vector3(0f,0.68f,0f);
        Rigidbody rb = car.AddComponent<Rigidbody>();
        rb.mass = style == 2 ? 1450f : 1180f;
        rb.linearDamping = 0.12f;
        rb.angularDamping = 2.7f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        DriveableVehicle dv = car.AddComponent<DriveableVehicle>();
        dv.wheels = wheels;
        dv.owned = owned;
        dv.purchasePrice = price;
        dv.acceleration = style == 3 ? 28f : style == 2 ? 18f : 23f;
        dv.maxSpeed = style == 3 ? 31f : style == 2 ? 23f : 27f;
        dv.steering = style == 2 ? 45f : 54f;
        AddVehicleSystems(car, dv);
        GameObject target = new GameObject("CameraTarget");
        target.transform.SetParent(car.transform);
        target.transform.localPosition = new Vector3(0f,1.1f,0f);
        dv.cameraTarget = target.transform;
        return car;
    }

    static GameObject BuildPlayerCar(Vector3 pos, Quaternion rot, Transform parent)
    {
        GameObject car = new GameObject("PlayerCar"); car.transform.SetParent(parent); car.transform.position = pos; car.transform.rotation = rot;
        Transform[] wheels = BuildCarVisual(car.transform, mats["Red"], 0);
        BoxCollider bc = car.AddComponent<BoxCollider>(); bc.size = new Vector3(1.9f,1.20f,4.2f); bc.center = new Vector3(0f,0.65f,0f);
        Rigidbody rb = car.AddComponent<Rigidbody>(); rb.mass=1150f; rb.linearDamping=0.15f; rb.angularDamping=2.5f; rb.interpolation=RigidbodyInterpolation.Interpolate;
        DriveableVehicle dv = car.AddComponent<DriveableVehicle>(); dv.wheels=wheels; dv.acceleration=17f; dv.maxSpeed=22f;
        AddVehicleSystems(car, dv);
        GameObject target = new GameObject("CameraTarget"); target.transform.SetParent(car.transform); target.transform.localPosition = new Vector3(0f,1.1f,0f); dv.cameraTarget = target.transform;
        return car;
    }

    static GameObject BuildPlayerAircraft(Vector3 pos, Quaternion rot, Transform parent)
    {
        GameObject aircraft = new GameObject("PlayerOnlyAircraft");
        aircraft.transform.SetParent(parent);
        aircraft.transform.position = pos;
        aircraft.transform.rotation = rot;

        GameObject asset = AssetStoreAssetResolver.GetAircraft();
        GameObject model = AssetStoreAssetResolver.InstantiateAsset(asset, aircraft.transform, "StarSparrowModel");
        if (model != null)
        {
            if (!AssetStoreAssetResolver.NormalizeLength(model, 8.5f, 0.15f)) Object.DestroyImmediate(model);
        }
        if (model == null)
        {
            CreateCylinder("Fuselage", new Vector3(0f,0.8f,0f), new Vector3(1.15f,3.9f,1.15f), mats["Silver"], aircraft.transform, false).transform.localRotation = Quaternion.Euler(90f,0f,0f);
            CreateCube("MainWing", new Vector3(0f,0.8f,0f), new Vector3(8.2f,0.18f,1.55f), mats["Blue"], aircraft.transform, false);
            CreateCube("TailWing", new Vector3(0f,1.0f,-3.0f), new Vector3(3.1f,0.14f,0.75f), mats["Blue"], aircraft.transform, false);
            CreateCube("TailFin", new Vector3(0f,1.65f,-3.15f), new Vector3(0.16f,1.35f,0.9f), mats["Red"], aircraft.transform, false);
        }

        BoxCollider collider = aircraft.AddComponent<BoxCollider>();
        collider.size = new Vector3(8.2f, 1.8f, 7.8f);
        collider.center = new Vector3(0f, 0.9f, 0f);
        Rigidbody rb = aircraft.AddComponent<Rigidbody>();
        rb.mass = 1250f;
        PlayerAircraft flight = aircraft.AddComponent<PlayerAircraft>();
        GameObject target = new GameObject("AircraftCameraTarget");
        target.transform.SetParent(aircraft.transform);
        target.transform.localPosition = new Vector3(0f, 1.6f, -0.5f);
        flight.cameraTarget = target.transform;
        return aircraft;
    }

    static void AddVehicleSystems(GameObject car, DriveableVehicle vehicle)
    {
        AddWheelColliderRig(car, vehicle);

        if (car.GetComponent<VehicleDamage>() == null)
            car.AddComponent<VehicleDamage>();

        VehicleLights lights = car.GetComponent<VehicleLights>();
        if (lights == null) lights = car.AddComponent<VehicleLights>();
        lights.vehicle = vehicle;
        lights.brakeOn = mats["RedOn"];
        lights.brakeOff = mats["RedOff"];

        List<MeshRenderer> tails = new List<MeshRenderer>();
        Transform tailL = car.transform.Find("TailL");
        Transform tailR = car.transform.Find("TailR");
        if (tailL != null && tailL.GetComponent<MeshRenderer>() != null) tails.Add(tailL.GetComponent<MeshRenderer>());
        if (tailR != null && tailR.GetComponent<MeshRenderer>() != null) tails.Add(tailR.GetComponent<MeshRenderer>());
        lights.brakeLights = tails.ToArray();

        GameObject headlight = new GameObject("HeadlightBeam");
        headlight.transform.SetParent(car.transform);
        headlight.transform.localPosition = new Vector3(0f, 0.82f, 2.0f);
        headlight.transform.localRotation = Quaternion.identity;
        Light beam = headlight.AddComponent<Light>();
        beam.type = LightType.Spot;
        beam.range = 23f;
        beam.spotAngle = 48f;
        beam.intensity = 1.15f;
        beam.color = new Color(1f, 0.93f, 0.78f);
        beam.shadows = LightShadows.None;
        beam.enabled = false;
        lights.headlights = new[] { beam };
    }

    static void AddWheelColliderRig(GameObject car, DriveableVehicle vehicle)
    {
        BoxCollider body = car.GetComponent<BoxCollider>();
        Vector3 size = body != null ? body.size : new Vector3(1.95f, 1.25f, 4.25f);
        float halfTrack = size.x * 0.43f;
        float axle = size.z * 0.34f;
        float radius = Mathf.Clamp(size.y * 0.29f, 0.32f, 0.44f);
        float wheelY = radius + 0.04f;

        GameObject root = new GameObject("WheelColliders");
        root.transform.SetParent(car.transform, false);
        vehicle.frontLeftWheel = CreateWheelCollider("FrontLeft", new Vector3(-halfTrack, wheelY, axle), radius, root.transform);
        vehicle.frontRightWheel = CreateWheelCollider("FrontRight", new Vector3(halfTrack, wheelY, axle), radius, root.transform);
        vehicle.rearLeftWheel = CreateWheelCollider("RearLeft", new Vector3(-halfTrack, wheelY, -axle), radius, root.transform);
        vehicle.rearRightWheel = CreateWheelCollider("RearRight", new Vector3(halfTrack, wheelY, -axle), radius, root.transform);

        if (vehicle.wheels != null && vehicle.wheels.Length >= 4)
        {
            Vector3[] targets = {
                new Vector3(-halfTrack, wheelY, axle), new Vector3(halfTrack, wheelY, axle),
                new Vector3(-halfTrack, wheelY, -axle), new Vector3(halfTrack, wheelY, -axle)
            };
            List<Transform> remaining = new List<Transform>(vehicle.wheels);
            Transform[] ordered = new Transform[4];
            for (int i = 0; i < ordered.Length; i++)
            {
                float best = float.MaxValue;
                for (int j = 0; j < remaining.Count; j++)
                {
                    Vector3 local = car.transform.InverseTransformPoint(remaining[j].position);
                    float distance = (local - targets[i]).sqrMagnitude;
                    if (distance < best) { best = distance; ordered[i] = remaining[j]; }
                }
                if (ordered[i] != null) remaining.Remove(ordered[i]);
            }
            vehicle.wheels = ordered;
        }
    }

    static WheelCollider CreateWheelCollider(string name, Vector3 localPosition, float radius, Transform parent)
    {
        GameObject wheel = new GameObject(name + "WheelCollider");
        wheel.transform.SetParent(parent, false);
        wheel.transform.localPosition = localPosition;
        WheelCollider collider = wheel.AddComponent<WheelCollider>();
        collider.radius = radius;
        collider.mass = 24f;
        collider.suspensionDistance = 0.22f;
        JointSpring spring = collider.suspensionSpring;
        spring.spring = 31000f;
        spring.damper = 4200f;
        spring.targetPosition = 0.48f;
        collider.suspensionSpring = spring;
        WheelFrictionCurve forward = collider.forwardFriction;
        forward.extremumSlip = 0.38f;
        forward.extremumValue = 1.15f;
        forward.asymptoteSlip = 0.82f;
        forward.asymptoteValue = 0.78f;
        forward.stiffness = 1.35f;
        collider.forwardFriction = forward;
        return collider;
    }

    static void AddPoliceLightbar(GameObject car, PoliceCarAI ai)
    {
        PoliceLightbar bar = car.AddComponent<PoliceLightbar>();
        bar.policeAI = ai;

        GameObject red = new GameObject("PoliceFlashRed");
        red.transform.SetParent(car.transform);
        red.transform.localPosition = new Vector3(-0.32f, 1.92f, -0.18f);
        Light redLight = red.AddComponent<Light>();
        redLight.type = LightType.Point;
        redLight.range = 11f;
        redLight.intensity = 2.2f;
        redLight.color = new Color(1f, 0.04f, 0.02f);
        redLight.shadows = LightShadows.None;
        redLight.enabled = false;

        GameObject blue = new GameObject("PoliceFlashBlue");
        blue.transform.SetParent(car.transform);
        blue.transform.localPosition = new Vector3(0.32f, 1.92f, -0.18f);
        Light blueLight = blue.AddComponent<Light>();
        blueLight.type = LightType.Point;
        blueLight.range = 11f;
        blueLight.intensity = 2.2f;
        blueLight.color = new Color(0.04f, 0.18f, 1f);
        blueLight.shadows = LightShadows.None;
        blueLight.enabled = false;

        bar.redLight = redLight;
        bar.blueLight = blueLight;
    }

    static GameObject BuildNPCExitTemplate(Transform car, int seed)
    {
        GameObject root = new GameObject("NPCDriverExitTemplate");
        root.transform.SetParent(car);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;

        CharacterController cc = root.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.32f;
        cc.center = new Vector3(0f, 0.9f, 0f);
        cc.stepOffset = 0.35f;

        HumanoidVisual fallback = BuildHumanoid(root.transform, seed, false);
        AnimatorMotionBridge bridge = BuildAssetStoreHumanoid(root.transform, seed, false);
        HumanoidVisual visual = fallback;
        if (bridge != null) { fallback.gameObject.SetActive(false); visual = null; }
        FleeingDriver fleeing = root.AddComponent<FleeingDriver>();
        fleeing.visual = visual;
        fleeing.animatorBridge = bridge;
        root.SetActive(false);
        return root;
    }

    static GameObject BuildNPCDriverVisual(Transform car, int seed)
    {
        GameObject root = new GameObject("NPC_Driver");
        root.transform.SetParent(car);
        // Keep the head below the Lab Coupe roofline; the previous generic-car
        // offset made drivers float visibly above the imported vehicle.
        root.transform.localPosition = new Vector3(-0.34f, 0.30f, 0.10f);
        root.transform.localRotation = Quaternion.identity;

        Material skin = mats[(seed % 3) == 0 ? "SkinA" : (seed % 3) == 1 ? "SkinB" : "SkinC"];
        Material shirt = mats[(seed % 4) == 0 ? "ShirtA" : (seed % 4) == 1 ? "ShirtB" : (seed % 4) == 2 ? "ShirtC" : "ShirtD"];

        CreateCube("Torso", new Vector3(0f,0.34f,0f), new Vector3(0.42f,0.48f,0.28f), shirt, root.transform, false);
        CreateSphere("Head", new Vector3(0f,0.72f,0.02f), new Vector3(0.28f,0.34f,0.28f), skin, root.transform, false);
        CreateCube("ArmL", new Vector3(-0.25f,0.34f,0.16f), new Vector3(0.12f,0.38f,0.12f), skin, root.transform, false);
        CreateCube("ArmR", new Vector3(0.25f,0.34f,0.16f), new Vector3(0.12f,0.38f,0.12f), skin, root.transform, false);
        return root;
    }

    static bool TryRendererBounds(GameObject root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            bounds = new Bounds(root.transform.position, Vector3.one);
            return false;
        }
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return true;
    }

    static void NormalizeCharacterModel(GameObject model, Transform parent, float targetHeight)
    {
        Bounds b;
        if (!TryRendererBounds(model, out b) || b.size.y < 0.001f) return;
        float scale = targetHeight / b.size.y;
        model.transform.localScale *= scale;
        if (!TryRendererBounds(model, out b)) return;
        Vector3 parentOrigin = parent.position;
        model.transform.position += new Vector3(parentOrigin.x - b.center.x, parentOrigin.y - b.min.y, parentOrigin.z - b.center.z);
    }

    public static void NormalizeCarModel(GameObject model, Transform parent, float targetLength)
    {
        Quaternion originalRotation = parent.rotation;
        parent.rotation = Quaternion.identity;
        Bounds b;
        if (!TryRendererBounds(model, out b)) return;
        if (b.size.x > b.size.z * 1.15f)
        {
            model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            if (!TryRendererBounds(model, out b)) return;
        }
        float sourceLength = Mathf.Max(b.size.x, b.size.z);
        if (sourceLength < 0.001f) return;
        model.transform.localScale *= targetLength / sourceLength;
        if (!TryRendererBounds(model, out b)) return;
        Vector3 parentOrigin = parent.position;
        model.transform.position += new Vector3(parentOrigin.x - b.center.x, parentOrigin.y - b.min.y + 0.03f, parentOrigin.z - b.center.z);
        parent.rotation = originalRotation;
    }

    static Transform[] BuildExternalCarVisual(Transform parent, int style, bool police)
    {
        GameObject asset = parent.name == "PlayerCar" ? AssetStoreAssetResolver.GetHeroVehicle() : AssetStoreAssetResolver.GetVehicle(style, police);
        if (asset == null) return null;
        GameObject model = AssetStoreAssetResolver.InstantiateAsset(asset, parent, "CarModel_" + asset.name);
        if (model == null) return null;
        float targetLength = style == 3 ? 4.65f : style == 2 ? 4.45f : 4.20f;
        if (model.GetComponentsInChildren<Renderer>(true).Length == 0)
        {
            Object.DestroyImmediate(model);
            return null;
        }
        NormalizeCarModel(model, parent, targetLength);
        var labWheels = new List<Transform>();
        foreach(string wheelName in new[]{"Lab_Tire_FL","Lab_Tire_FR","Lab_Tire_RL","Lab_Tire_RR"})
            foreach(var t in model.GetComponentsInChildren<Transform>(true))if(t.name==wheelName)labWheels.Add(t);
        if(labWheels.Count==4)return labWheels.ToArray();
        List<Transform> wheels = new List<Transform>();
        foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLowerInvariant();
            if (!n.Contains("steering") && (n.Contains("wheel") || n.Contains("tire") || n.Contains("tyre"))) wheels.Add(t);
        }
        CreateCube("TailL", new Vector3(-0.52f,0.70f,-2.02f), new Vector3(0.24f,0.16f,0.06f), mats["RedOff"], parent, false);
        CreateCube("TailR", new Vector3(0.52f,0.70f,-2.02f), new Vector3(0.24f,0.16f,0.06f), mats["RedOff"], parent, false);
        return wheels.ToArray();
    }

    static Transform[] BuildCarVisual(Transform parent, Material body, int style, bool police = false)
    {
        Transform[] external = BuildExternalCarVisual(parent, style, police);
        if (external != null) return external;

        List<Transform> wheels = new List<Transform>();
        float width = style == 2 ? 2.05f : 1.90f;
        float bodyZ = style == 3 ? 4.65f : style == 2 ? 4.45f : 4.15f;
        float bodyY = style == 2 ? 0.72f : 0.56f;
        float cabinY = style == 2 ? 0.90f : style == 1 ? 0.68f : 0.62f;
        float cabinZ = style == 3 ? 2.05f : style == 2 ? 2.35f : 1.92f;

        CreateCube("LowerBody", new Vector3(0f,0.58f,0f), new Vector3(width,bodyY,bodyZ), body, parent, false);
        CreateCube("SillL", new Vector3(-width*0.49f,0.38f,0f), new Vector3(0.12f,0.20f,bodyZ*0.78f), mats["Black"], parent, false);
        CreateCube("SillR", new Vector3(width*0.49f,0.38f,0f), new Vector3(0.12f,0.20f,bodyZ*0.78f), mats["Black"], parent, false);

        GameObject hood = CreateCube("Hood", new Vector3(0f,0.88f,bodyZ*0.31f), new Vector3(width*0.92f,0.20f,bodyZ*0.28f), body, parent, false);
        hood.transform.localRotation = Quaternion.Euler(-3f,0f,0f);
        GameObject trunk = CreateCube("Trunk", new Vector3(0f,0.88f,-bodyZ*0.37f), new Vector3(width*0.92f,0.20f,bodyZ*0.16f), body, parent, false);
        trunk.transform.localRotation = Quaternion.Euler(2f,0f,0f);

        CreateCube("Cabin", new Vector3(0f,1.18f,-0.15f), new Vector3(width*0.82f,cabinY,cabinZ), body, parent, false);
        CreateCube("Roof", new Vector3(0f,1.60f,-0.20f), new Vector3(width*0.78f,0.14f,cabinZ*0.78f), body, parent, false);

        GameObject windshield = CreateCube("Windshield", new Vector3(0f,1.25f,0.80f), new Vector3(width*0.72f,0.52f,0.08f), mats["Glass"], parent, false);
        windshield.transform.localRotation = Quaternion.Euler(-19f,0f,0f);
        GameObject rearGlass = CreateCube("RearGlass", new Vector3(0f,1.24f,-1.05f), new Vector3(width*0.72f,0.50f,0.08f), mats["Glass"], parent, false);
        rearGlass.transform.localRotation = Quaternion.Euler(18f,0f,0f);

        CreateCube("LeftWindowFront", new Vector3(-width*0.415f,1.26f,0.28f), new Vector3(0.055f,0.46f,0.72f), mats["Glass"], parent, false);
        CreateCube("LeftWindowRear", new Vector3(-width*0.415f,1.26f,-0.50f), new Vector3(0.055f,0.46f,0.62f), mats["Glass"], parent, false);
        CreateCube("RightWindowFront", new Vector3(width*0.415f,1.26f,0.28f), new Vector3(0.055f,0.46f,0.72f), mats["Glass"], parent, false);
        CreateCube("RightWindowRear", new Vector3(width*0.415f,1.26f,-0.50f), new Vector3(0.055f,0.46f,0.62f), mats["Glass"], parent, false);

        CreateCube("MirrorL", new Vector3(-width*0.57f,1.17f,0.62f), new Vector3(0.22f,0.15f,0.28f), body, parent, false);
        CreateCube("MirrorR", new Vector3(width*0.57f,1.17f,0.62f), new Vector3(0.22f,0.15f,0.28f), body, parent, false);
        CreateCube("FrontBumper", new Vector3(0f,0.46f,bodyZ*0.5f+0.08f), new Vector3(width*0.94f,0.20f,0.18f), mats["Black"], parent, false);
        CreateCube("RearBumper", new Vector3(0f,0.46f,-bodyZ*0.5f-0.08f), new Vector3(width*0.94f,0.20f,0.18f), mats["Black"], parent, false);
        CreateCube("Grille", new Vector3(0f,0.63f,bodyZ*0.5f+0.11f), new Vector3(width*0.52f,0.26f,0.06f), mats["Metal"], parent, false);
        CreateCube("PlateFront", new Vector3(0f,0.42f,bodyZ*0.5f+0.13f), new Vector3(0.58f,0.18f,0.04f), mats["White"], parent, false);
        CreateCube("PlateRear", new Vector3(0f,0.42f,-bodyZ*0.5f-0.13f), new Vector3(0.58f,0.18f,0.04f), mats["White"], parent, false);
        CreateCube("HeadlightL", new Vector3(-width*0.31f,0.72f,bodyZ*0.5f+0.10f), new Vector3(0.42f,0.22f,0.07f), mats["White"], parent, false);
        CreateCube("HeadlightR", new Vector3(width*0.31f,0.72f,bodyZ*0.5f+0.10f), new Vector3(0.42f,0.22f,0.07f), mats["White"], parent, false);
        CreateCube("TailL", new Vector3(-width*0.31f,0.72f,-bodyZ*0.5f-0.10f), new Vector3(0.42f,0.22f,0.07f), mats["RedOff"], parent, false);
        CreateCube("TailR", new Vector3(width*0.31f,0.72f,-bodyZ*0.5f-0.10f), new Vector3(0.42f,0.22f,0.07f), mats["RedOff"], parent, false);

        if (style == 3)
        {
            CreateCube("SpoilerBar", new Vector3(0f,1.08f,-bodyZ*0.47f), new Vector3(width*0.80f,0.10f,0.24f), mats["Black"], parent, false);
            CreateCube("SpoilerLegL", new Vector3(-0.55f,0.98f,-bodyZ*0.46f), new Vector3(0.10f,0.30f,0.10f), mats["Black"], parent, false);
            CreateCube("SpoilerLegR", new Vector3(0.55f,0.98f,-bodyZ*0.46f), new Vector3(0.10f,0.30f,0.10f), mats["Black"], parent, false);
        }
        if (style == 2)
        {
            CreateCube("RoofRackL", new Vector3(-0.58f,1.77f,-0.18f), new Vector3(0.08f,0.10f,1.65f), mats["Black"], parent, false);
            CreateCube("RoofRackR", new Vector3(0.58f,1.77f,-0.18f), new Vector3(0.08f,0.10f,1.65f), mats["Black"], parent, false);
        }

        float wz = bodyZ * 0.33f;
        Vector3[] wheelPos = {
            new Vector3(-width*0.53f,0.42f,wz), new Vector3(width*0.53f,0.42f,wz),
            new Vector3(-width*0.53f,0.42f,-wz), new Vector3(width*0.53f,0.42f,-wz)
        };
        foreach (Vector3 p in wheelPos)
        {
            GameObject w = CreateCylinder("Wheel", p, new Vector3(0.72f,0.19f,0.72f), mats["Black"], parent, false);
            w.transform.localRotation = Quaternion.Euler(0f,0f,90f);
            GameObject hub = CreateCylinder("Hub", Vector3.zero, new Vector3(0.40f,0.20f,0.40f), mats["Silver"], w.transform, false);
            hub.transform.localRotation = Quaternion.identity;
            wheels.Add(w.transform);
        }
        return wheels.ToArray();
    }

    static Camera BuildCamera(Transform target)
    {
        GameObject go = new GameObject("Main Camera"); go.tag = "MainCamera"; go.transform.position = target.position + new Vector3(0f,4f,-8f);
        Camera cam = go.AddComponent<Camera>(); cam.fieldOfView = 65f; cam.nearClipPlane = 0.12f; cam.farClipPlane = 700f;
        go.AddComponent<AudioListener>();
        ThirdPersonCamera follow = go.AddComponent<ThirdPersonCamera>(); follow.target=target; follow.distance=9.2f; follow.vehicleDistance=10.8f; follow.height=2.1f; follow.vehicleHeight=2.5f; follow.sensitivity=3f;
        return cam;
    }

    static Light BuildSun()
    {
        GameObject go = new GameObject("Sun"); Light l = go.AddComponent<Light>(); l.type=LightType.Directional; l.intensity=1.1f; l.shadows=LightShadows.Soft; l.shadowStrength=0.8f; go.transform.rotation=Quaternion.Euler(35f,-35f,0f); return l;
    }

    static void BuildMissions(Transform parent, VehicleInteractor player, EconomySystem economy)
    {
        GameObject root = new GameObject("Mission System");
        root.transform.SetParent(parent);
        Vector3[] locations = {
            new Vector3(-40f,0.3f,40f),
            new Vector3(40f,0.3f,40f),
            new Vector3(120f,0.3f,-40f),
            new Vector3(-40f,0.3f,-40f),
            new Vector3(120f,0.3f,120f),
            new Vector3(-120f,0.3f,40f),
            new Vector3(40f,0.3f,-40f),
            new Vector3(-120f,0.3f,-120f)
        };
        GameObject[] markers = new GameObject[locations.Length];
        for (int i = 0; i < locations.Length; i++)
        {
            GameObject m = new GameObject("MissionMarker_" + (i + 1));
            m.transform.SetParent(root.transform);
            m.transform.position = locations[i];
            Material color = i == 2 ? mats["GreenOn"] : (i >= 3 && i <= 5 ? mats["Yellow"] : mats["Marker"]);
            CreateCylinder("Ring", new Vector3(0f,0.1f,0f), new Vector3(3.5f,0.03f,3.5f), color, m.transform, false);
            // Ground-only marker cannot obscure the third-person camera.
            m.AddComponent<MissionMarker>();
            markers[i] = m;
        }
        MissionSystem ms = root.AddComponent<MissionSystem>();
        ms.player = player;
        ms.economy = economy;
        ms.missionMarkers = markers;
        ms.completeRadius = 5f;
        ms.requiresVehicle = new[] { false, false, false, true, true, true, false, true };
        ms.rewards = new[] { 200, 250, 300, 400, 450, 500, 550, 750 };
    }

    static GameObject CreateCube(string name, Vector3 localPos, Vector3 localScale, Material mat, Transform parent, bool collider, bool local = false)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name=name; g.transform.SetParent(parent);
        if (local) g.transform.localPosition=localPos; else g.transform.localPosition=localPos;
        g.transform.localScale=localScale;
        if (mat != null) g.GetComponent<MeshRenderer>().sharedMaterial=mat;
        Collider c=g.GetComponent<Collider>(); if (!collider && c!=null) Object.DestroyImmediate(c);
        return g;
    }

    static GameObject CreateCylinder(string name, Vector3 localPos, Vector3 localScale, Material mat, Transform parent, bool collider)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); g.name=name; g.transform.SetParent(parent); g.transform.localPosition=localPos; g.transform.localScale=localScale;
        if (mat!=null) g.GetComponent<MeshRenderer>().sharedMaterial=mat; Collider c=g.GetComponent<Collider>(); if(!collider&&c!=null) Object.DestroyImmediate(c); return g;
    }

    static GameObject CreateSphere(string name, Vector3 localPos, Vector3 localScale, Material mat, Transform parent, bool collider)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere); g.name=name; g.transform.SetParent(parent); g.transform.localPosition=localPos; g.transform.localScale=localScale;
        if(mat!=null)g.GetComponent<MeshRenderer>().sharedMaterial=mat; Collider c=g.GetComponent<Collider>(); if(!collider&&c!=null) Object.DestroyImmediate(c); return g;
    }
}
#endif
