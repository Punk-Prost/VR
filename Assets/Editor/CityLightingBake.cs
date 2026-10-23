#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CityLightingBake
{
    static double started;
    public static void BakeGarage()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        var zone=UnityEngine.Object.FindAnyObjectByType<GarageZone>();
        if(zone==null)throw new InvalidOperationException("Garage missing");
        Transform garage=zone.transform.parent;
        foreach(var renderer in garage.GetComponentsInChildren<MeshRenderer>())
        {
            if(renderer.name.Contains("ServiceRing") || renderer.name.Contains("Stripe"))continue;
            GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);
            renderer.receiveGI=ReceiveGI.Lightmaps;
        }
        var existing=garage.Find("Baked garage light");
        Light lamp=existing!=null?existing.GetComponent<Light>():new GameObject("Baked garage light").AddComponent<Light>();
        lamp.transform.SetParent(garage,false);lamp.transform.localPosition=new Vector3(0,7,12);
        lamp.type=LightType.Point;lamp.range=28;lamp.intensity=5;lamp.color=new Color(1f,0.85f,0.66f);lamp.lightmapBakeType=LightmapBakeType.Baked;
        const string path="Assets/Generated/GarageLighting.lighting";
        var settings=AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
        if(settings==null){settings=new LightingSettings();AssetDatabase.CreateAsset(settings,path);}
        settings.bakedGI=true;settings.realtimeGI=false;settings.lightmapResolution=4f;settings.lightmapMaxSize=512;
        settings.directSampleCount=16;settings.indirectSampleCount=32;settings.environmentSampleCount=16;
        string[] available=Enum.GetNames(typeof(LightingSettings.Lightmapper));
        Debug.Log("LIGHTMAPPERS "+string.Join(",",available));
        foreach(string name in new[]{"UnityComputeCPU","ProgressiveCPU"})
            if(Array.IndexOf(available,name)>=0){settings.lightmapper=(LightingSettings.Lightmapper)Enum.Parse(typeof(LightingSettings.Lightmapper),name);break;}
        Lightmapping.lightingSettings=settings;EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();started=EditorApplication.timeSinceStartup;
        Lightmapping.bakeCompleted+=Complete;EditorApplication.update+=Watch;
        if(!Lightmapping.BakeAsync())throw new InvalidOperationException("Lighting bake did not start");
    }
    static void Watch()
    {
        if(EditorApplication.timeSinceStartup-started<180)return;
        Lightmapping.Cancel();EditorApplication.update-=Watch;Lightmapping.bakeCompleted-=Complete;
        Debug.LogError("GARAGE_BAKE_TIMEOUT");EditorApplication.Exit(1);
    }
    static void Complete()
    {
        Lightmapping.bakeCompleted-=Complete;EditorApplication.update-=Watch;
        EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        bool success=LightmapSettings.lightmaps.Length>0;
        Debug.Log("GARAGE_BAKE_RESULT "+success+" lightmaps="+LightmapSettings.lightmaps.Length);
        if(Application.isBatchMode)EditorApplication.Exit(success?0:1);
    }
}
#endif
