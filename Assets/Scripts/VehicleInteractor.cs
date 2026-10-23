using UnityEngine;

public class VehicleInteractor : MonoBehaviour
{
    public PlayerController playerController;
    public CharacterController characterController;
    public HumanoidVisual visual;
    public GameObject animatedVisualRoot;
    public GameObject animatorVisualRoot;
    public ThirdPersonCamera followCamera;
    public EconomySystem economy;
    public float interactionRadius = 4.5f;

    DriveableVehicle currentVehicle;
    PlayerAircraft currentAircraft;

    public Transform ActiveTransform => currentAircraft != null ? currentAircraft.transform : currentVehicle != null ? currentVehicle.transform : transform;
    public DriveableVehicle ActiveVehicle => currentVehicle;
    public PlayerAircraft ActiveAircraft => currentAircraft;
    public bool InVehicle => currentVehicle != null || currentAircraft != null;
    public float CurrentSpeed => currentAircraft != null ? currentAircraft.Speed : currentVehicle != null ? currentVehicle.Speed : 0f;

    void Awake()
    {
        if (playerController == null) playerController = GetComponent<PlayerController>();
        if (characterController == null) characterController = GetComponent<CharacterController>();
        if (visual == null) visual = GetComponentInChildren<HumanoidVisual>();
        if (animatedVisualRoot == null)
        {
            LegacyCharacterAnimator a = GetComponentInChildren<LegacyCharacterAnimator>();
            if (a != null) animatedVisualRoot = a.gameObject;
        }
        if (animatorVisualRoot == null)
        {
            AnimatorMotionBridge b = GetComponentInChildren<AnimatorMotionBridge>();
            if (b != null) animatorVisualRoot = b.gameObject;
        }
        if (followCamera == null && Camera.main != null) followCamera = Camera.main.GetComponent<ThirdPersonCamera>();
    }

    void Start()
    {
        if (economy == null) economy = EconomySystem.Instance;
    }

    void Update()
    {
        if (GameSession.BlockInput || !Input.GetKeyDown(GameSession.InteractKey)) return;

        if (currentVehicle != null)
        {
            ExitVehicle();
            return;
        }
        if (currentAircraft != null)
        {
            if (!currentAircraft.CanExit) { GameSession.Notify("Сначала посади самолёт и остановись"); return; }
            ExitAircraft();
            return;
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, interactionRadius, ~0, QueryTriggerInteraction.Collide);
        DriveableVehicle closest = null;
        PlayerAircraft closestAircraft = null;
        float best = float.MaxValue;

        foreach (Collider hit in hits)
        {
            DriveableVehicle candidate = hit.GetComponentInParent<DriveableVehicle>();
            if (candidate == null || candidate.IsDriven) continue;

            float d = (candidate.transform.position - transform.position).sqrMagnitude;
            if (d < best)
            {
                best = d;
                closest = candidate;
            }
        }

        foreach (Collider hit in hits)
        {
            PlayerAircraft candidate = hit.GetComponentInParent<PlayerAircraft>();
            if (candidate == null || candidate.IsDriven) continue;
            float d = (candidate.transform.position - transform.position).sqrMagnitude;
            if (d < best) { best = d; closest = null; closestAircraft = candidate; }
        }

        // Every traffic, parked and police car is a DriveableVehicle in V6.
        if (closest != null && closest.CanUse(economy))
            EnterVehicle(closest);
        else if (closestAircraft != null)
            EnterAircraft(closestAircraft);
    }

    void SetPlayerVisible(bool visible)
    {
        WeaponInventory inventory = GetComponent<WeaponInventory>();
        if (inventory != null) inventory.SetVisible(visible);
        if (playerController != null) playerController.enabled = visible;
        if (characterController != null) characterController.enabled = visible;
        if (visual != null) visual.gameObject.SetActive(visible);
        if (animatedVisualRoot != null) animatedVisualRoot.SetActive(visible);
        if (animatorVisualRoot != null) animatorVisualRoot.SetActive(visible);
    }

    public void EnterVehicle(DriveableVehicle vehicle)
    {
        currentVehicle = vehicle;
        vehicle.SetDriven(true);

        SetPlayerVisible(false);

        transform.SetParent(vehicle.transform, true);
        transform.localPosition = new Vector3(0f, 0.65f, 0f);
        transform.localRotation = Quaternion.identity;

        if (followCamera != null)
            followCamera.SetTarget(vehicle.cameraTarget != null ? vehicle.cameraTarget : vehicle.transform);
    }

    public void ExitVehicle()
    {
        DriveableVehicle vehicle = currentVehicle;
        currentVehicle = null;

        transform.SetParent(null, true);
        transform.position = vehicle.GetSafeExitPosition();
        transform.rotation = Quaternion.Euler(0f, vehicle.transform.eulerAngles.y, 0f);

        vehicle.SetDriven(false);

        SetPlayerVisible(true);
        if (followCamera != null) followCamera.SetTarget(transform);
    }

    public void EnterAircraft(PlayerAircraft aircraft)
    {
        currentAircraft = aircraft;
        aircraft.SetDriven(true);
        SetPlayerVisible(false);
        transform.SetParent(aircraft.transform, true);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        if (followCamera != null) followCamera.SetTarget(aircraft.cameraTarget != null ? aircraft.cameraTarget : aircraft.transform);
    }

    public void ExitAircraft()
    {
        PlayerAircraft aircraft = currentAircraft;
        currentAircraft = null;
        transform.SetParent(null, true);
        transform.position = aircraft.GetSafeExitPosition();
        transform.rotation = Quaternion.Euler(0f, aircraft.transform.eulerAngles.y, 0f);
        aircraft.SetDriven(false);
        SetPlayerVisible(true);
        if (followCamera != null) followCamera.SetTarget(transform);
    }
}
