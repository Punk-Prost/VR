#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class CityUpgradePipeline
{
    const string Folder = "Assets/Generated/URP";
    static readonly Dictionary<Material, Material> converted = new Dictionary<Material, Material>();
    static int buildingIndex;

    public static GameObject Civilian(int variant)
    {
        char letter = (char)('a' + Mathf.Abs(variant) % 18);
        return AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/Kenney/BlockyCharacters/Models/FBX format/character-" + letter + ".fbx");
    }

    public static bool PlaceBuilding(string name, Vector3 position, float width, float depth, float height, float yaw, Transform parent)
    {
        string district = position.z > 80f ? "CitySuburban" : "CityCommercial";
        string[] paths = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/ThirdParty/Kenney/" + district })
            .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".fbx") && p.ToLowerInvariant().Contains("building") && !p.Contains("low-detail")).OrderBy(p => p).ToArray();
        if (paths.Length == 0) return false;
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(paths[buildingIndex++ % paths.Length]);
        var root = new GameObject(name + "_Imported");
        root.transform.SetParent(parent);
        root.transform.position = position;
        GameObject model = AssetStoreAssetResolver.InstantiateAsset(asset, root.transform, asset.name);
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { Object.DestroyImmediate(root); return false; }
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        float scale = Mathf.Min(width / Mathf.Max(0.1f,b.size.x), depth / Mathf.Max(0.1f,b.size.z));
        scale = Mathf.Min(scale,height / Mathf.Max(0.1f,b.size.y));
        model.transform.localScale *= scale;
        b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        model.transform.position += position - new Vector3(b.center.x,b.min.y,b.center.z);
        var collider = root.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f,b.size.y * 0.5f,0f);
        collider.size = b.size;
        root.transform.rotation = Quaternion.Euler(0f,yaw,0f);
        foreach (var r in renderers) GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        return true;
    }

    public static void Apply(Scene scene)
    {
        CityInfrastructure.Build();
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Generated", "URP");
        if(!AssetDatabase.IsValidFolder("Assets/Resources"))AssetDatabase.CreateFolder("Assets","Resources");
        var particle=AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/CityParticle.mat");
        if(particle==null)
        {
            particle=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            particle.SetFloat("_Surface",1);particle.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);particle.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            particle.SetFloat("_ZWrite",0);particle.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");particle.renderQueue=3000;
            var texture=new Texture2D(32,32,TextureFormat.RGBA32,false);
            for(int y=0;y<32;y++)for(int x=0;x<32;x++){float d=new Vector2((x-15.5f)/15.5f,(y-15.5f)/15.5f).magnitude;texture.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(1-d)*0.6f));}
            texture.Apply();AssetDatabase.CreateAsset(texture,"Assets/Generated/ParticleSoft.asset");particle.SetTexture("_BaseMap",texture);
            AssetDatabase.CreateAsset(particle,"Assets/Resources/CityParticle.mat");
        }
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Folder + "/CityRenderer.asset");
        if (renderer == null) { renderer = ScriptableObject.CreateInstance<UniversalRendererData>(); AssetDatabase.CreateAsset(renderer, Folder + "/CityRenderer.asset"); }
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Folder + "/CityPipeline.asset");
        if (pipeline == null) { pipeline = UniversalRenderPipelineAsset.Create(renderer); AssetDatabase.CreateAsset(pipeline, Folder + "/CityPipeline.asset"); }
        pipeline.shadowDistance = 80f;
        pipeline.shadowCascadeCount = 2;
        pipeline.msaaSampleCount = 2;
        pipeline.renderScale = 1f;
        pipeline.supportsCameraDepthTexture = true;
        pipeline.supportsCameraOpaqueTexture = false;
        GraphicsSettings.defaultRenderPipeline = pipeline;
        int originalQuality = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i,false); QualitySettings.renderPipeline = pipeline; }
        QualitySettings.SetQualityLevel(originalQuality,false);
        QualitySettings.renderPipeline = pipeline;
        EditorUtility.SetDirty(pipeline);
        converted.Clear();
        foreach (var root in scene.GetRootGameObjects())
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r is SpriteRenderer || r.GetComponent<TextMesh>() != null) continue;
            Material[] materials = r.sharedMaterials;
            for (int i=0;i<materials.Length;i++) materials[i] = Convert(materials[i]);
            r.sharedMaterials = materials;
        }
        foreach(var car in Object.FindObjectsByType<DriveableVehicle>())
        {
            foreach(Transform child in car.transform)if(child.name.StartsWith("CarModel_"))CityLOD.Add(child.gameObject);
        }
        var buildings=GameObject.Find("Buildings");
        if(buildings!=null)foreach(Transform child in buildings.transform)if(child.name.EndsWith("_Imported"))CityLOD.Add(child.gameObject);
        foreach (var light in Object.FindObjectsByType<Light>())
            if (light.type != LightType.Directional) light.shadows = LightShadows.None;
        // Convert serialized material references too: lights replace sharedMaterials at runtime.
        foreach(var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
        {
            if(behaviour==null)continue;var serialized=new SerializedObject(behaviour);var property=serialized.GetIterator();
            while(property.Next(true))
                if(property.propertyType==SerializedPropertyType.ObjectReference && property.objectReferenceValue is Material material)
                    property.objectReferenceValue=Convert(material);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach (Camera camera in Object.FindObjectsByType<Camera>())
        {
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            camera.farClipPlane = 380f;
        }
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Folder + "/CityVolume.asset");
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, Folder + "/CityVolume.asset");
            var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(0.18f); bloom.threshold.Override(1.1f);
            var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
            var color = profile.Add<ColorAdjustments>(true); color.contrast.Override(8f); color.saturation.Override(-6f);
            var vignette = profile.Add<Vignette>(true); vignette.intensity.Override(0.16f);
            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);
        }
        var volume = new GameObject("City Post Processing").AddComponent<Volume>();
        volume.isGlobal = true; volume.sharedProfile = profile;
        // One low-resolution realtime probe, updated on demand (not every frame).
        var probe = new GameObject("Central Reflection Probe").AddComponent<ReflectionProbe>();
        probe.transform.position = new Vector3(0f,12f,0f); probe.size = new Vector3(500f,100f,500f);
        probe.mode = ReflectionProbeMode.Realtime; probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces; probe.resolution = 128;
        probe.cullingMask = 1; probe.boxProjection = true;
        var weather = Object.FindAnyObjectByType<WeatherSystem>();
        weather.gameObject.AddComponent<GameSession>();
        var weaponShop = new GameObject("Weapon Shop").AddComponent<CityShop>();
        weaponShop.transform.position = new Vector3(-40f,0.3f,-40f);
        var garageShop = new GameObject("Car Dealership").AddComponent<CityShop>();
        garageShop.transform.position = new Vector3(120f,0.3f,-40f); garageShop.garage = true;
        var missions = Object.FindAnyObjectByType<MissionSystem>();
        if(missions!=null && missions.missionMarkers.Length>=8)
        {
            Vector3[] stops = {new Vector3(-18,0.3f,-12),new Vector3(-40,0.3f,40),new Vector3(3.5f,0.3f,-30),new Vector3(83.5f,0.3f,40),new Vector3(83.5f,0.3f,-30),new Vector3(112,0.3f,-34),new Vector3(205,0.3f,125),new Vector3(-200,0.3f,200)};
            for(int i=0;i<8;i++) missions.missionMarkers[i].transform.position=stops[i];
        }
        var asphalt = AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/Materials/Asphalt.mat");
        if (weather != null) weather.wettableMaterials = new[] { Convert(asphalt) };
        AssetDatabase.SaveAssets();
    }

    public static Material Convert(Material source)
    {
        if (source == null) return null;
        if (source.shader != null && (source.shader.name.StartsWith("Universal Render Pipeline/") || source.shader.name.StartsWith("Skybox/"))) return source;
        if (converted.TryGetValue(source, out var cached)) return cached;
        string key = AssetDatabase.GetAssetPath(source);
        if (string.IsNullOrEmpty(key)) key = source.name;
        string file = Folder + "/Material_" + Hash128.Compute(key + ":" + source.name) + ".mat";
        var target = AssetDatabase.LoadAssetAtPath<Material>(file);
        if (target == null) { target = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(target, file); }
        target.name = source.name + "_URP";
        target.color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
        Texture tex = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
        target.SetTexture("_BaseMap",tex);
        target.SetFloat("_Smoothness",source.HasProperty("_Glossiness") ? source.GetFloat("_Glossiness") : 0.25f);
        target.SetFloat("_Metallic",source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : 0f);
        if(source.name.StartsWith("Lab_") && source.name.ToLowerInvariant().Contains("glass"))
        {
            target.SetFloat("_Surface",1);target.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);target.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            target.SetFloat("_ZWrite",0);target.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");target.renderQueue=3000;
        }
        if (source.HasProperty("_BumpMap") && source.GetTexture("_BumpMap") != null)
        { target.SetTexture("_BumpMap",source.GetTexture("_BumpMap")); target.EnableKeyword("_NORMALMAP"); }
        if (source.IsKeywordEnabled("_EMISSION") && source.HasProperty("_EmissionColor"))
        { target.EnableKeyword("_EMISSION"); target.SetColor("_EmissionColor",source.GetColor("_EmissionColor")); }
        converted[source] = target; EditorUtility.SetDirty(target); return target;
    }
}
#endif
