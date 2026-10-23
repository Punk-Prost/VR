using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class DriveableVehicle : MonoBehaviour
{
    [Header("Ownership")]
    public bool owned = true;
    public int purchasePrice = 0;

    [Header("References")]
    public Transform cameraTarget;
    public Transform[] wheels;
    public WheelCollider frontLeftWheel;
    public WheelCollider frontRightWheel;
    public WheelCollider rearLeftWheel;
    public WheelCollider rearRightWheel;
    public GameObject npcDriverVisual;
    public GameObject npcExitPedestrianTemplate;

    [Header("Driving")]
    public float acceleration = 22f;
    public float reverseAcceleration = 13f;
    public float steering = 52f;
    public float maxSpeed = 26f;
    public float brakeStrength = 7f;
    public float lateralGrip = 7f;
    public float downforce = 1.8f;
    public float handbrakeGrip = 1.5f;
    public float aerodynamicDrag = 0.012f;
    public float highSpeedSteering = 0.42f;
    public float extraGravity = 13f;
    public float tractionAssist = 0.65f;

    [Header("NPC takeover")]
    public bool resumeAIWhenPlayerExits = true;
    public bool ejectNPCDriverOnTakeover = true;

    Rigidbody rb;
    TrafficCarAI trafficAI;
    PoliceCarAI policeAI;
    bool driven;
    float throttle;
    float steerInput;
    float speedHeatTimer;
    float performanceMultiplier = 1f;
    bool driverEjectionArmed = true;
    public int Gear { get; private set; } = 1;
    public float EngineRPM { get; private set; } = 850f;
    public string GearLabel => IsReversing ? "R" : Speed < 0.2f && Mathf.Abs(throttle)<0.1f ? "N" : Gear.ToString();
    float shiftTimer;
    Quaternion[] wheelBindRotations;
    Vector3[] wheelBindOffsets;
    WheelCollider[] wheelRig;
    public bool StoredInGarage;
    bool testInput;
    public void SetTestInput(float gas,float turn){if(GameSession.IsTest){testInput=true;throttle=gas;steerInput=turn;}}

    public bool IsDriven => driven;
    public bool HasNPCDriver => (trafficAI != null || policeAI != null) && !driven;
    public float Speed => rb != null ? rb.linearVelocity.magnitude : 0f;
    public float ForwardSpeed => rb != null ? Vector3.Dot(rb.linearVelocity, transform.forward) : 0f;
    public float ThrottleInput => throttle;
    public float SteeringInput => steerInput;
    public bool IsBraking => driven && (Input.GetKey(KeyCode.Space) || (Mathf.Abs(throttle) > 0.05f && Mathf.Sign(throttle) != Mathf.Sign(ForwardSpeed) && Mathf.Abs(ForwardSpeed) > 1f));
    public bool IsReversing => driven && ForwardSpeed < -0.6f;
    public float PerformanceMultiplier => performanceMultiplier;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        trafficAI = GetComponent<TrafficCarAI>();
        policeAI = GetComponent<PoliceCarAI>();
        wheelRig = new[] { frontLeftWheel, frontRightWheel, rearLeftWheel, rearRightWheel };
        wheelBindRotations = new Quaternion[4]; wheelBindOffsets = new Vector3[4];
        for(int i=0;i<4;i++) if(wheels!=null && i<wheels.Length && wheels[i]!=null && wheelRig[i]!=null)
        {
            wheelBindRotations[i]=Quaternion.Inverse(transform.rotation)*wheels[i].rotation;
            wheelBindOffsets[i]=transform.InverseTransformPoint(wheels[i].position)-transform.InverseTransformPoint(wheelRig[i].transform.position);
        }
        if(GetComponent<VehicleAudio>()==null) gameObject.AddComponent<VehicleAudio>();

        rb.centerOfMass = new Vector3(0f, -0.48f, 0.05f);
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (purchasePrice > 0 && !GameSession.IsTest)
            owned = PlayerPrefs.GetInt("MiniGTA_V6_Owned_" + gameObject.name, owned ? 1 : 0) == 1;

        if (trafficAI != null || policeAI != null)
        {
            rb.isKinematic = true;
            SetNPCVisual(true);
        }
    }

    public bool CanUse(EconomySystem economy)
    {
        if (owned) return true;
        if (economy == null || !economy.TrySpend(purchasePrice)) return false;
        owned = true;
        if(!GameSession.IsTest){PlayerPrefs.SetInt("MiniGTA_V6_Owned_" + gameObject.name, 1);PlayerPrefs.Save();}
        return true;
    }

    public void SetPerformanceMultiplier(float value)
    {
        performanceMultiplier = Mathf.Clamp01(value);
    }

    public void SetDriven(bool value)
    {
        if (driven == value) return;

        Vector3 inheritedVelocity = Vector3.zero;
        if (value)
        {
            if (trafficAI != null && trafficAI.enabled)
                inheritedVelocity = transform.forward * trafficAI.CurrentSpeed;
            else if (policeAI != null && policeAI.enabled)
                inheritedVelocity = transform.forward * policeAI.CurrentSpeed;
        }

        driven = value;
        throttle = 0f;
        steerInput = 0f;

        if (value)
        {
            if (ejectNPCDriverOnTakeover && driverEjectionArmed && npcDriverVisual != null && npcDriverVisual.activeSelf)
            {
                SpawnFleeingDriver();
                driverEjectionArmed = false;
            }

            if (trafficAI != null) trafficAI.enabled = false;
            if (policeAI != null) policeAI.enabled = false;
            SetNPCVisual(false);

            rb.isKinematic = false;
            rb.linearVelocity = inheritedVelocity;
            rb.angularVelocity = Vector3.zero;
            rb.WakeUp();
        }
        else
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            bool hasAI = trafficAI != null || policeAI != null;
            if (hasAI && resumeAIWhenPlayerExits && !StoredInGarage)
            {
                rb.isKinematic = true;
                if (trafficAI != null) trafficAI.enabled = true;
                if (policeAI != null) policeAI.enabled = true;
                SetNPCVisual(true);
                driverEjectionArmed = true;
            }
        }
    }

    void SpawnFleeingDriver()
    {
        if (npcExitPedestrianTemplate == null) return;

        GameObject pedestrian = Instantiate(npcExitPedestrianTemplate);
        pedestrian.name = gameObject.name + "_FormerDriver";
        pedestrian.transform.SetParent(null, true);
        pedestrian.transform.position = GetSafeExitPosition();
        pedestrian.transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y + 180f, 0f);

        FleeingDriver fleeing = pedestrian.GetComponent<FleeingDriver>();
        if (fleeing != null)
            fleeing.Initialize(transform.position);

        pedestrian.SetActive(true);
    }

    void SetNPCVisual(bool visible)
    {
        if (npcDriverVisual != null)
            npcDriverVisual.SetActive(visible);
    }

    void Update()
    {
        UpdateTransmission();
        if (!driven || GameSession.BlockInput) { throttle=0f;steerInput=0f;return; }
        if(testInput)return;

        throttle = 0f;
        if (Input.GetKey(KeyCode.W)) throttle += 1f;
        if (Input.GetKey(KeyCode.S)) throttle -= 1f;

        steerInput = 0f;
        if (Input.GetKey(KeyCode.D)) steerInput += 1f;
        if (Input.GetKey(KeyCode.A)) steerInput -= 1f;

        if (!HasWheelColliderRig() && wheels != null)
        {
            float wheelSpin = Vector3.Dot(rb.linearVelocity, transform.forward) * 85f * Time.deltaTime;
            foreach (Transform wheel in wheels)
                if (wheel != null) wheel.Rotate(Vector3.right, wheelSpin, Space.Self);
        }

        if (Input.GetKeyDown(KeyCode.R) && Speed < 4f)
            RecoverUpright();

        speedHeatTimer -= Time.deltaTime;
        if (speedHeatTimer <= 0f)
        {
            speedHeatTimer = 1.2f;
            if (WantedSystem.Instance != null)
                WantedSystem.Instance.ReportSpeed(Speed);
        }
    }

    void FixedUpdate()
    {
        if (!driven || rb.isKinematic) return;

        if (HasWheelColliderRig())
        {
            DriveWheelColliders();
            return;
        }

        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        float speed = rb.linearVelocity.magnitude;
        float effectiveMaxSpeed = maxSpeed * performanceMultiplier;
        float speed01 = Mathf.Clamp01(speed / Mathf.Max(1f, effectiveMaxSpeed));
        bool grounded = Physics.Raycast(transform.position + Vector3.up * 0.55f, Vector3.down, 1.15f, ~0, QueryTriggerInteraction.Ignore);
        bool handbrake = Input.GetKey(KeyCode.LeftControl);

        float effectiveAcceleration = acceleration * performanceMultiplier;
        float effectiveReverse = reverseAcceleration * performanceMultiplier;
        float accel = throttle >= 0f ? effectiveAcceleration : effectiveReverse;
        float torqueFade = Mathf.Lerp(1f, 0.18f, speed01 * speed01);

        if (grounded && (Mathf.Abs(forwardSpeed) < effectiveMaxSpeed || Mathf.Sign(throttle) != Mathf.Sign(forwardSpeed)))
            rb.AddForce(transform.forward * throttle * accel * torqueFade, ForceMode.Acceleration);

        // Less steering at high speed, stronger response at parking speed.
        float steerSpeedFactor = Mathf.Lerp(1f, highSpeedSteering, speed01);
        float movementFactor = Mathf.Lerp(0.30f, 1f, Mathf.Clamp01(speed / 4f));
        float directionSign = Mathf.Abs(forwardSpeed) < 0.5f ? 1f : Mathf.Sign(forwardSpeed);
        float handbrakeSteer = handbrake ? 1.28f : 1f;
        if (grounded)
            rb.AddTorque(Vector3.up * steerInput * steering * steerSpeedFactor * movementFactor * directionSign * handbrakeSteer, ForceMode.Acceleration);

        if (Input.GetKey(KeyCode.Space))
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, brakeStrength * Time.fixedDeltaTime);

        // Tire grip: rain lowers traction, handbrake deliberately releases the rear.
        float wetGrip = WeatherSystem.IsRaining ? 0.68f : 1f;
        float grip = handbrake ? handbrakeGrip : lateralGrip * wetGrip;
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        float lateralKill = Mathf.Clamp01(grip * Time.fixedDeltaTime * Mathf.Lerp(0.8f, tractionAssist, speed01));
        localVelocity.x = Mathf.Lerp(localVelocity.x, 0f, lateralKill);
        rb.linearVelocity = transform.TransformDirection(localVelocity);

        if (grounded)
        {
            rb.AddForce(Vector3.down * (extraGravity + speed * downforce * 0.22f), ForceMode.Acceleration);
            Vector3 upright = Vector3.Cross(transform.up, Vector3.up);
            rb.AddTorque(upright * (handbrake ? 4.5f : 9f), ForceMode.Acceleration);

            // Small body roll / weight transfer makes steering feel less flat.
            float roll = -steerInput * speed01 * 4.2f;
            rb.AddTorque(transform.forward * roll, ForceMode.Acceleration);
        }

        // Aerodynamic drag prevents unrealistic endless coasting.
        if (speed > 0.1f)
            rb.AddForce(-rb.linearVelocity * speed * aerodynamicDrag, ForceMode.Acceleration);
    }

    bool HasWheelColliderRig()
    {
        return frontLeftWheel != null && frontRightWheel != null && rearLeftWheel != null && rearRightWheel != null;
    }

    void DriveWheelColliders()
    {
        float speed = rb.linearVelocity.magnitude;
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        float effectiveMaxSpeed = (throttle<0f?Mathf.Min(10f,maxSpeed):maxSpeed) * performanceMultiplier;
        float speed01 = Mathf.Clamp01(speed / Mathf.Max(1f, effectiveMaxSpeed));
        float steerAngle = steerInput * steering * Mathf.Lerp(1f, highSpeedSteering, speed01);
        frontLeftWheel.steerAngle = steerAngle;
        frontRightWheel.steerAngle = steerAngle;

        bool reversingDirection = Mathf.Abs(forwardSpeed) > 1f && Mathf.Abs(throttle) > 0.05f && Mathf.Sign(throttle) != Mathf.Sign(forwardSpeed);
        bool serviceBrake = reversingDirection;
        bool handbrake = Input.GetKey(KeyCode.Space);
        float brakeTorque = serviceBrake ? brakeStrength * 950f : 0f;
        float rearBrakeTorque = handbrake ? brakeStrength * 1450f : brakeTorque;

        float driveTorque = 0f;
        if (!serviceBrake && shiftTimer<=0f && (speed < effectiveMaxSpeed || Mathf.Sign(throttle) != Mathf.Sign(forwardSpeed)))
        {
            float accel = throttle >= 0f ? acceleration : reverseAcceleration;
            driveTorque = throttle * accel * rb.mass * 0.42f * performanceMultiplier * Mathf.Lerp(1f, 0.16f, speed01 * speed01);
            // Automatic gearbox changes delivered wheel torque, not just the HUD/RPM.
            driveTorque *= throttle<0f?0.75f:Mathf.Lerp(1.25f,0.68f,(Gear-1)/4f);
        }

        frontLeftWheel.motorTorque = driveTorque * 0.25f;
        frontRightWheel.motorTorque = driveTorque * 0.25f;
        rearLeftWheel.motorTorque = driveTorque * 0.25f;
        rearRightWheel.motorTorque = driveTorque * 0.25f;
        frontLeftWheel.brakeTorque = brakeTorque;
        frontRightWheel.brakeTorque = brakeTorque;
        rearLeftWheel.brakeTorque = rearBrakeTorque;
        rearRightWheel.brakeTorque = rearBrakeTorque;

        float wetGrip = WeatherSystem.IsRaining ? 0.68f : 1f;
        SetSidewaysGrip(frontLeftWheel, lateralGrip * wetGrip);
        SetSidewaysGrip(frontRightWheel, lateralGrip * wetGrip);
        SetSidewaysGrip(rearLeftWheel, handbrake ? handbrakeGrip : lateralGrip * wetGrip);
        SetSidewaysGrip(rearRightWheel, handbrake ? handbrakeGrip : lateralGrip * wetGrip);

        rb.AddForce(Vector3.down * (extraGravity + speed * downforce * 0.22f), ForceMode.Acceleration);
        if (speed > 0.1f) rb.AddForce(-rb.linearVelocity * speed * aerodynamicDrag, ForceMode.Acceleration);
        UpdateWheelVisuals();
    }

    static void SetSidewaysGrip(WheelCollider wheel, float stiffness)
    {
        WheelFrictionCurve friction = wheel.sidewaysFriction;
        friction.stiffness = Mathf.Clamp(stiffness * 0.22f, 0.35f, 2.2f);
        wheel.sidewaysFriction = friction;
    }

    void UpdateWheelVisuals()
    {
        if (wheels == null || wheels.Length < 4) return;
        for (int i = 0; i < 4; i++)
        {
            if (wheels[i] == null || wheelRig[i] == null) continue;
            wheelRig[i].GetWorldPose(out Vector3 position, out Quaternion rotation);
            wheels[i].SetPositionAndRotation(position + transform.TransformVector(wheelBindOffsets[i]), rotation * wheelBindRotations[i]);
        }
    }

    void UpdateTransmission()
    {
        shiftTimer-=Time.deltaTime;
        float roadSpeed = trafficAI != null && trafficAI.enabled ? trafficAI.CurrentSpeed : Speed;
        float band = Mathf.Max(1f,maxSpeed/5f);
        if(shiftTimer<=0f)
        {
            if(roadSpeed>Gear*band && Gear<5) {Gear++;shiftTimer=0.24f;}
            else if(Gear>1 && roadSpeed<(Gear-1)*band-1.5f) {Gear--;shiftTimer=0.18f;}
        }
        float rpm=850f + Mathf.InverseLerp((Gear-1)*band-2f,Gear*band+1f,roadSpeed)*4900f;
        EngineRPM=Mathf.Lerp(EngineRPM,shiftTimer>0f?rpm*0.7f:rpm,Time.deltaTime*8f);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!driven || WantedSystem.Instance == null) return;
        WantedSystem.Instance.ReportCollision(collision.relativeVelocity.magnitude);
    }

    public Vector3 GetSafeExitPosition()
    {
        Vector3 left = transform.position - transform.right * 2.35f + Vector3.up * 0.35f;
        Vector3 right = transform.position + transform.right * 2.35f + Vector3.up * 0.35f;
        if (IsExitClear(left)) return left;
        if (IsExitClear(right)) return right;
        return transform.position - transform.forward * 2.8f + Vector3.up * 0.35f;
    }

    bool IsExitClear(Vector3 position)
    {
        Vector3 bottom = position + Vector3.up * 0.25f;
        Vector3 top = position + Vector3.up * 1.55f;
        return !Physics.CheckCapsule(bottom, top, 0.35f, ~0, QueryTriggerInteraction.Ignore);
    }

    public void RecoverUpright()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position += Vector3.up * 0.8f;
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
    }

    public void ServiceVehicle()
    {
        RecoverUpright();
        VehicleDamage damage = GetComponent<VehicleDamage>();
        if (damage != null) damage.RepairFull();
    }
}
