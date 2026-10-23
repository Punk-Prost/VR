using UnityEngine;

public class TrafficLightController : MonoBehaviour
{
    public float greenTime = 10f;
    public float allRedTime = 2f;
    public MeshRenderer[] northSouthRed;
    public MeshRenderer[] northSouthGreen;
    public MeshRenderer[] eastWestRed;
    public MeshRenderer[] eastWestGreen;
    public Material redOn;
    public Material redOff;
    public Material greenOn;
    public Material greenOff;

    float timer;
    int phase; // 0 NS green, 1 all red, 2 EW green, 3 all red

    public bool NorthSouthRed => phase != 0;
    public bool EastWestRed => phase != 2;
    public bool CanStartPedestrianCrossing => phase==3 && timer<5f;

    void Start() => ApplyVisuals();

    void Update()
    {
        timer += Time.deltaTime;
        float duration = phase==3 ? 28f : (phase == 0 || phase == 2) ? greenTime : allRedTime;
        if (timer >= duration)
        {
            timer = 0f;
            phase = (phase + 1) % 4;
            ApplyVisuals();
        }
    }

    void ApplyVisuals()
    {
        bool nsGreen = phase == 0;
        bool ewGreen = phase == 2;
        SetGroup(northSouthRed, nsGreen ? redOff : redOn);
        SetGroup(northSouthGreen, nsGreen ? greenOn : greenOff);
        SetGroup(eastWestRed, ewGreen ? redOff : redOn);
        SetGroup(eastWestGreen, ewGreen ? greenOn : greenOff);
    }

    static void SetGroup(MeshRenderer[] group, Material mat)
    {
        if (group == null || mat == null) return;
        foreach (MeshRenderer r in group) if (r != null) r.sharedMaterial = mat;
    }
}
