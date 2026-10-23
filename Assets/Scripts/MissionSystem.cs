using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

public class MissionSystem : MonoBehaviour
{
    [Serializable] public class CarSave { public string id; public bool owned,stored; public Vector3 position; public float yaw,health; }
    [Serializable] public class SaveData
    {
        public int version=2, missionIndex, money;
        public Vector3 playerPosition;
        public WeaponInventory.WeaponSlot[] inventory;
        public List<CarSave> cars=new List<CarSave>();
    }
    public VehicleInteractor player;
    public EconomySystem economy;
    public GameObject[] missionMarkers;
    public bool[] requiresVehicle;
    public int[] rewards;
    public float completeRadius=6f;
    public int CompletedJobs {get;private set;}
    public bool JobActive {get;private set;}
    public Vector3? Destination => missionMarkers!=null && MarkerIndex<missionMarkers.Length && missionMarkers[MarkerIndex]!=null ? missionMarkers[MarkerIndex].transform.position : (Vector3?)null;
    public string Objective => CompletedJobs>=4 ? "Контракты выполнены. Исследуй город" : !JobActive ? names[CompletedJobs]+": F у точки старта" : instructions[CompletedJobs];
    static readonly string[] names={"Курьер","Такси","Доставка автомобиля","Воздушная почта"};
    static readonly string[] instructions={"Доставь посылку пешком · F у цели","Довези пассажира на такси · F у цели","Доставь машину с состоянием выше 50% · F","Пролети над целью выше 15 м"};
    int MarkerIndex => CompletedJobs*2+(JobActive?1:0);
    float remaining,saveTimer;
    DriveableVehicle deliveryCar;
    string SavePath=>Path.Combine(Application.persistentDataPath,GameSession.IsTest?"city_test_save.json":"minigta_campaign_v2.json");
    public string SaveFilePath=>SavePath;
    void Start(){if(economy==null)economy=EconomySystem.Instance;if(!GameSession.IsTest)Load();RefreshMarkers();}
    void Update()
    {
        if(player==null)return;
        if(Input.GetKeyDown(KeyCode.F5) && !GameSession.BlockInput)Save();
        saveTimer+=Time.deltaTime;if(saveTimer>60f){saveTimer=0f;Save(false);}
        if(GameSession.BlockInput || CompletedJobs>=4)return;
        if(JobActive)
        {
            remaining-=Time.deltaTime;
            if(remaining<=0f || (deliveryCar!=null && deliveryCar.GetComponent<VehicleDamage>().NormalizedHealth<0.5f))
            {FailJob("Контракт сорван. Вернись к точке старта");return;}
        }
        if(!Destination.HasValue)return;
        Vector3 delta=player.ActiveTransform.position-Destination.Value;float altitude=delta.y;delta.y=0;
        if(CompletedJobs==3 && JobActive && player.ActiveAircraft!=null && delta.magnitude<22f && altitude>15f){CompleteJob();return;}
        if(delta.magnitude>completeRadius || !Input.GetKeyDown(KeyCode.F))return;
        TryInteract();
    }
    public void TryInteract()
    {
        if(GameSession.BlockInput || CompletedJobs>=4 || !Destination.HasValue || player==null)return;
        Vector3 distance=player.ActiveTransform.position-Destination.Value;distance.y=0;
        if(distance.magnitude>completeRadius)return;
        if(!JobActive)
        {
            if(CompletedJobs==1 && (player.ActiveVehicle==null || !HasModel(player.ActiveVehicle,"taxi"))){GameSession.Notify("Для этого заказа нужно такси");return;}
            if(CompletedJobs==2 && player.ActiveVehicle==null){GameSession.Notify("Возьми автомобиль для доставки");return;}
            JobActive=true;remaining=CompletedJobs==3?240f:180f;deliveryCar=CompletedJobs==2?player.ActiveVehicle:null;
            RefreshMarkers();GameSession.Notify("Контракт принят");
        }
        else
        {
            if(CompletedJobs==0 && player.InVehicle){GameSession.Notify("Доставь посылку пешком");return;}
            if(CompletedJobs==1 && (player.ActiveVehicle==null || !HasModel(player.ActiveVehicle,"taxi")))return;
            if(CompletedJobs==2 && player.ActiveVehicle!=deliveryCar)return;
            if(CompletedJobs!=3)CompleteJob();
        }
    }
    public static bool HasModel(DriveableVehicle car,string token)
    {foreach(var t in car.GetComponentsInChildren<Transform>())if(t.name.ToLowerInvariant().Contains(token))return true;return false;}
    public void FailJob(string message){if(!JobActive)return;JobActive=false;deliveryCar=null;RefreshMarkers();GameSession.Notify(message);}
    void CompleteJob()
    {
        int reward=new[]{300,500,850,1200}[CompletedJobs];economy.AddMoney(reward);CompletedJobs++;JobActive=false;deliveryCar=null;
        RefreshMarkers();Save(false);GameSession.Notify("Контракт выполнен: +$"+reward);
    }
    void RefreshMarkers(){if(missionMarkers==null)return;for(int i=0;i<missionMarkers.Length;i++)if(missionMarkers[i]!=null)missionMarkers[i].SetActive(i==MarkerIndex);}
    public void Save()=>Save(true);
    public void Save(bool notify)
    {
        if(player==null || economy==null)return;
        Vector3 position=player.InVehicle?new Vector3(-18f,0.35f,-18f):player.transform.position;
        var data=new SaveData{missionIndex=CompletedJobs,money=economy.Money,playerPosition=position,inventory=player.GetComponent<WeaponInventory>()?.weapons};
        foreach(var car in FindObjectsByType<DriveableVehicle>())
            if(car.purchasePrice>0 || car.StoredInGarage || car.name=="PlayerCar")data.cars.Add(new CarSave{id=car.name,owned=car.owned,stored=car.StoredInGarage,position=car.transform.position,yaw=car.transform.eulerAngles.y,health=car.GetComponent<VehicleDamage>().Health});
        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);string temp=SavePath+".tmp";File.WriteAllText(temp,JsonUtility.ToJson(data,true));
            if(File.Exists(SavePath))File.Replace(temp,SavePath,SavePath+".bak");else File.Move(temp,SavePath);
            economy.Save();if(notify)GameSession.Notify("Прогресс сохранён");
        }
        catch(Exception e){Debug.LogError("Save failed: "+e.Message);GameSession.Notify("Не удалось сохранить прогресс");}
    }
    public void Load()
    {
        SaveData data=null;
        foreach(string path in new[]{SavePath,SavePath+".bak"})
        {
            if(!File.Exists(path))continue;
            try{var candidate=JsonUtility.FromJson<SaveData>(File.ReadAllText(path));if(candidate!=null && candidate.version==2){data=candidate;break;}}
            catch(Exception e){Debug.LogWarning("Cannot read save: "+e.Message);}
        }
        if(data==null)return;
        CompletedJobs=Mathf.Clamp(data.missionIndex,0,4);JobActive=false;economy.Restore(data.money);
        var inventory=player.GetComponent<WeaponInventory>();
        if(data.inventory!=null && inventory!=null && data.inventory.Length==inventory.weapons.Length)
            for(int i=0;i<data.inventory.Length;i++)if(data.inventory[i]!=null)
            {inventory.weapons[i].unlocked=i==0 || data.inventory[i].unlocked;inventory.weapons[i].ammoInMagazine=Mathf.Clamp(data.inventory[i].ammoInMagazine,0,inventory.weapons[i].magazineSize);inventory.weapons[i].reserveAmmo=Mathf.Clamp(data.inventory[i].reserveAmmo,0,9999);}
        Vector3 p=data.playerPosition;
        if(!float.IsNaN(p.x) && !float.IsInfinity(p.x) && Mathf.Abs(p.x)<240f && Mathf.Abs(p.z)<240f && p.y>=0f && p.y<40f)
        {player.characterController.enabled=false;player.transform.position=p+Vector3.up*0.1f;player.characterController.enabled=true;}
        if(data.cars!=null)foreach(var car in FindObjectsByType<DriveableVehicle>())
        {
            var state=data.cars.Find(c=>c.id==car.name);if(state==null)continue;car.owned=state.owned;car.StoredInGarage=state.stored;
            if(state.stored && Mathf.Abs(state.position.x)<240f && Mathf.Abs(state.position.z)<240f && state.position.y>=0 && state.position.y<5f)
            {
                var traffic=car.GetComponent<TrafficCarAI>();if(traffic!=null)traffic.enabled=false;
                var police=car.GetComponent<PoliceCarAI>();if(police!=null)police.enabled=false;
                car.transform.SetPositionAndRotation(state.position,Quaternion.Euler(0,state.yaw,0));
            }
            car.GetComponent<VehicleDamage>().RestoreCondition(state.health);
        }
        RefreshMarkers();
    }
    void OnGUI()
    {
        if(GameSession.BlockInput)return;
        GUI.Box(new Rect(18,18,390,JobActive?68:46),"");GUI.Label(new Rect(30,27,370,30),Objective);
        if(JobActive)GUI.Label(new Rect(30,53,370,24),"Осталось: "+Mathf.CeilToInt(remaining)+" с");
    }
    void OnApplicationQuit(){if(!GameSession.IsTest)Save(false);}
}
