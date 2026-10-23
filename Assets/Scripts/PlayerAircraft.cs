using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class PlayerAircraft : MonoBehaviour
{
    // Player-only flight controller; traffic AI never claims this vehicle.
    public Transform cameraTarget;
    public float enginePower = 42f;
    public float liftPower = 1.15f;
    public float pitchPower = 34f;
    public float rollPower = 42f;
    public float yawPower = 18f;
    public float maxSpeed = 82f;
    public float stallSpeed = 12f;

    Rigidbody body;
    float throttle;
    bool testFlight;
    public void StartTestFlight(){if(GameSession.IsTest){testFlight=true;throttle=1f;}}
    public bool IsDriven { get; private set; }
    public float Speed => body == null ? 0f : body.linearVelocity.magnitude;
    public bool CanExit => Speed < 3f && Physics.Raycast(transform.position + Vector3.up, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore);

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.mass = Mathf.Max(850f, body.mass);
        body.linearDamping = 0.025f;
        body.angularDamping = 1.6f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.centerOfMass = new Vector3(0f, -0.35f, 0.2f);
    }

    public void SetDriven(bool value)
    {
        IsDriven = value;
        if (!value) throttle = Mathf.Min(throttle, 0.18f);
    }

    public Vector3 GetSafeExitPosition()
    {
        Vector3 side = transform.position + transform.right * 4f + Vector3.up * 0.6f;
        if (Physics.Raycast(side + Vector3.up * 12f, Vector3.down, out RaycastHit hit, 30f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point + Vector3.up * 0.15f;
        return side;
    }

    void FixedUpdate()
    {
        if (body == null || GameSession.BlockInput) return;
        if (!IsDriven)
        {
            body.AddForce(Vector3.down * 5f, ForceMode.Acceleration);
            return;
        }

        float pitch = testFlight?0f:(Input.GetKey(KeyCode.S)?1f:0f)-(Input.GetKey(KeyCode.W)?1f:0f);
        float roll = testFlight?0f:(Input.GetKey(KeyCode.A)?1f:0f)-(Input.GetKey(KeyCode.D)?1f:0f);
        float yaw = (Input.GetKey(KeyCode.C) ? 1f : 0f) - (Input.GetKey(KeyCode.Z) ? 1f : 0f);
        if (Input.GetKey(KeyCode.LeftShift)) throttle = Mathf.MoveTowards(throttle, 1f, 0.8f * Time.fixedDeltaTime);
        if (Input.GetKey(KeyCode.LeftControl)) throttle = Mathf.MoveTowards(throttle, 0f, 0.8f * Time.fixedDeltaTime);

        float forwardSpeed = Mathf.Max(0f, Vector3.Dot(body.linearVelocity, transform.forward));
        body.AddForce(transform.forward * enginePower * throttle, ForceMode.Acceleration);
        float liftFactor = Mathf.Clamp01((forwardSpeed - stallSpeed * 0.35f) / Mathf.Max(1f, stallSpeed));
        body.AddForce(transform.up * forwardSpeed * liftPower * liftFactor, ForceMode.Acceleration);
        body.AddTorque(transform.right * pitch * pitchPower + transform.forward * roll * rollPower + transform.up * yaw * yawPower, ForceMode.Acceleration);
        body.AddForce(-transform.right * Vector3.Dot(body.linearVelocity, transform.right) * 1.7f, ForceMode.Acceleration);
        body.AddForce(-transform.up * Vector3.Dot(body.linearVelocity, transform.up) * 0.12f, ForceMode.Acceleration);
        if (body.linearVelocity.magnitude > maxSpeed) body.linearVelocity = body.linearVelocity.normalized * maxSpeed;
    }
}

public static class PlayerAircraftRuntimeInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureAircraftExists()
    {
        if (Object.FindAnyObjectByType<PlayerAircraft>() != null) return;

        GameObject aircraft = new GameObject("PlayerOnlyAircraft_Runtime");
        aircraft.transform.SetPositionAndRotation(new Vector3(205f, 0.75f, 125f), Quaternion.Euler(0f, 180f, 0f));
        Material body = MakeMaterial(new Color(0.72f, 0.75f, 0.80f));
        Material accent = MakeMaterial(new Color(0.05f, 0.22f, 0.58f));
        Material tail = MakeMaterial(new Color(0.72f, 0.05f, 0.04f));

        GameObject fuselage = Primitive("Fuselage", PrimitiveType.Cylinder, aircraft.transform, new Vector3(0f,0.8f,0f), new Vector3(1.15f,3.9f,1.15f), body);
        fuselage.transform.localRotation = Quaternion.Euler(90f,0f,0f);
        Primitive("MainWing", PrimitiveType.Cube, aircraft.transform, new Vector3(0f,0.8f,0f), new Vector3(8.2f,0.18f,1.55f), accent);
        Primitive("TailWing", PrimitiveType.Cube, aircraft.transform, new Vector3(0f,1.0f,-3.0f), new Vector3(3.1f,0.14f,0.75f), accent);
        Primitive("TailFin", PrimitiveType.Cube, aircraft.transform, new Vector3(0f,1.65f,-3.15f), new Vector3(0.16f,1.35f,0.9f), tail);

        BoxCollider collider = aircraft.AddComponent<BoxCollider>();
        collider.size = new Vector3(8.2f, 1.8f, 7.8f);
        collider.center = new Vector3(0f, 0.9f, 0f);
        Rigidbody bodyPhysics = aircraft.AddComponent<Rigidbody>();
        bodyPhysics.mass = 1250f;
        PlayerAircraft flight = aircraft.AddComponent<PlayerAircraft>();
        GameObject target = new GameObject("AircraftCameraTarget");
        target.transform.SetParent(aircraft.transform, false);
        target.transform.localPosition = new Vector3(0f, 1.6f, -0.5f);
        flight.cameraTarget = target.transform;
    }

    static Material MakeMaterial(Color color)
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        Material material = new Material(shader);
        material.color = color;
        return material;
    }

    static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = material;
        return go;
    }
}
