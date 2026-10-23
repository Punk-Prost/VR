using UnityEngine;

public class CityShop : MonoBehaviour
{
    public bool garage;
    public VehicleInteractor player;
    bool open;
    DriveableVehicle[] stock;
    void Start(){if(player==null)player=FindAnyObjectByType<VehicleInteractor>();stock=FindObjectsByType<DriveableVehicle>();}
    void Update()
    {
        if(player==null)return;
        if(open && Input.GetKeyDown(KeyCode.Escape)){Close();return;}
        if(!GameSession.BlockInput && Input.GetKeyDown(KeyCode.B) && Vector3.Distance(player.ActiveTransform.position,transform.position)<13f)
        {open=true;GameSession.SetModal(true);}
    }
    void Close(){open=false;GameSession.SetModal(false);}
    void OnGUI()
    {
        if(player==null)return;
        if(!open)
        {
            if(!GameSession.BlockInput && Vector3.Distance(player.ActiveTransform.position,transform.position)<13f)
                GUI.Box(new Rect(Screen.width/2-180,Screen.height-155,360,28),garage?"B — автосалон / гараж · G — ремонт $75":"B — оружейный магазин");
            return;
        }
        var rect=new Rect(Screen.width/2-220,Screen.height/2-180,440,360);
        GUI.Box(rect,garage?"Автосалон и гараж":"Оружие и боеприпасы");
        GUILayout.BeginArea(new Rect(rect.x+20,rect.y+40,400,300));
        var economy=EconomySystem.Instance;GUILayout.Label("Баланс: $"+(economy!=null?economy.Money:0));
        if(garage)
        {
            foreach(var car in stock)if(car!=null && car.purchasePrice>0)
            {
                if(!car.owned && GUILayout.Button(car.name+" — $"+car.purchasePrice))
                {if(car.CanUse(economy))GameSession.Notify("Машина куплена: забери её у гаража");else GameSession.Notify("Недостаточно денег");}
                if(car.owned)GUILayout.Label(car.name+" — куплена");
            }
            if(player.ActiveVehicle!=null && GUILayout.Button("Сохранить текущую машину в гараже"))
            {
                var car=player.ActiveVehicle;car.StoredInGarage=true;car.resumeAIWhenPlayerExits=false;
                FindAnyObjectByType<MissionSystem>()?.Save();GameSession.Notify("Машина закреплена за гаражом");
            }
        }
        else
        {
            var inventory=player.GetComponent<WeaponInventory>();
            for(int i=0;i<inventory.weapons.Length;i++)
            {
                var slot=inventory.weapons[i];int cost=slot.unlocked?60:i==1?600:450;
                if(GUILayout.Button(slot.displayName+(slot.unlocked?" · 3 магазина":" · оружие")+" — $"+cost))inventory.Buy(i,economy);
            }
        }
        GUILayout.Space(18);if(GUILayout.Button("Закрыть")){FindAnyObjectByType<MissionSystem>()?.Save(false);Close();}
        GUILayout.EndArea();
    }
}
