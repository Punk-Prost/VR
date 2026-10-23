#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ExpansionValidation
{
    public static void RebuildAndTest()
    {
        MiniGTABuilder.BuildEnhancedCity();
        Debug.Log("EXPANSION_SCENE_READY");
        EditorApplication.EnterPlaymode();
    }
    public static void TestExisting()
    {EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");EditorApplication.EnterPlaymode();}
    public static void BuildPlayer()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        MiniGTAVisualValidation.BuildSmokePlayer();
    }
    public static void RefreshHeroAndBuild()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        var hero=GameObject.Find("PlayerCar");
        foreach(Transform child in hero.transform)if(child.name.StartsWith("CarModel_"))
        {
            AssetStoreAssetResolver.MapVehicleMaterials(child.gameObject);
            AssetStoreAssetResolver.ConvertRenderersToBuiltIn(child.gameObject);
            foreach(var renderer in child.GetComponentsInChildren<Renderer>(true))
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)materials[i]=CityUpgradePipeline.Convert(materials[i]);
                renderer.sharedMaterials=materials;
            }
        }
        EditorSceneManager.MarkSceneDirty(hero.scene);
        EditorSceneManager.SaveScene(hero.scene);AssetDatabase.SaveAssets();
        MiniGTAVisualValidation.BuildSmokePlayer();
    }
    public static void RepairHeroAssemblyAndBuild()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        var hero=GameObject.Find("PlayerCar");var car=hero.GetComponent<DriveableVehicle>();
        var remove=new System.Collections.Generic.List<GameObject>();
        foreach(Transform child in hero.transform)if(child.name.StartsWith("CarModel_") || child.name=="TailL" || child.name=="TailR")remove.Add(child.gameObject);
        foreach(var old in remove)UnityEngine.Object.DestroyImmediate(old);
        var model=AssetStoreAssetResolver.InstantiateAsset(AssetStoreAssetResolver.GetHeroVehicle(),hero.transform,"CarModel_LabCoupe_MD");
        MiniGTABuilder.NormalizeCarModel(model,hero.transform,4.2f);
        string[] names={"Lab_Tire_FL","Lab_Tire_FR","Lab_Tire_RL","Lab_Tire_RR"};
        car.wheels=new Transform[4];var rig=new[]{car.frontLeftWheel,car.frontRightWheel,car.rearLeftWheel,car.rearRightWheel};
        for(int i=0;i<4;i++)
        {
            foreach(var child in model.GetComponentsInChildren<Transform>(true))if(child.name==names[i])car.wheels[i]=child;
            if(car.wheels[i]==null)throw new InvalidOperationException("Missing assembled wheel "+names[i]);
            rig[i].transform.position=car.wheels[i].position;
            var renderers=car.wheels[i].GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
            foreach(var r in renderers)bounds.Encapsulate(r.bounds);rig[i].radius=Mathf.Clamp(bounds.size.y*0.5f,0.25f,0.5f);
        }
        foreach(var r in model.GetComponentsInChildren<Renderer>(true))
        {var materials=r.sharedMaterials;for(int i=0;i<materials.Length;i++)materials[i]=CityUpgradePipeline.Convert(materials[i]);r.sharedMaterials=materials;}
        CityLOD.Add(model);
        EditorSceneManager.MarkSceneDirty(hero.scene);EditorSceneManager.SaveScene(hero.scene);AssetDatabase.SaveAssets();
        MiniGTAVisualValidation.BuildSmokePlayer();
    }
    public static void FinalizeAndBake()
    {
        MiniGTABuilder.BuildEnhancedCity();
        // Sidewalks must end at junctions, including the newly added ring road.
        foreach(var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>())
        {
            if(!renderer.name.StartsWith("Sidewalk"))continue;
            foreach(float x in RoadNavigation.Coordinates)foreach(float z in RoadNavigation.Coordinates)
                if(renderer.bounds.Intersects(new Bounds(new Vector3(x,0.15f,z),new Vector3(13.8f,0.3f,13.8f))))
                    throw new InvalidOperationException("Sidewalk blocks junction: "+renderer.name+" at "+x+","+z);
        }
        Debug.Log("JUNCTION_GEOMETRY_VALID");
        CityLightingBake.BakeGarage();
    }
    public static void InspectAndBuild()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        var hero=GameObject.Find("PlayerCar");
        foreach(var renderer in hero.GetComponentsInChildren<Renderer>(true))
        {
            if(renderer.sharedMaterial==null)continue;
            Debug.Log("HERO_ART "+renderer.name+" static="+renderer.gameObject.isStatic+" mat="+renderer.sharedMaterial.name+" color="+renderer.sharedMaterial.color);
        }
        var camera=Camera.main;
        camera.transform.position=hero.transform.position+new Vector3(4,2.5f,-5);
        camera.transform.LookAt(hero.transform.position+Vector3.up*0.8f);
        var target=new RenderTexture(1280,720,24);camera.targetTexture=target;camera.Render();RenderTexture.active=target;
        var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
        System.IO.File.WriteAllBytes("Logs/HeroArt.png",image.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;
        UnityEngine.Object.DestroyImmediate(image);target.Release();UnityEngine.Object.DestroyImmediate(target);
        // Discard the diagnostic camera pose, preserve the user's saved scene.
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");MiniGTAVisualValidation.BuildSmokePlayer();
    }
}
#endif
