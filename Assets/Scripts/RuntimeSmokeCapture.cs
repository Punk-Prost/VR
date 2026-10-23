using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class RuntimeSmokeCapture : MonoBehaviour
{
    [Serializable] class Report {public bool passed;public List<string> checks=new List<string>();public List<string> errors=new List<string>();public float seconds;}
    Report report=new Report();
    float started;
    string folder;
    void Record(string name,bool success)
    {report.checks.Add((success?"PASS ":"FAIL ")+name);if(!success)report.errors.Add(name);Debug.Log((success?"TEST_PASS ":"TEST_FAIL ")+name);}
    void Log(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)report.errors.Add(message);}
    IEnumerator Start()
    {
        if(!GameSession.IsTest){enabled=false;yield break;}
        Application.logMessageReceived+=Log;started=Time.realtimeSinceStartup;
        folder=Path.GetFullPath(Path.Combine(Application.dataPath,Application.isEditor?"../Logs":".."));Directory.CreateDirectory(folder);
        var steps=Tests();
        while(true)
        {
            bool more=false;try{more=steps.MoveNext();}catch(Exception e){report.errors.Add(e.ToString());}
            if(!more)break;yield return steps.Current;
        }
        report.passed=report.errors.Count==0;report.seconds=Time.realtimeSinceStartup-started;
        File.WriteAllText(Path.Combine(folder,"ExpansionRuntimeReport.json"),JsonUtility.ToJson(report,true));
        Debug.Log("EXPANSION_TEST_RESULT "+(report.passed?"PASS":"FAIL")+" checks="+report.checks.Count+" errors="+report.errors.Count);
        Application.logMessageReceived-=Log;
#if UNITY_EDITOR
        if(Application.isBatchMode)UnityEditor.EditorApplication.Exit(report.passed?0:1);
        else UnityEditor.EditorApplication.isPlaying=false;
#else
        Application.Quit(report.passed?0:1);
#endif
    }
    IEnumerator Tests()
    {
        yield return new WaitForSeconds(2f);
        var player=FindAnyObjectByType<VehicleInteractor>();var economy=EconomySystem.Instance;
        var mission=FindAnyObjectByType<MissionSystem>();var inventory=player.GetComponent<WeaponInventory>();
        var cars=FindObjectsByType<DriveableVehicle>();var npcs=FindObjectsByType<NPCWalker>();
        Record("URP active",UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset);
        Record("Player, campaign, settings and shops",player!=null && mission!=null && FindAnyObjectByType<GameSession>()!=null && FindObjectsByType<CityShop>().Length==2);
        Record("64 civilians and 54 cars",npcs.Length==64 && cars.Length==54);
        bool rigs=true;foreach(var car in cars)rigs &= car.frontLeftWheel!=null && car.frontRightWheel!=null && car.rearLeftWheel!=null && car.rearRightWheel!=null;
        Record("Four WheelColliders on every car",rigs);
        bool controllers=true;foreach(var npc in npcs)controllers &= npc.animatorBridge!=null && npc.animatorBridge.animator.runtimeAnimatorController!=null;
        Record("Every civilian has animation controller",controllers);
        int lods=0;foreach(var group in FindObjectsByType<LODGroup>())if(group.GetLODs().Length>=2)lods++;
        Record("Multi-level mesh LODs",lods>20);
        var npcPositions=new Vector3[npcs.Length];for(int i=0;i<npcs.Length;i++)npcPositions[i]=npcs[i].transform.position;
        yield return new WaitForSeconds(1f);
        int moved=0;for(int i=0;i<npcs.Length;i++)if(Vector3.Distance(npcPositions[i],npcs[i].transform.position)>0.15f)moved++;
        Record("Civilian movement (at least 40)",moved>=40);
        int money=economy.Money;
        Record("Unaffordable purchase rejected",!economy.TrySpend(money+1) && economy.Money==money);
        Record("Weapon purchase unlocks and charges",inventory.Buy(1,economy) && inventory.weapons[1].unlocked && economy.Money==money-600);
        economy.Restore(4321);mission.Save(false);economy.Restore(1);mission.Load();
        Record("Save roundtrip money and inventory",economy.Money==4321 && inventory.weapons[1].unlocked && File.Exists(mission.SaveFilePath));
        for(int job=0;job<3;job++)
        {
            if(job>0)
            {
                DriveableVehicle jobCar=null;
                foreach(var candidate in cars)if(job==2 || MissionSystem.HasModel(candidate,"taxi")){jobCar=candidate;break;}
                if(jobCar!=null)player.EnterVehicle(jobCar);
            }
            TeleportPlayer(player,mission.Destination.Value);mission.TryInteract();
            Record("Contract "+job+" starts",mission.JobActive);
            TeleportPlayer(player,mission.Destination.Value);mission.TryInteract();
            Record("Contract "+job+" rewards once",mission.CompletedJobs==job+1 && !mission.JobActive);
            if(player.ActiveVehicle!=null)player.ExitVehicle();
        }
        TeleportPlayer(player,mission.Destination.Value);mission.TryInteract();
        var mailPlane=FindAnyObjectByType<PlayerAircraft>();Vector3 planeHome=mailPlane.transform.position;
        Record("Air delivery starts",mission.CompletedJobs==3 && mission.JobActive);
        player.EnterAircraft(mailPlane);mailPlane.GetComponent<Rigidbody>().position=mission.Destination.Value+Vector3.up*25;Physics.SyncTransforms();
        yield return new WaitForSeconds(0.25f);Record("Air delivery completes in flight",mission.CompletedJobs==4);
        player.ExitAircraft();mailPlane.transform.position=planeHome;mailPlane.GetComponent<Rigidbody>().linearVelocity=Vector3.zero;
        mission.Load();
        var traffic=FindAnyObjectByType<TrafficCarAI>();var driven=traffic.GetComponent<DriveableVehicle>();
        player.EnterVehicle(driven);
        Record("Takeover disables AI and enables physics",player.ActiveVehicle==driven && !traffic.enabled && !driven.GetComponent<Rigidbody>().isKinematic);
        player.ExitVehicle();
        Record("Exit restores player controller",player.playerController.enabled && player.characterController.enabled && !player.InVehicle);
        var damage=driven.GetComponent<VehicleDamage>();damage.ApplyDamage(200f);
        Record("Destroyed vehicle loses power",damage.Health<=0 && driven.PerformanceMultiplier==0);
        damage.RepairFull();Record("Full repair restores power and health",damage.Health==damage.maxHealth && driven.PerformanceMultiplier==1 && driven.enabled);
        var blocker=new GameObject("Traffic sensor regression blocker");blocker.transform.SetParent(driven.transform.parent);
        blocker.transform.position=driven.transform.position+driven.transform.forward*4.5f+Vector3.up*0.8f;blocker.transform.rotation=driven.transform.rotation;
        blocker.AddComponent<BoxCollider>().size=new Vector3(2,1.2f,1);Physics.SyncTransforms();
        float obstruction=(float)typeof(TrafficCarAI).GetMethod("CheckObstacleFactor",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(traffic,null);
        Record("Sensor detects sibling objects under CITY",obstruction<0.6f);Destroy(blocker);
        var hero=GameObject.Find("PlayerCar").GetComponent<DriveableVehicle>();
        var heroBody=hero.GetComponent<Rigidbody>();heroBody.position=new Vector3(3.5f,0.4f,-120);heroBody.rotation=Quaternion.identity;Physics.SyncTransforms();
        player.EnterVehicle(hero);hero.SetTestInput(1f,0f);
        yield return new WaitForFixedUpdate();Vector3 begin=heroBody.position;
        yield return new WaitForSeconds(3f);
        hero.SetTestInput(0f,0f);
        Record("WheelCollider acceleration moves car",Vector3.Dot(heroBody.position-begin,Vector3.forward)>2f && hero.Speed>1f);
        Record("Car stays upright",Vector3.Dot(hero.transform.up,Vector3.up)>0.7f);
        bool assembled=true;
        foreach(var wheel in hero.wheels)
        {
            var parts=wheel.GetComponentsInChildren<Renderer>();
            assembled &= wheel.name.StartsWith("Lab_Tire_") && parts.Length>0;
            foreach(var part in parts)assembled &= Vector3.Distance(part.bounds.center,wheel.position)<1.2f;
        }
        Record("Four assembled wheel meshes rotate around their hubs",assembled);
        player.ExitVehicle();
        var plane=FindAnyObjectByType<PlayerAircraft>();player.EnterAircraft(plane);
        Record("Aircraft takeover",player.ActiveAircraft==plane && plane.IsDriven && !player.playerController.enabled);
        plane.transform.SetPositionAndRotation(new Vector3(200,3,140),Quaternion.Euler(0,180,0));plane.StartTestFlight();
        yield return new WaitForSeconds(2f);
        Record("Aircraft produces thrust and lift",plane.Speed>12f && plane.transform.position.y>3f);
        Record("Airborne exit is blocked",!plane.CanExit);
        player.ExitAircraft();Record("Aircraft exit",!player.InVehicle && player.playerController.enabled);
        var weather=FindAnyObjectByType<WeatherSystem>();weather.SetWeather(true);Record("Rain state",WeatherSystem.IsRaining);weather.SetWeather(false);
        var route=RoadNavigation.FindPath(new Vector3(-120,0,-120),new Vector3(120,0,120));
        bool axis=true;for(int i=1;i<route.Count;i++){Vector3 d=route[i]-route[i-1];axis &= Mathf.Abs(d.x)<0.01f || Mathf.Abs(d.z)<0.01f;}
        Record("Minimap route stays on grid edges",route.Count>2 && axis);
        GameSession.SetModal(true);Record("Menu pauses and gates controls",GameSession.BlockInput && Time.timeScale==0);GameSession.SetModal(false);
        player.characterController.enabled=false;player.transform.position=new Vector3(-18,0.35f,-18);player.characterController.enabled=true;
        yield return new WaitForSeconds(1f);
        if(!Application.isEditor){ScreenCapture.CaptureScreenshot(Path.Combine(folder,"ExpansionHUD.png"));yield return new WaitForSeconds(0.5f);}
        Capture(Camera.main,Path.Combine(folder,"ExpansionGameplay.png"));
        var camera=Camera.main;var follow=camera.GetComponent<ThirdPersonCamera>();follow.enabled=false;
        camera.transform.position=hero.transform.position+new Vector3(4,2.5f,-5);camera.transform.LookAt(hero.transform.position+Vector3.up*0.8f);
        yield return null;Capture(camera,Path.Combine(folder,"ExpansionCar.png"));
        camera.transform.position=new Vector3(-105,110,-110);camera.transform.LookAt(new Vector3(0,0,10));
        yield return null;Capture(camera,Path.Combine(folder,"ExpansionCity.png"));
    }
    void Capture(Camera camera,string path)
    {
        var target=new RenderTexture(1280,720,24);var previous=RenderTexture.active;var old=camera.targetTexture;
        camera.targetTexture=target;camera.Render();RenderTexture.active=target;
        var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
        camera.targetTexture=old;RenderTexture.active=previous;Destroy(image);target.Release();Destroy(target);
    }
    void TeleportPlayer(VehicleInteractor player,Vector3 position)
    {
        bool foot=!player.InVehicle;if(foot)player.characterController.enabled=false;
        player.ActiveTransform.position=position+Vector3.up*0.3f;
        if(foot)player.characterController.enabled=true;Physics.SyncTransforms();
    }
}
