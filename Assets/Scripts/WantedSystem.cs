using UnityEngine;

public class WantedSystem : MonoBehaviour
{
    public static WantedSystem Instance { get; private set; }

    public VehicleInteractor player;
    public EconomySystem economy;
    public PoliceCarAI[] policeCars;
    public float heatDecayPerSecond = 2.4f;
    public float decayDelay = 12f;
    public float speedingThreshold = 23f;

    public float Heat { get; private set; }
    public int WantedLevel { get; private set; }

    float lastHeatTime = -999f;
    float speedReportCooldown;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        RefreshPolice();
    }

    void Update()
    {
        if (Heat > 0f && Time.time - lastHeatTime > decayDelay)
        {
            Heat = Mathf.Max(0f, Heat - heatDecayPerSecond * Time.deltaTime);
            RecalculateLevel();
        }

        if (speedReportCooldown > 0f)
            speedReportCooldown -= Time.deltaTime;
    }

    public void AddHeat(float amount)
    {
        if (amount <= 0f) return;
        Heat = Mathf.Clamp(Heat + amount, 0f, 100f);
        lastHeatTime = Time.time;
        RecalculateLevel();
    }

    public void ReportSpeed(float metersPerSecond)
    {
        if (speedReportCooldown > 0f) return;
        if (metersPerSecond < speedingThreshold) return;
        speedReportCooldown = 2.5f;
        AddHeat(Mathf.Lerp(2.5f, 7f, Mathf.InverseLerp(speedingThreshold, 35f, metersPerSecond)));
    }

    public void ReportCollision(float impact)
    {
        if (impact < 5f) return;
        AddHeat(Mathf.Clamp(impact * 0.45f, 2f, 12f));
    }

    public Transform GetTarget()
    {
        return player != null ? player.ActiveTransform : null;
    }

    public float GetTargetSpeed()
    {
        return player != null ? player.CurrentSpeed : 0f;
    }

    public void ResolvePoliceStop()
    {
        if (WantedLevel <= 0) return;
        int fine = 100 + WantedLevel * 125;
        if (economy != null) economy.TrySpend(Mathf.Min(economy.Money,fine));
        FindAnyObjectByType<MissionSystem>()?.FailJob("Контракт отменён: задержание");
        GameSession.Notify("Задержание. Штраф до $"+fine);
        Heat = 0f;
        RecalculateLevel();
    }

    void RecalculateLevel()
    {
        int newLevel = Heat <= 0.1f ? 0 : Mathf.Clamp(Mathf.CeilToInt(Heat / 20f), 1, 5);
        if (newLevel == WantedLevel) return;
        WantedLevel = newLevel;
        RefreshPolice();
    }

    void RefreshPolice()
    {
        if (policeCars == null) return;
        for (int i = 0; i < policeCars.Length; i++)
        {
            if (policeCars[i] == null) continue;
            policeCars[i].wanted = this;
            policeCars[i].SetResponseActive(WantedLevel > 0 && i < Mathf.Max(1, WantedLevel));
        }
    }
    void OnDestroy(){if(Instance==this)Instance=null;}
}
