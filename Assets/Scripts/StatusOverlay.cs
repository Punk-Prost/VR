using UnityEngine;

public class StatusOverlay : MonoBehaviour
{
    public WantedSystem wanted;
    public EconomySystem economy;
    public VehicleInteractor player;

    Texture2D wantedOn;
    Texture2D wantedOff;
    Texture2D moneyFill;
    Texture2D healthFill;
    Texture2D panel;

    void Start()
    {
        wantedOn = MakeTex(new Color(1f, 0.77f, 0.08f, 0.95f));
        wantedOff = MakeTex(new Color(0.22f, 0.22f, 0.22f, 0.75f));
        moneyFill = MakeTex(new Color(0.16f, 0.72f, 0.32f, 0.95f));
        healthFill = MakeTex(new Color(0.90f, 0.22f, 0.14f, 0.95f));
        panel = MakeTex(new Color(0.02f, 0.02f, 0.02f, 0.72f));
    }

    Texture2D MakeTex(Color c)
    {
        Texture2D t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    void OnGUI()
    {
        if (wanted != null)
        {
            float x = Screen.width - 155f;
            float y = 205f;
            for (int i = 0; i < 5; i++)
            {
                Rect r = new Rect(x + i * 26f, y, 18f, 18f);
                GUI.DrawTexture(r, i < wanted.WantedLevel ? wantedOn : wantedOff);
            }
        }

        if (player != null && player.InVehicle && player.ActiveVehicle != null)
        {
            VehicleDamage damage = player.ActiveVehicle.GetComponent<VehicleDamage>();
            if (damage != null)
            {
                float x = Screen.width - 155f;
                float y = 250f;
                GUI.DrawTexture(new Rect(x, y, 122f, 8f), panel);
                GUI.DrawTexture(new Rect(x + 2f, y + 2f, 118f * damage.NormalizedHealth, 4f), healthFill);
            }
        }
    }
}
