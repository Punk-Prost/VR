using UnityEngine;

public class WorldRecovery : MonoBehaviour
{
    public VehicleInteractor player;
    public Vector3 playerSpawn = new Vector3(-18f, 0.3f, -18f);
    public float fallY = -8f;

    void Update()
    {
        if (player == null) return;
        Transform active = player.ActiveTransform;
        if (active == null || active.position.y >= fallY) return;

        if (player.InVehicle && player.ActiveVehicle != null)
        {
            DriveableVehicle car = player.ActiveVehicle;
            car.transform.position = playerSpawn + new Vector3(2.5f, 0.8f, 0f);
            car.transform.rotation = Quaternion.identity;
            car.RecoverUpright();
        }
        else
        {
            CharacterController cc = player.characterController;
            if (cc != null) cc.enabled = false;
            player.transform.position = playerSpawn;
            player.transform.rotation = Quaternion.identity;
            if (cc != null) cc.enabled = true;
        }
    }
}
