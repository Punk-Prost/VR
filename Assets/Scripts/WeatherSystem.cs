using UnityEngine;

public class WeatherSystem : MonoBehaviour
{
    public static bool IsRaining { get; private set; }

    public Transform target;
    public Material[] wettableMaterials;
    public float weatherDuration = 75f;
    public float rainChance = 0.45f;

    ParticleSystem rain;
    float timer;
    bool raining;

    void Start()
    {
        CreateRain();
        SetWeather(false);
        timer = weatherDuration * 0.65f;
    }

    void OnDestroy()
    {
        if (IsRaining == raining) IsRaining = false;
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            timer = weatherDuration * Random.Range(0.75f, 1.25f);
            SetWeather(Random.value < rainChance);
        }

        if (target != null && rain != null)
            rain.transform.position = target.position + new Vector3(0f, 16f, 0f);
    }

    void CreateRain()
    {
        GameObject go = new GameObject("Rain System");
        go.transform.SetParent(transform);
        go.transform.position = Vector3.up * 16f;
        rain = go.AddComponent<ParticleSystem>();

        var main = rain.main;
        main.loop = true;
        main.startLifetime = 1.2f;
        main.startSpeed = 0.5f;
        main.startSize = 0.055f;
        main.maxParticles = 1700;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = rain.emission;
        emission.rateOverTime = 850f;

        var shape = rain.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(36f, 1f, 36f);

        var velocity = rain.velocityOverLifetime;
        velocity.enabled = true;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
        velocity.y = new ParticleSystem.MinMaxCurve(-19f, -17f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);

        var renderer = rain.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = RuntimeMaterials.Particle;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 7f;
        renderer.velocityScale = 0.15f;
    }

    public void SetWeather(bool rainOn)
    {
        raining = rainOn;
        IsRaining = rainOn;
        if (rain != null)
        {
            if (raining && !rain.isPlaying) rain.Play();
            if (!raining && rain.isPlaying) rain.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        RenderSettings.fogStartDistance = raining ? 55f : 110f;
        RenderSettings.fogEndDistance = raining ? 185f : 280f;

        if (wettableMaterials != null)
        {
            foreach (Material m in wettableMaterials)
            {
                if (m == null) continue;
                if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", raining ? 0.72f : 0.10f);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", raining ? 0.72f : 0.10f);
            }
        }
    }
}
