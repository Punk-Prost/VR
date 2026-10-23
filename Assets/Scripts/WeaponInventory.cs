using UnityEngine;

public class WeaponInventory : MonoBehaviour
{
    [System.Serializable]
    public class WeaponSlot
    {
        public string displayName;
        public float damage;
        public float range;
        public float shotsPerSecond;
        public int magazineSize;
        public int ammoInMagazine;
        public int reserveAmmo;
        public int pellets = 1;
        public float spread;
        public bool unlocked;
    }

    public Camera aimCamera;
    public GameObject[] weaponVisuals;
    public WantedSystem wanted;
    public WeaponSlot[] weapons;
    public int ActiveIndex { get; private set; }

    float nextShot;
    Texture2D panel;
    Texture2D selected;

    void Awake()
    {
        weapons = new[] {
            new WeaponSlot { displayName="Pistol", damage=28f, range=90f, shotsPerSecond=4.2f, magazineSize=12, ammoInMagazine=12, reserveAmmo=72, spread=0.004f },
            new WeaponSlot { displayName="Rifle", damage=18f, range=125f, shotsPerSecond=10f, magazineSize=30, ammoInMagazine=30, reserveAmmo=180, spread=0.012f },
            new WeaponSlot { displayName="Shotgun", damage=12f, range=48f, shotsPerSecond=1.15f, magazineSize=8, ammoInMagazine=8, reserveAmmo=48, pellets=8, spread=0.055f }
        };
        panel = MakeTex(new Color(0.02f,0.02f,0.025f,0.82f));
        selected = MakeTex(new Color(0.95f,0.55f,0.08f,0.92f));
        Select(0);
        weapons[0].unlocked=true;
    }

    Texture2D MakeTex(Color c) { var t=new Texture2D(1,1); t.SetPixel(0,0,c); t.Apply(); return t; }

    void Update()
    {
        if (aimCamera == null) aimCamera = Camera.main;
        if (!GetComponent<PlayerController>().enabled || GameSession.BlockInput) return;
        if (Input.GetKeyDown(KeyCode.Alpha1)) Select(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) Select(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) Select(2);
        float wheel = Input.mouseScrollDelta.y;
        if (wheel != 0f) Select((ActiveIndex + (wheel < 0f ? 1 : weapons.Length - 1)) % weapons.Length);
        if (Input.GetKeyDown(KeyCode.R)) Reload();
        if (Input.GetMouseButton(0)) Fire();
    }

    void Select(int index)
    {
        if (weapons != null && index>0 && index<weapons.Length && !weapons[index].unlocked) return;
        ActiveIndex = Mathf.Clamp(index, 0, weapons.Length - 1);
        if (weaponVisuals == null) return;
        for (int i=0;i<weaponVisuals.Length;i++) if (weaponVisuals[i] != null) weaponVisuals[i].SetActive(i == ActiveIndex);
    }

    void Reload()
    {
        WeaponSlot w=weapons[ActiveIndex];
        int needed=w.magazineSize-w.ammoInMagazine;
        int moved=Mathf.Min(needed,w.reserveAmmo);
        w.ammoInMagazine+=moved; w.reserveAmmo-=moved;
    }

    void Fire()
    {
        if(aimCamera==null) return;
        WeaponSlot w=weapons[ActiveIndex];
        if (Time.time < nextShot) return;
        if (w.ammoInMagazine <= 0) { Reload(); return; }
        nextShot=Time.time+1f/w.shotsPerSecond;
        w.ammoInMagazine--;
        if(wanted!=null) wanted.AddHeat(1f);
        foreach(Collider nearby in Physics.OverlapSphere(transform.position,24f))
        {
            NPCWalker walker=nearby.GetComponentInParent<NPCWalker>();
            if(walker!=null) walker.Panic(transform.position,Random.Range(3f,6f));
        }
        for(int p=0;p<w.pellets;p++)
        {
            Vector3 direction=aimCamera.transform.forward + aimCamera.transform.right*Random.Range(-w.spread,w.spread) + aimCamera.transform.up*Random.Range(-w.spread,w.spread);
            if(!Physics.Raycast(aimCamera.transform.position,direction.normalized,out RaycastHit hit,w.range,~0,QueryTriggerInteraction.Ignore)) { CreateTracer(aimCamera.transform.position+direction.normalized*w.range); continue; }
            CreateTracer(hit.point);
            NPCHealth npc=hit.collider.GetComponentInParent<NPCHealth>();
            if(npc!=null) { npc.ApplyDamage(w.damage); if(wanted!=null) wanted.AddHeat(7f); continue; }
            VehicleDamage vehicle=hit.collider.GetComponentInParent<VehicleDamage>();
            if(vehicle!=null) { vehicle.ApplyDamage(w.damage*0.72f,hit.point,direction); if(wanted!=null) wanted.AddHeat(3.5f); }
            Rigidbody body=hit.rigidbody; if(body!=null) body.AddForceAtPosition(direction*85f,hit.point,ForceMode.Impulse);
        }
    }

    void CreateTracer(Vector3 end)
    {
        GameObject tracer=new GameObject("BulletTracer");
        LineRenderer line=tracer.AddComponent<LineRenderer>();
        line.positionCount=2; line.SetPosition(0,transform.position+Vector3.up*1.25f+transform.forward*0.45f); line.SetPosition(1,end);
        line.startWidth=0.025f; line.endWidth=0.006f; line.sharedMaterial=RuntimeMaterials.Particle; line.startColor=new Color(1f,0.82f,0.25f,0.95f); line.endColor=new Color(1f,0.25f,0.05f,0.05f);
        Destroy(tracer,0.055f);
    }

    void OnGUI()
    {
        if (weapons == null || weapons.Length == 0) return;
        if (!GetComponent<PlayerController>().enabled) return;
        float x=18f, y=Screen.height-56f;
        GUI.DrawTexture(new Rect(x-4f,y-4f,116f,42f),panel);
        for(int i=0;i<weapons.Length;i++)
        {
            Rect r=new Rect(x+i*38f,y,34f,34f);
            if(i==ActiveIndex) GUI.DrawTexture(r,selected);
            GUI.color=weapons[i].unlocked ? Color.white : Color.gray;
            GUI.Box(r, "");
            GUI.DrawTexture(new Rect(r.x+6,r.y+9,i==0?17:24,5),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x+10,r.y+13,5,8),Texture2D.whiteTexture);
            GUI.color=Color.white;
            if(i==ActiveIndex) GUI.Label(new Rect(r.x+3,r.y+18,34,20),weapons[i].ammoInMagazine.ToString());
        }
        GUI.Label(new Rect(Screen.width/2f-6f,Screen.height/2f-12f,20f,20f),"+");
    }
    public void SetVisible(bool visible)
    {
        if(weaponVisuals==null)return;
        for(int i=0;i<weaponVisuals.Length;i++)if(weaponVisuals[i]!=null)weaponVisuals[i].SetActive(visible && i==ActiveIndex);
    }
    public bool Buy(int index, EconomySystem economy)
    {
        if(index<0||index>=weapons.Length||economy==null)return false;
        int cost=weapons[index].unlocked?60:index==1?600:450;
        if(!economy.TrySpend(cost)){GameSession.Notify("Недостаточно денег");return false;}
        weapons[index].unlocked=true;weapons[index].reserveAmmo+=weapons[index].magazineSize*3;
        GameSession.Notify("Покупка добавлена в инвентарь");return true;
    }
}
