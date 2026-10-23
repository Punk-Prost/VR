using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Shared input gate: UI clicks must never also shoot, enter a car or steer.
public class GameSession : MonoBehaviour
{
    public static bool IsTest => Array.IndexOf(Environment.GetCommandLineArgs(), "-miniGTASmokeTest") >= 0;
    public static bool BlockInput { get; private set; }
    public static float MouseSensitivity { get; private set; } = 1f;
    public static KeyCode InteractKey { get; private set; } = KeyCode.E;
    public static KeyCode SprintKey { get; private set; } = KeyCode.LeftShift;
    public static string Notice { get; private set; }
    static float noticeUntil;
    bool menu;
    int binding;
    int quality = 1;
    float volume = 0.7f;
    VehicleInteractor player;
    UniversalRenderPipelineAsset pipeline;
    float originalRenderScale;
    float originalShadows;

    public static void Notify(string message) { Notice = message; noticeUntil = Time.unscaledTime + 4f; }
    public static void SetModal(bool open)
    {
        BlockInput = open; Time.timeScale = open ? 0f : 1f;
        Cursor.visible = open; Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
    }
    void Awake()
    {
        // Automated standalone tests must advance even when their window is hidden.
        if(IsTest)Application.runInBackground=true;
        BlockInput = false; Time.timeScale = 1f;
        MouseSensitivity = PlayerPrefs.GetFloat("City.Mouse",1f);
        InteractKey = (KeyCode)PlayerPrefs.GetInt("City.Interact",(int)KeyCode.E);
        SprintKey = (KeyCode)PlayerPrefs.GetInt("City.Sprint",(int)KeyCode.LeftShift);
        volume = PlayerPrefs.GetFloat("City.Volume",0.7f);
        quality = PlayerPrefs.GetInt("City.Quality",1);
        pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline != null) { originalRenderScale=pipeline.renderScale; originalShadows=pipeline.shadowDistance; }
        ApplySettings();
    }
    void Start() { player = FindAnyObjectByType<VehicleInteractor>(); }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!menu && BlockInput) return;
            menu = !menu; binding=0; SetModal(menu);
        }
    }
    void ApplySettings()
    {
        QualitySettings.vSyncCount = 0; Application.targetFrameRate = 60;
        QualitySettings.lodBias = quality == 0 ? 0.65f : quality == 1 ? 1f : 1.4f;
        AudioListener.volume = volume;
        if (pipeline != null) { pipeline.renderScale = quality == 0 ? 0.75f : 1f; pipeline.shadowDistance = quality == 0 ? 35f : quality == 1 ? 70f : 110f; }
    }
    void OnGUI()
    {
        if (Time.unscaledTime < noticeUntil) GUI.Box(new Rect(Screen.width/2-240,Screen.height-110,480,32),Notice);
        if (player != null && !menu)
        {
            GUI.Box(new Rect(Screen.width-180,228,162,26),"");
            GUI.Label(new Rect(Screen.width-165,231,160,25), "$ " + (EconomySystem.Instance != null ? EconomySystem.Instance.Money : 0));
            if (player.InVehicle)
            {
                var car=player.ActiveVehicle;
                var style=new GUIStyle(GUI.skin.label){fontSize=32,alignment=TextAnchor.MiddleRight};
                GUI.Box(new Rect(Screen.width-198,Screen.height-112,180,94), "");
                GUI.Label(new Rect(Screen.width-190,Screen.height-110,158,46),Mathf.RoundToInt(player.CurrentSpeed*3.6f)+"",style);
                GUI.Label(new Rect(Screen.width-180,Screen.height-62,160,24),car!=null ? "km/h   " + car.GearLabel + "   " + Mathf.RoundToInt(car.EngineRPM) + " rpm" : "km/h    AIR");
            }
        }
        if (!menu) return;
        Rect window = new Rect(Screen.width/2-220,Screen.height/2-245,440,490);
        GUI.Box(window,"Настройки / пауза");
        GUILayout.BeginArea(new Rect(window.x+24,window.y+35,392,440));
        GUILayout.Label("Графика");
        int next = GUILayout.Toolbar(quality,new[]{"Быстро","Баланс","Высоко"});
        if(next!=quality){quality=next;ApplySettings();}
        GUILayout.Space(12); GUILayout.Label("Чувствительность мыши");
        MouseSensitivity=GUILayout.HorizontalSlider(MouseSensitivity,0.25f,2.5f);
        GUILayout.Label("Громкость"); volume=GUILayout.HorizontalSlider(volume,0f,1f); AudioListener.volume=volume;
        GUILayout.Space(12);
        if(GUILayout.Button("Сесть / выйти: " + InteractKey)) binding=1;
        if(GUILayout.Button("Бег: " + SprintKey)) binding=2;
        if(binding>0)
        {
            GUILayout.Label("Нажми свободную клавишу (Esc — отмена)");
            Event e=Event.current;
            if(e.type==EventType.KeyDown && e.keyCode!=KeyCode.None)
            {
                KeyCode key=e.keyCode;
                if(key!=KeyCode.Escape && key!=KeyCode.W && key!=KeyCode.A && key!=KeyCode.S && key!=KeyCode.D && key!=KeyCode.B && key!=KeyCode.F && key!=KeyCode.M && key!=KeyCode.Space && key!=KeyCode.R && key!=KeyCode.G && key!=KeyCode.F5 && key!=InteractKey && key!=SprintKey)
                { if(binding==1)InteractKey=key;else SprintKey=key;binding=0; }
                if(key==KeyCode.Escape) binding=0;
                e.Use();
            }
        }
        GUILayout.Label("WASD — движение · Space — прыжок / ручник\nF — задание · B — магазин · G — ремонт\nM — карта · F5 — сохранить · 1–3 — оружие\nСамолёт: Shift/Ctrl — тяга, W/S — тангаж,\nA/D — крен, Z/C — руль. Выход после посадки.");
        if(GUILayout.Button("Сохранить игру")) FindAnyObjectByType<MissionSystem>()?.Save();
        if(GUILayout.Button("Применить и продолжить"))
        {
            ApplySettings();
            if(!IsTest){PlayerPrefs.SetFloat("City.Mouse",MouseSensitivity);PlayerPrefs.SetFloat("City.Volume",volume);PlayerPrefs.SetInt("City.Quality",quality);PlayerPrefs.SetInt("City.Interact",(int)InteractKey);PlayerPrefs.SetInt("City.Sprint",(int)SprintKey);PlayerPrefs.Save();}
            menu=false;SetModal(false);
        }
        GUILayout.EndArea();
    }
    void OnDestroy()
    {
        if(pipeline!=null){pipeline.renderScale=originalRenderScale;pipeline.shadowDistance=originalShadows;}
        BlockInput=false;Time.timeScale=1f;
    }
}
