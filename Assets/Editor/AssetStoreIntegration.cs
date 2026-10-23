#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class MiniGTAAssetStoreWindow : EditorWindow
{
    // Asset Store models are detected by content, so package folder names may vary by release.
    const string CharacterUrl = "https://assetstore.unity.com/packages/3d/characters/low-poly-soldiers-demo-73611";
    const string VehicleUrl = "https://assetstore.unity.com/packages/3d/vehicles/land/hdrp-lab-car-free-realistic-car-model-185778";
    const string AircraftUrl = "https://assetstore.unity.com/packages/3d/vehicles/space/star-sparrow-modular-spaceship-73167";

    [MenuItem("MiniGTA/Asset Store Setup V8")]
    public static void OpenWindow()
    {
        GetWindow<MiniGTAAssetStoreWindow>("MiniGTA V8 Assets").minSize = new Vector2(520, 420);
    }

    void OnGUI()
    {
        GUILayout.Space(8);
        EditorGUILayout.LabelField("MiniGTA V8 — Unity Asset Store setup", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "V8 always keeps a built-in fallback player, so the character cannot disappear. " +
            "For higher-quality art, add the free Asset Store packages below to your Unity account and import them into this project. " +
            "Then press Scan + Rebuild. Asset Store packages are not redistributed inside this ZIP.",
            MessageType.Info);

        DrawAsset("Characters + animations", "Low Poly Soldiers Demo", CharacterUrl,
            AssetStoreAssetResolver.CountCharacterCandidates());
        DrawAsset("Vehicles", "HDRP Lab Car Free (model is reused with safe materials)", VehicleUrl,
            AssetStoreAssetResolver.CountVehicleCandidates());
        DrawAsset("Player aircraft", "Star Sparrow Modular Spaceship", AircraftUrl,
            AssetStoreAssetResolver.CountAircraftCandidates());

        GUILayout.Space(12);
        if (GUILayout.Button("Scan imported assets + Rebuild V8 City", GUILayout.Height(42)))
        {
            AssetDatabase.Refresh();
            AssetStoreAssetResolver.ClearCache();
            MiniGTABuilder.BuildEnhancedCity();
        }
        GUILayout.Space(4);
        if (GUILayout.Button("Rebuild using only built-in fallback art", GUILayout.Height(30)))
        {
            EditorPrefs.SetBool("MiniGTA_V8_IgnoreAssetStore", true);
            AssetStoreAssetResolver.ClearCache();
            MiniGTABuilder.BuildEnhancedCity();
        }
        if (GUILayout.Button("Enable Asset Store art again", GUILayout.Height(26)))
        {
            EditorPrefs.SetBool("MiniGTA_V8_IgnoreAssetStore", false);
            AssetStoreAssetResolver.ClearCache();
        }

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Detected", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Character candidates", AssetStoreAssetResolver.CountCharacterCandidates().ToString());
        EditorGUILayout.LabelField("Vehicle candidates", AssetStoreAssetResolver.CountVehicleCandidates().ToString());
        EditorGUILayout.LabelField("Environment candidates", AssetStoreAssetResolver.CountEnvironmentCandidates().ToString());
        EditorGUILayout.LabelField("Aircraft candidates", AssetStoreAssetResolver.CountAircraftCandidates().ToString());
    }

    static void DrawAsset(string label, string package, string url, int found)
    {
        GUILayout.Space(8);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        EditorGUILayout.LabelField(package);
        EditorGUILayout.LabelField(found > 0 ? ("Detected: " + found) : "Not imported yet");
        if (GUILayout.Button("Open Asset Store page")) Application.OpenURL(url);
        EditorGUILayout.EndVertical();
    }
}

[InitializeOnLoad]
public static class MiniGTAV8Bootstrap
{
    const int SceneRevision = 20;
    static MiniGTAV8Bootstrap() => EditorApplication.delayCall += FirstOpen;

    static void FirstOpen()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Main.unity") == null ||
            EditorPrefs.GetInt("MiniGTA_V8_SceneRevision", 0) < SceneRevision)
        {
            MiniGTABuilder.BuildEnhancedCity();
            EditorPrefs.SetInt("MiniGTA_V8_SceneRevision", SceneRevision);
            try { MiniGTAVisualValidation.CaptureMainCamera(); }
            catch (Exception ex) { Debug.LogWarning("MiniGTA preview capture skipped: " + ex.Message); }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Main.unity") == null)
                EditorApplication.delayCall += () => MiniGTAAssetStoreWindow.OpenWindow();
        }
    }
}

[InitializeOnLoad]
public static class MiniGTAAutoPlay
{
    static MiniGTAAutoPlay()
    {
        if (Environment.GetCommandLineArgs().Any(a => a == "-miniGTAAutoPlay"))
            EditorApplication.delayCall += EnterPlayWhenReady;
    }

    static void EnterPlayWhenReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnterPlayWhenReady;
            return;
        }
        SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Main.unity");
        if (scene == null) return;
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        EditorApplication.EnterPlaymode();
    }
}

[InitializeOnLoad]
public static class MiniGTAStartupGuard
{
    const string MainScene = "Assets/Scenes/Main.unity";

    static MiniGTAStartupGuard() => EditorApplication.delayCall += EnsureMainScene;

    static void EnsureMainScene()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnsureMainScene;
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != MainScene &&
            AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScene) != null)
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(MainScene);
    }
}

public static class MiniGTAVisualValidation
{
    public static void BuildSmokePlayer()
    {
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/SmokeTest"));
        Directory.CreateDirectory(folder);
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = Path.Combine(folder, "MiniGTA.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        };
        UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new InvalidOperationException("Smoke build failed: " + report.summary.result);
        Debug.Log("MiniGTA smoke player built: " + options.locationPathName);
    }

    public static void CaptureMainCamera()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        Camera camera = UnityEngine.Object.FindAnyObjectByType<Camera>();
        if (camera == null) throw new InvalidOperationException("Main scene has no camera.");
        const int width = 1280;
        const int height = 720;
        RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        RenderTexture oldTarget = camera.targetTexture;
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply();
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/MainCameraPreview.png"));
        File.WriteAllBytes(output, image.EncodeToPNG());
        camera.targetTexture = oldTarget;
        RenderTexture.active = previous;
        UnityEngine.Object.DestroyImmediate(image);
        target.Release();
        UnityEngine.Object.DestroyImmediate(target);
        Debug.Log("MiniGTA visual preview saved: " + output);
    }
}

public static class AssetStoreAssetResolver
{
    static List<GameObject> characters;
    static List<GameObject> vehicles;
    static List<GameObject> environments;
    static List<GameObject> weapons;
    static List<GameObject> aircraft;
    static readonly Dictionary<string, RuntimeAnimatorController> characterControllers = new Dictionary<string, RuntimeAnimatorController>();

    public static void ClearCache() { characters = null; vehicles = null; environments = null; weapons = null; aircraft = null; characterControllers.Clear(); }

    public static int CountCharacterCandidates() { EnsureCharacters(); return characters.Count; }
    public static int CountVehicleCandidates() { EnsureVehicles(); return vehicles.Count; }
    public static int CountEnvironmentCandidates() { EnsureEnvironments(); return environments.Count; }
    public static int CountWeaponCandidates() { EnsureWeapons(); return weapons.Count; }
    public static int CountAircraftCandidates() { EnsureAircraft(); return aircraft.Count; }

    public static GameObject GetAircraft()
    {
        if (EditorPrefs.GetBool("MiniGTA_V8_IgnoreAssetStore", false)) return null;
        EnsureAircraft();
        return aircraft.Count == 0 ? null : aircraft[0];
    }

    public static GameObject GetWeapon(int index)
    {
        EnsureWeapons();
        return weapons.Count == 0 ? null : weapons[Mathf.Abs(index) % weapons.Count];
    }

    public static void ConfigureKenneyCharacterAnimations()
    {
        const string marker = "MiniGTA_KenneyLoopV1";
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/ThirdParty/Kenney/BlockyCharacters/Models/FBX format" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null || importer.userData.Contains(marker)) continue;
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++)
            {
                string lower = clips[i].name.ToLowerInvariant();
                clips[i].loopTime = !lower.Contains("jump") && !lower.Contains("fall") && !lower.Contains("death");
                clips[i].loopPose = clips[i].loopTime;
            }
            importer.clipAnimations = clips;
            importer.userData = string.IsNullOrEmpty(importer.userData) ? marker : importer.userData + ";" + marker;
            importer.SaveAndReimport();
        }
        ClearCache();
    }

    public static GameObject GetCharacter(int index)
    {
        if (EditorPrefs.GetBool("MiniGTA_V8_IgnoreAssetStore", false)) return null;
        EnsureCharacters();
        return characters.Count == 0 ? null : characters[Mathf.Abs(index) % characters.Count];
    }

    public static GameObject GetVehicle(int index, bool police = false)
    {
        if (EditorPrefs.GetBool("MiniGTA_V8_IgnoreAssetStore", false)) return null;
        EnsureVehicles();
        if (vehicles.Count == 0) return null;
        string[] fleet = { "sedan", "taxi", "ambulance", "suv", "hatchback-sports", "delivery", "sedan-sports" };
        string desired = police ? "police" : fleet[Mathf.Abs(index) % fleet.Length];
        GameObject selected = vehicles.FirstOrDefault(v => v.name == desired);
        if (selected != null) return selected;
        List<GameObject> lightweight = vehicles.Where(v => !AssetDatabase.GetAssetPath(v).ToLowerInvariant().Contains("assets/hdrplab/")).ToList();
        if (lightweight.Count == 0) lightweight = vehicles;
        if (police)
        {
            GameObject p = vehicles.FirstOrDefault(v => v.name.ToLowerInvariant().Contains("police") || v.name.ToLowerInvariant().Contains("cop"));
            if (p != null) return p;
        }
        return lightweight[Mathf.Abs(index) % lightweight.Count];
    }

    public static GameObject GetHeroVehicle()
    {
        if (EditorPrefs.GetBool("MiniGTA_V8_IgnoreAssetStore", false)) return null;
        EnsureVehicles();
        // Prefer the clean model asset. The package's presentation prefabs also contain
        // studio cameras, audio listeners and environment helpers that do not belong in
        // a gameplay vehicle and caused duplicate listeners and broken-looking scaling.
        GameObject cleanModel = vehicles.FirstOrDefault(v =>
        {
            string path = AssetDatabase.GetAssetPath(v).Replace('\\', '/').ToLowerInvariant();
            return path.EndsWith("/labcoupe_md.fbx");
        });
        if (cleanModel != null) return cleanModel;
        return vehicles.FirstOrDefault(v => AssetDatabase.GetAssetPath(v).ToLowerInvariant().Contains("assets/hdrplab/") && v.name.ToLowerInvariant().Contains("labcoupe"));
    }

    public static GameObject GetEnvironment(int index)
    {
        if (EditorPrefs.GetBool("MiniGTA_V8_IgnoreAssetStore", false)) return null;
        EnsureEnvironments();
        return environments.Count == 0 ? null : environments[Mathf.Abs(index) % environments.Count];
    }

    public static GameObject InstantiateAsset(GameObject asset, Transform parent, string name, bool stripPhysics = true)
    {
        if (asset == null) return null;
        GameObject go = null;
        if(AssetDatabase.GetAssetPath(asset).ToLowerInvariant().Contains("assets/hdrplab/") && asset.name.ToLowerInvariant().Contains("labcoupe"))
        {
            var presentation=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HDRPLab/Scenes/HDRPLabController/Prefabs/LabCoupe_Env.prefab");
            var carRoot=presentation!=null?presentation.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="LabCoupe_MD"):null;
            if(carRoot!=null)go=CopyCarGeometry(carRoot,parent);
        }
        if(go==null)go = PrefabUtility.InstantiatePrefab(asset, parent) as GameObject;
        if (go == null) go = UnityEngine.Object.Instantiate(asset, parent);
        if (go == null) return null;
        if (PrefabUtility.IsPartOfPrefabInstance(go)) PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = name;
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        if (stripPhysics)
        {
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
            foreach (Rigidbody r in go.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(r);
        }
        // Imported demo prefabs may bundle their own presentation camera/listener.
        // Gameplay supplies one shared camera, so retaining these produces duplicate
        // audio listener warnings and can unexpectedly take over the Game view.
        foreach (AudioListener listener in go.GetComponentsInChildren<AudioListener>(true)) UnityEngine.Object.DestroyImmediate(listener);
        foreach (Camera camera in go.GetComponentsInChildren<Camera>(true)) UnityEngine.Object.DestroyImmediate(camera);
        string sourcePath = AssetDatabase.GetAssetPath(asset).ToLowerInvariant();
        if(sourcePath.Contains("assets/hdrplab/"))MapVehicleMaterials(go);
        if (sourcePath.Contains("soldier") || sourcePath.Contains("starsparrow") || sourcePath.Contains("star sparrow") ||
            sourcePath.Contains("assets/hdrplab/") || sourcePath.Contains("hdrp lab car") || sourcePath.Contains("hdrplabcar") || sourcePath.Contains("lab car"))
            ConvertRenderersToBuiltIn(go);
        if (sourcePath.Contains("assets/hdrplab/") || sourcePath.Contains("starsparrow") || sourcePath.Contains("star sparrow"))
            AddDistanceCulling(go);
        return go;
    }

    static GameObject CopyCarGeometry(Transform source,Transform parent)
    {
        // Recreate only transforms and mesh art. No studio cameras, scripts, physics,
        // missing MonoBehaviours or static flags are ever instantiated from the demo.
        var go=new GameObject(source.name);go.transform.SetParent(parent,false);
        go.transform.localPosition=source.localPosition;go.transform.localRotation=source.localRotation;go.transform.localScale=source.localScale;
        var filter=source.GetComponent<MeshFilter>();var renderer=source.GetComponent<MeshRenderer>();
        if(filter!=null && renderer!=null)
        {go.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;go.AddComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;}
        foreach(Transform child in source)CopyCarGeometry(child,go.transform);
        go.SetActive(source.gameObject.activeSelf);return go;
    }

    static void AddDistanceCulling(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        LODGroup group = root.GetComponent<LODGroup>();
        if (group == null) group = root.AddComponent<LODGroup>();
        group.fadeMode = LODFadeMode.CrossFade;
        group.animateCrossFading = true;
        group.SetLODs(new[] { new LOD(0.018f, renderers) });
        group.RecalculateBounds();
    }

    public static void MapVehicleMaterials(GameObject root)
    {
        var art=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HDRPLab/Scenes/HDRPLabController/Prefabs/LabCoupe_Env.prefab");
        if(art==null)return;
        var mapping=new Dictionary<Mesh,Material[]>();
        foreach(var f in art.GetComponentsInChildren<MeshFilter>(true))
        {var r=f.GetComponent<Renderer>();if(f.sharedMesh!=null && r!=null && !mapping.ContainsKey(f.sharedMesh))mapping.Add(f.sharedMesh,r.sharedMaterials);}
        int mapped=0;
        foreach(var f in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var r=f.GetComponent<Renderer>();
            if(r!=null && f.sharedMesh!=null && mapping.TryGetValue(f.sharedMesh,out var materials)){r.sharedMaterials=materials;mapped++;}
        }
        foreach(var r in root.GetComponentsInChildren<Renderer>(true))
            if(r.name.EndsWith("_LOD1") && r.transform.parent.GetComponent<Renderer>()!=null)r.sharedMaterials=r.transform.parent.GetComponent<Renderer>().sharedMaterials;
        Debug.Log("Lab Coupe material bindings restored: "+mapped);
    }

    public static void ConvertRenderersToBuiltIn(GameObject root)
    {
        Shader standard = Shader.Find("Standard");
        if (standard == null) return;
        Dictionary<Material, Material> converted = new Dictionary<Material, Material>();
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material source = materials[i];
                if (source == null) continue;
                // Sparrow includes a Standard variant with explicit texture bindings.
                if(source.name.StartsWith("StarSparrow_"))
                {
                    var legacy=AssetDatabase.LoadAssetAtPath<Material>("Assets/StarSparrow/Legacy/Standard/Materials/"+source.name+"_Standard.mat");
                    if(legacy!=null)source=legacy;
                }
                if (!converted.TryGetValue(source, out Material target))
                {
                    target = new Material(standard) { name = source.name + "_BuiltIn" };
                    if (source.HasProperty("_BaseColor")) target.color = source.GetColor("_BaseColor");
                    else if (source.HasProperty("_Color")) target.color = source.GetColor("_Color");
                    Texture texture = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
                    // If the package shader is unavailable in this Built-in project,
                    // HasProperty returns false even though the serialized material
                    // still references its texture. Use the package's known textures.
                    string sourceName = source.name.ToLowerInvariant();
                    if (texture == null && sourceName.Contains("demo_soldier"))
                        texture = AssetDatabase.LoadAssetAtPath<Texture>("Assets/LowPolySoldiers_demo/models/Materials/demo_soldier_512.tga");
                    else if (texture == null && sourceName.Contains("demo_weapon"))
                        texture = AssetDatabase.LoadAssetAtPath<Texture>("Assets/LowPolySoldiers_demo/models/Materials/demo_weapon.tga");
                    if (texture != null) target.mainTexture = texture;
                    Texture normal = source.HasProperty("_BumpMap") ? source.GetTexture("_BumpMap") :
                        source.HasProperty("_NormalMap") ? source.GetTexture("_NormalMap") : null;
                    if (normal != null)
                    {
                        target.SetTexture("_BumpMap", normal);
                        target.EnableKeyword("_NORMALMAP");
                    }
                    Texture emission = source.HasProperty("_EmissionMap") ? source.GetTexture("_EmissionMap") : null;
                    if (emission != null)
                    {
                        target.SetTexture("_EmissionMap", emission);
                        target.SetColor("_EmissionColor", Color.white);
                        target.EnableKeyword("_EMISSION");
                    }
                    if (source.HasProperty("_Metallic")) target.SetFloat("_Metallic", source.GetFloat("_Metallic"));
                    if (source.HasProperty("_Smoothness")) target.SetFloat("_Glossiness", source.GetFloat("_Smoothness"));
                    if(source.name.StartsWith("Lab_"))
                    {
                        var properties=new SerializedObject(source);
                        var colors=properties.FindProperty("m_SavedProperties.m_Colors");
                        if(colors!=null)for(int c=0;c<colors.arraySize;c++)
                        {
                            var entry=colors.GetArrayElementAtIndex(c);
                            if(!entry.FindPropertyRelative("first").stringValue.StartsWith("Color_"))continue;
                            Color tint=entry.FindPropertyRelative("second").colorValue;tint.a=1;target.color=tint;break;
                        }
                        string n=source.name.ToLowerInvariant();
                        if(n.Contains("rubber") || n.Contains("black"))target.color=new Color(0.035f,0.04f,0.045f);
                        if(n.Contains("chrome") || n.Contains("metal") || n.Contains("aluminum"))target.SetFloat("_Metallic",0.85f);
                        if(n.Contains("pearl")){target.SetFloat("_Metallic",0.6f);target.SetFloat("_Glossiness",0.78f);}
                        if(n.Contains("glass")){target.color=new Color(0.12f,0.2f,0.25f,0.45f);target.SetFloat("_Glossiness",0.9f);}
                        var textures=properties.FindProperty("m_SavedProperties.m_TexEnvs");
                        if(textures!=null)for(int t=0;t<textures.arraySize;t++)
                        {
                            var entry=textures.GetArrayElementAtIndex(t);var tex=entry.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
                            if(tex==null)continue;string tn=tex.name.ToLowerInvariant();
                            if(tn.Contains("albedo") || tn.Contains("basecolor") || tn.Contains("diffuse")){target.mainTexture=tex;break;}
                        }
                    }
                    converted[source] = target;
                }
                materials[i] = target;
            }
            renderer.sharedMaterials = materials;
        }
    }

    public static RuntimeAnimatorController GetCharacterController(GameObject asset)
    {
        if (asset == null) return null;
        string sourcePath = AssetDatabase.GetAssetPath(asset);
        if (characterControllers.TryGetValue(sourcePath, out RuntimeAnimatorController cached) && cached != null) return cached;
        List<AnimationClip> clipList = AssetDatabase.LoadAllAssetsAtPath(sourcePath)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Low Poly Soldiers ships its motions as separate FBX files next to the model.
        // Collect those clips as well so the imported soldier gets a real locomotion
        // controller instead of silently falling back to a static pose.
        if (sourcePath.IndexOf("soldier", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/LowPolySoldiers_demo/animation" }))
            {
                string animationPath = AssetDatabase.GUIDToAssetPath(guid);
                clipList.AddRange(AssetDatabase.LoadAllAssetsAtPath(animationPath)
                    .OfType<AnimationClip>()
                    .Where(c => !c.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase)));
            }
        }
        AnimationClip[] clips = clipList.Distinct().ToArray();
        if (clips.Length == 0) return null;

        AnimationClip idle = FindClip(clips, "idle") ?? clips[0];
        AnimationClip walk = FindClip(clips, "walk") ?? FindClip(clips, "run") ?? idle;
        AnimationClip run = FindClip(clips, "run") ?? walk;
        AnimationClip jump = FindClip(clips, "jump") ?? idle;
        string safeName = new string(asset.name.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
        string controllerPath = "Assets/Generated/" + safeName + "_Locomotion.controller";
        RuntimeAnimatorController existing = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
        if (existing != null)
        {
            characterControllers[sourcePath] = existing;
            return existing;
        }
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState idleState = sm.AddState("Idle"); idleState.motion = idle;
        AnimatorState walkState = sm.AddState("Walk"); walkState.motion = walk;
        if(FindClip(clips,"walk")==null && walk!=idle)walkState.speed=0.55f;
        AnimatorState runState = sm.AddState("Run"); runState.motion = run;
        AnimatorState jumpState = sm.AddState("Jump"); jumpState.motion = jump;
        sm.defaultState = idleState;
        AddTransition(idleState, walkState, AnimatorConditionMode.Greater, 0.08f, "Speed");
        AddTransition(walkState, idleState, AnimatorConditionMode.Less, 0.08f, "Speed");
        AddTransition(walkState, runState, AnimatorConditionMode.Greater, 0.70f, "Speed");
        AddTransition(runState, walkState, AnimatorConditionMode.Less, 0.70f, "Speed");
        AnimatorStateTransition jumpTransition = sm.AddAnyStateTransition(jumpState);
        jumpTransition.hasExitTime = false;
        jumpTransition.duration = 0.08f;
        jumpTransition.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
        AnimatorStateTransition land = jumpState.AddTransition(idleState);
        land.hasExitTime = true;
        land.exitTime = 0.88f;
        land.duration = 0.12f;
        characterControllers[sourcePath] = controller;
        return controller;
    }

    static AnimationClip FindClip(AnimationClip[] clips, string token)
    {
        return clips.FirstOrDefault(c => c.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    static void AddTransition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float threshold, string parameter)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.12f;
        transition.AddCondition(mode, threshold, parameter);
    }

    public static bool NormalizeHeight(GameObject go, float targetHeight, float yOffset = 0f)
    {
        if (go == null) return false;
        Renderer[] rr = go.GetComponentsInChildren<Renderer>(true);
        if (rr.Length == 0) return false;
        Bounds b = rr[0].bounds;
        for (int i = 1; i < rr.Length; i++) b.Encapsulate(rr[i].bounds);
        if (b.size.y < 0.01f) return false;
        float s = targetHeight / b.size.y;
        go.transform.localScale *= s;
        rr = go.GetComponentsInChildren<Renderer>(true);
        b = rr[0].bounds;
        for (int i = 1; i < rr.Length; i++) b.Encapsulate(rr[i].bounds);
        Vector3 origin = go.transform.parent.position;
        go.transform.position += new Vector3(origin.x - b.center.x, origin.y + yOffset - b.min.y, origin.z - b.center.z);
        return true;
    }

    public static bool NormalizeLength(GameObject go, float targetLength, float yOffset = 0f)
    {
        if (go == null) return false;
        Renderer[] rr = go.GetComponentsInChildren<Renderer>(true);
        if (rr.Length == 0) return false;
        Bounds b = rr[0].bounds;
        for (int i = 1; i < rr.Length; i++) b.Encapsulate(rr[i].bounds);
        float length = Mathf.Max(b.size.x, b.size.z);
        if (length < 0.01f) return false;
        float s = targetLength / length;
        go.transform.localScale *= s;
        rr = go.GetComponentsInChildren<Renderer>(true);
        b = rr[0].bounds;
        for (int i = 1; i < rr.Length; i++) b.Encapsulate(rr[i].bounds);
        Vector3 origin = go.transform.parent.position;
        go.transform.position += new Vector3(origin.x - b.center.x, origin.y + yOffset - b.min.y, origin.z - b.center.z);
        return true;
    }

    static void EnsureCharacters()
    {
        if (characters != null) return;
        characters = Scan(go =>
        {
            string p = AssetDatabase.GetAssetPath(go).ToLowerInvariant();
            string n = go.name.ToLowerInvariant();
            int score = 0;
            if (p.Contains("low poly soldiers") || p.Contains("lowpolysoldiers") || p.Contains("soldier")) score += 500;
            if (p.Contains("thirdparty/kenney/blockycharacters/models/fbx format")) score += 300;
            if (p.Contains("hodaart")) score += 120;
            if (p.Contains("character collection 3") || p.Contains("charactercollection3")) score += 120;
            if (p.Contains("character")) score += 30;
            if (n.Contains("character") || n.Contains("male") || n.Contains("female") || n.Contains("human")) score += 30;
            if (go.GetComponentInChildren<Animator>(true) != null) score += 80;
            if (go.GetComponentInChildren<SkinnedMeshRenderer>(true) != null) score += 80;
            return score;
        }, 90);
    }

    static void EnsureVehicles()
    {
        if (vehicles != null) return;
        vehicles = Scan(go =>
        {
            string p = AssetDatabase.GetAssetPath(go).ToLowerInvariant();
            string n = go.name.ToLowerInvariant();
            int score = 0;
            if (p.Contains("assets/hdrplab/") && (n.Contains("labcoupe") || n.Contains("coupe"))) score += 900;
            if (p.Contains("hdrp lab car") || p.Contains("hdrplabcar") || p.Contains("lab car")) score += 600;
            if (p.Contains("thirdparty/kenney/carkit/models/fbx format")) score += 260;
            if (n.Contains("debris") || n.Contains("cone") || n.Contains("box") || n.Contains("wheel") || n.Contains("tire")) score -= 300;
            if (p.Contains("road vehicles") || p.Contains("vehicle pack")) score += 130;
            if (p.Contains("vehicle") || p.Contains("car")) score += 45;
            if (n.Contains("car") || n.Contains("sedan") || n.Contains("suv") || n.Contains("truck") || n.Contains("taxi") || n.Contains("police")) score += 55;
            if (go.GetComponentsInChildren<Renderer>(true).Length > 1) score += 15;
            if (go.GetComponentInChildren<SkinnedMeshRenderer>(true) != null) score -= 80;
            return score;
        }, 90);
    }

    static void EnsureEnvironments()
    {
        if (environments != null) return;
        environments = Scan(go =>
        {
            string p = AssetDatabase.GetAssetPath(go).ToLowerInvariant();
            string n = go.name.ToLowerInvariant();
            int score = 0;
            if (p.Contains("thirdparty/kenney/citysuburban/models/fbx format")) score += 260;
            if (p.Contains("thirdparty/kenney/citycommercial/models/fbx format")) score += 285;
            if (p.Contains("thirdparty/kenney/cityroads/models/fbx format") && (n.Contains("light") || n.Contains("traffic") || n.Contains("bench") || n.Contains("dumpster"))) score += 120;
            if (p.Contains("urban city") || p.Contains("simple urban")) score += 130;
            if (p.Contains("environment") || p.Contains("building") || p.Contains("urban")) score += 35;
            if (n.Contains("building") || n.Contains("house") || n.Contains("shop") || n.Contains("office")) score += 50;
            if (n.Contains("road") || n.Contains("street") || n.Contains("sidewalk")) score += 20;
            if (n.Contains("car") || n.Contains("vehicle") || n.Contains("character")) score -= 80;
            return score;
        }, 85);
    }

    static void EnsureWeapons()
    {
        if (weapons != null) return;
        weapons = Scan(go =>
        {
            string p = AssetDatabase.GetAssetPath(go).ToLowerInvariant();
            string n = go.name.ToLowerInvariant();
            int score = 0;
            if (p.Contains("thirdparty/kenney/blasterkit/models/fbx format")) score += 300;
            if (n.Contains("blaster")) score += 80;
            if (n.Contains("bullet") || n.Contains("crate") || n.Contains("target") || n.Contains("grenade")) score -= 300;
            return score;
        }, 200);
    }

    static void EnsureAircraft()
    {
        if (aircraft != null) return;
        aircraft = Scan(go =>
        {
            string p = AssetDatabase.GetAssetPath(go).ToLowerInvariant();
            string n = go.name.ToLowerInvariant();
            int score = 0;
            if (p.Contains("star sparrow") || p.Contains("starsparrow")) score += 700;
            if (p.Contains("spaceship") || p.Contains("aircraft")) score += 120;
            if (n.Contains("sparrow") || n.Contains("spaceship") || n.Contains("fighter") || n.Contains("plane")) score += 90;
            if (n.Contains("part") || n.Contains("weapon") || n.Contains("wing") || n.Contains("engine")) score -= 120;
            return score;
        }, 100);
    }

    static List<GameObject> Scan(Func<GameObject, int> scorer, int minScore)
    {
        List<(GameObject go, int score)> scored = new List<(GameObject, int)>();
        HashSet<string> paths = new HashSet<string>();
        foreach (string filter in new[] { "t:Prefab", "t:Model" })
        {
            foreach (string guid in AssetDatabase.FindAssets(filter, new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!paths.Add(path)) continue;
                string lower = path.ToLowerInvariant();
                if (lower.Contains("assets/generated/")) continue;
                if (lower.Contains("assets/thirdparty/quaternius/")) continue;
                if (lower.Contains("thirdparty/kenney") && !lower.Contains("fbx format")) continue;
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                int s = scorer(go);
                if (s >= minScore) scored.Add((go, s));
            }
        }
        return scored.OrderByDescending(x => x.score).Select(x => x.go).Take(64).ToList();
    }
}
#endif
