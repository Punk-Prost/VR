using UnityEngine;

[RequireComponent(typeof(Camera))]
public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 8f;
    public float minDistance = 4f;
    public float maxDistance = 13.5f;
    public float height = 2.2f;
    public float sensitivity = 3f;
    public float smooth = 14f;
    public LayerMask collisionMask = ~0;

    [Header("Vehicle camera")]
    public float vehicleDistance = 10.5f;
    public float vehicleHeight = 2.5f;
    public float baseFov = 65f;
    public float maxVehicleFov = 74f;
    public float autoCenterDelay = 1.4f;
    public float autoCenterSpeed = 2.7f;

    Camera cam;
    float yaw;
    float pitch = 16f;
    float lastMouseInputTime;
    public bool IsAiming { get; private set; }

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void Start()
    {
        if (target != null) yaw = target.eulerAngles.y;
        if (cam != null) cam.fieldOfView = baseFov;
        LockCursor();
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null) yaw = target.eulerAngles.y;
        lastMouseInputTime = Time.time;
    }

    void LateUpdate()
    {
        if (target == null || GameSession.BlockInput) return;

        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
            LockCursor();

        float mouseX = 0f;
        float mouseY = 0f;
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            mouseX = Input.GetAxisRaw("Mouse X");
            mouseY = Input.GetAxisRaw("Mouse Y");
            if (Mathf.Abs(mouseX) > 0.001f || Mathf.Abs(mouseY) > 0.001f)
                lastMouseInputTime = Time.time;

            yaw += mouseX * sensitivity * GameSession.MouseSensitivity;
            pitch -= mouseY * sensitivity * GameSession.MouseSensitivity;
            pitch = Mathf.Clamp(pitch, -15f, 60f);
        }

        DriveableVehicle vehicle = target.GetComponentInParent<DriveableVehicle>();
        bool inVehicle = vehicle != null && vehicle.IsDriven;
        PlayerAircraft aircraft = target.GetComponentInParent<PlayerAircraft>();
        bool inAircraft = aircraft != null && aircraft.IsDriven;
        IsAiming = !inVehicle && !inAircraft && Input.GetMouseButton(1);

        if (inVehicle && Time.time - lastMouseInputTime > autoCenterDelay && vehicle.Speed > 3f)
        {
            float wantedYaw = vehicle.transform.eulerAngles.y;
            yaw = Mathf.LerpAngle(yaw, wantedYaw, 1f - Mathf.Exp(-autoCenterSpeed * Time.deltaTime));
        }

        float wantedBaseDistance = IsAiming ? 3.25f : (inAircraft ? 16f : inVehicle ? vehicleDistance : distance);
        float zoomedDistance = inAircraft ? 16f : IsAiming ? 3.25f : Mathf.Clamp(wantedBaseDistance - (inVehicle ? Input.mouseScrollDelta.y * 0.7f : 0f), minDistance, maxDistance);
        if (!inVehicle && !inAircraft)
            distance = zoomedDistance;
        else if (inVehicle)
            vehicleDistance = zoomedDistance;

        float wantedHeight = IsAiming ? 1.55f : (inAircraft ? 4.2f : inVehicle ? vehicleHeight : height);
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 focus = target.position + Vector3.up * wantedHeight;
        if (IsAiming) focus += rot * Vector3.right * 0.62f;
        Vector3 desired = focus - rot * Vector3.forward * zoomedDistance;

        Vector3 rayDir = desired - focus;
        float rayDist = rayDir.magnitude;
        if (rayDist > 0.01f && Physics.SphereCast(focus, 0.25f, rayDir.normalized, out RaycastHit hit, rayDist, collisionMask, QueryTriggerInteraction.Ignore))
            desired = focus + rayDir.normalized * Mathf.Max(0.7f, hit.distance - 0.25f);

        transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-smooth * Time.deltaTime));
        transform.rotation = Quaternion.LookRotation(focus - transform.position, Vector3.up);

        if (cam != null)
        {
            float speed01 = inAircraft ? Mathf.Clamp01(aircraft.Speed / Mathf.Max(1f, aircraft.maxSpeed)) : inVehicle ? Mathf.Clamp01(vehicle.Speed / Mathf.Max(1f, vehicle.maxSpeed)) : 0f;
            float targetFov = IsAiming ? 52f : Mathf.Lerp(baseFov, maxVehicleFov, speed01);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, 1f - Mathf.Exp(-4f * Time.deltaTime));
        }
    }

    void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
