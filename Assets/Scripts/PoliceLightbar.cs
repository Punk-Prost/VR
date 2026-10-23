using UnityEngine;

public class PoliceLightbar : MonoBehaviour
{
    public PoliceCarAI policeAI;
    public Light redLight;
    public Light blueLight;
    public float flashRate = 8f;

    void Awake()
    {
        if (policeAI == null) policeAI = GetComponent<PoliceCarAI>();
    }

    void Update()
    {
        bool active = policeAI != null && policeAI.ResponseActive && policeAI.enabled;
        if (!active)
        {
            if (redLight != null) redLight.enabled = false;
            if (blueLight != null) blueLight.enabled = false;
            return;
        }

        bool phase = Mathf.Sin(Time.time * flashRate) > 0f;
        if (redLight != null) redLight.enabled = phase;
        if (blueLight != null) blueLight.enabled = !phase;
    }
}
