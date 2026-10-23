using UnityEngine;

public class GarageZone : MonoBehaviour
{
    public VehicleInteractor player;
    public EconomySystem economy;
    public float radius = 7f;
    public int servicePrice = 75;
    public Transform servicePoint;

    float cooldown;

    void Update()
    {
        if (GameSession.BlockInput) return;
        if (cooldown > 0f) cooldown -= Time.deltaTime;
        if (player == null || !player.InVehicle || cooldown > 0f) return;
        if (Vector3.Distance(player.ActiveTransform.position, transform.position) > radius) return;
        if (!Input.GetKeyDown(KeyCode.G)) return;

        DriveableVehicle car = player.ActiveVehicle;
        if (car == null) return;
        if (economy != null && !economy.TrySpend(servicePrice)) return;

        if (servicePoint != null)
        {
            car.transform.position = servicePoint.position;
            car.transform.rotation = servicePoint.rotation;
        }
        car.ServiceVehicle();
        cooldown = 1.5f;
    }
}
