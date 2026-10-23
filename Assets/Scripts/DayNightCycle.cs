using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    public Light sun;
    public Light[] streetLights;
    public float dayLengthSeconds = 240f;
    [Range(0f, 1f)] public float startTime = 0.30f;

    float time01;

    void Start()
    {
        time01 = startTime;
        UpdateLighting();
    }

    void Update()
    {
        time01 = Mathf.Repeat(time01 + Time.deltaTime / Mathf.Max(30f, dayLengthSeconds), 1f);
        UpdateLighting();
    }

    void UpdateLighting()
    {
        if (sun == null) return;
        float angle = time01 * 360f - 90f;
        sun.transform.rotation = Quaternion.Euler(angle, -35f, 0f);
        float daylight = Mathf.Clamp01(Vector3.Dot(-sun.transform.forward, Vector3.up) * 1.25f);
        sun.intensity = Mathf.Lerp(0.03f, 1.15f, daylight);
        RenderSettings.ambientLight = Color.Lerp(new Color(0.055f, 0.07f, 0.12f), new Color(0.62f, 0.66f, 0.72f), daylight);
        RenderSettings.fogColor = Color.Lerp(new Color(0.04f, 0.055f, 0.09f), new Color(0.67f, 0.76f, 0.84f), daylight);
        bool night = daylight < 0.28f;
        if (streetLights != null)
            foreach (Light l in streetLights) if (l != null) l.enabled = night;
    }
}
