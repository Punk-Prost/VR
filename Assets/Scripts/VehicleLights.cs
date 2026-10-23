using UnityEngine;

public class VehicleLights : MonoBehaviour
{
    public Light[] headlights;
    public MeshRenderer[] brakeLights;
    public Material brakeOn;
    public Material brakeOff;
    public DriveableVehicle vehicle;

    void Awake()
    {
        if (vehicle == null) vehicle = GetComponent<DriveableVehicle>();
    }

    void Update()
    {
        bool night = RenderSettings.ambientLight.grayscale < 0.30f;
        bool nearCamera = Camera.main == null || (Camera.main.transform.position - transform.position).sqrMagnitude < 85f * 85f;
        if (headlights != null)
            foreach (Light l in headlights)
                if (l != null) l.enabled = night && nearCamera;

        bool braking = vehicle != null && vehicle.IsBraking;
        if (brakeLights != null)
        {
            foreach (MeshRenderer r in brakeLights)
            {
                if (r == null || brakeOn == null || brakeOff == null) continue;
                r.sharedMaterial = braking ? brakeOn : brakeOff;
            }
        }
    }
}
