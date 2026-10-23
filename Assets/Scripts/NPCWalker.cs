using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class NPCWalker : MonoBehaviour
{
    public Transform[] waypoints;
    public float speed = 1.6f;
    public float turnSpeed = 5f;
    public HumanoidVisual visual;
    public LegacyCharacterAnimator animatedVisual;
    public AnimatorMotionBridge animatorBridge;
    public VehicleInteractor player;
    public float awarenessRadius = 7f;

    CharacterController controller;
    int index;
    int direction = 1;
    float pause;
    float yVelocity;
    float fleeTimer;
    float senseTimer;
    Vector3 fleeDirection;
    TrafficLightController crossingSignal;
    bool crossing;
    readonly Collider[] nearbyTraffic = new Collider[12];

    public void Panic(Vector3 dangerPosition, float duration = 4f)
    {
        Vector3 away=transform.position-dangerPosition; away.y=0f;
        if(away.sqrMagnitude<0.05f) away=transform.right;
        fleeDirection=away.normalized;
        fleeTimer=Mathf.Max(fleeTimer,duration+Random.Range(0f,2f));
    }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (visual == null) visual = GetComponentInChildren<HumanoidVisual>();
        if (animatedVisual == null) animatedVisual = GetComponentInChildren<LegacyCharacterAnimator>();
        if (animatorBridge == null) animatorBridge = GetComponentInChildren<AnimatorMotionBridge>();
    }

    void Start()
    {
        SelectNearestSegment();
        crossingSignal=FindAnyObjectByType<TrafficLightController>();
        speed *= Random.Range(0.85f, 1.15f);
        if (player == null)
        {
            GameObject p = GameObject.Find("Player");
            if (p != null) player = p.GetComponent<VehicleInteractor>();
        }
    }

    void Update()
    {
        SenseTraffic();

        if (fleeTimer > 0f) fleeTimer -= Time.deltaTime;

        if (waypoints == null || waypoints.Length == 0) return;

        if (pause > 0f && fleeTimer<=0f)
        {
            pause -= Time.deltaTime;
            if (visual != null) visual.SetMotion(0f, controller.isGrounded, yVelocity);
            if (animatedVisual != null) animatedVisual.SetMotion(0f, controller.isGrounded, yVelocity);
            if (animatorBridge != null) animatorBridge.SetMotion(0f, controller.isGrounded, yVelocity);
            ApplyGravity(Vector3.zero);
            return;
        }

        Vector3 to = waypoints[index].position - transform.position;
        to.y = 0f;
        bool wantsCrossing=Mathf.Abs(transform.position.z-9f)<1.5f && Mathf.Abs(transform.position.x)<12f && Mathf.Abs(waypoints[index].position.z-9f)<1.5f && transform.position.x*waypoints[index].position.x<0f;
        if(wantsCrossing && !crossing && crossingSignal!=null)
        {if(!crossingSignal.CanStartPedestrianCrossing){Move(Vector3.zero,0f);return;}crossing=true;}
        if (to.magnitude < 0.85f)
        {
            index = (index + direction + waypoints.Length) % waypoints.Length;
            crossing=false;
            if (Random.value < 0.14f) pause = Random.Range(0.7f, 2.6f);
            return;
        }

        bool running=fleeTimer>0f || WeatherSystem.IsRaining;
        Move(to.normalized * speed*(running?1.65f:1f),running?0.85f:0.48f);
    }

    void SenseTraffic()
    {
        senseTimer -= Time.deltaTime;
        if (senseTimer > 0f) return;
        senseTimer = Random.Range(0.20f, 0.38f);

        // Avoid every moving/parked gameplay vehicle, not only the player's car.
        int hits = Physics.OverlapSphereNonAlloc(transform.position, 3.0f, nearbyTraffic, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits; i++)
        {
            if(WantedSystem.Instance!=null && WantedSystem.Instance.WantedLevel>0 && nearbyTraffic[i].GetComponentInParent<PoliceOfficerAI>()!=null)
                fleeTimer=Mathf.Max(fleeTimer,2f);
            DriveableVehicle vehicle = nearbyTraffic[i].GetComponentInParent<DriveableVehicle>();
            if (vehicle == null) continue;
            var ai=vehicle.GetComponent<TrafficCarAI>();
            if(vehicle.Speed<2f && (ai==null || ai.CurrentSpeed<2f))continue;
            Vector3 awayFromTraffic = transform.position - vehicle.transform.position;
            awayFromTraffic.y = 0f;
            if (awayFromTraffic.sqrMagnitude < 0.05f) awayFromTraffic = transform.right;
            fleeDirection = awayFromTraffic.normalized;
            fleeTimer = Mathf.Max(fleeTimer, Random.Range(0.65f, 1.25f));
            return;
        }

        if (player == null || !player.InVehicle) return;

        Transform t = player.ActiveTransform;
        float d = Vector3.Distance(transform.position, t.position);
        if (d > awarenessRadius || player.CurrentSpeed < 6f) return;

        Vector3 away = transform.position - t.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.05f) away = transform.right;
        fleeDirection = away.normalized;
        fleeTimer = Random.Range(1.0f, 2.1f);
    }

    void Move(Vector3 horizontal, float animationSpeed)
    {
        Vector3 dir = horizontal;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir.normalized), turnSpeed * Time.deltaTime);
        ApplyGravity(horizontal);
        if (visual != null) visual.SetMotion(animationSpeed, controller.isGrounded, yVelocity);
        if (animatedVisual != null) animatedVisual.SetMotion(animationSpeed, controller.isGrounded, yVelocity);
        if (animatorBridge != null) animatorBridge.SetMotion(animationSpeed, controller.isGrounded, yVelocity);
    }

    void ApplyGravity(Vector3 horizontal)
    {
        if (controller.isGrounded && yVelocity < 0f) yVelocity = -1f;
        else yVelocity += -18f * Time.deltaTime;
        horizontal.y = yVelocity;
        controller.Move(horizontal * Time.deltaTime);
    }
    void SelectNearestSegment()
    {
        if(waypoints==null || waypoints.Length<2)return;
        float best=float.MaxValue;
        for(int i=0;i<waypoints.Length;i++)
        {
            Vector3 a=waypoints[i].position,b=waypoints[(i+1)%waypoints.Length].position,ab=b-a;
            float t=Mathf.Clamp01(Vector3.Dot(transform.position-a,ab)/Mathf.Max(0.01f,ab.sqrMagnitude));
            float d=(transform.position-(a+ab*t)).sqrMagnitude;
            if(d<best){best=d;index=(i+1)%waypoints.Length;}
        }
    }
}
