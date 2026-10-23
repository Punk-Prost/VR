using UnityEngine;

public class TrafficCarAI : MonoBehaviour
{
    public Transform[] waypoints;
    public float speed = 7f;
    public float turnSpeed = 5f;
    public float sensorDistance = 7f;
    public float acceleration = 4.5f;
    public float braking = 9f;
    public Transform[] wheels;

    int index;
    float currentSpeed;
    float speedBias = 1f;
    readonly RaycastHit[] sensorHits=new RaycastHit[24];
    float lateralOffset, laneTimer, parkedTimer;
    float parkingCountdown;
    bool parked;
    DriveableVehicle cachedVehicle;

    public float CurrentSpeed => currentSpeed;

    void OnEnable()
    {
        RecalculateNearestWaypoint();
        speedBias = Random.Range(0.90f, 1.08f);
    }

    void Start()
    {
        cachedVehicle=GetComponent<DriveableVehicle>();parkingCountdown=Random.Range(35f,100f);
        RecalculateNearestWaypoint();
    }

    void RecalculateNearestWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        int nearest = 0;
        float best = float.MaxValue;
        for (int i = 0; i < waypoints.Length; i++)
        {
            float d = (waypoints[i].position - transform.position).sqrMagnitude;
            if (d < best)
            {
                best = d;
                nearest = i;
            }
        }
        // Choose the forward end of the nearest segment, not an arbitrary corner.
        best=float.MaxValue;
        for(int i=0;i<waypoints.Length;i++)
        {
            Vector3 a=waypoints[i].position,b=waypoints[(i+1)%waypoints.Length].position;
            Vector3 ab=b-a;float t=Mathf.Clamp01(Vector3.Dot(transform.position-a,ab)/Mathf.Max(0.001f,ab.sqrMagnitude));
            float d=(a+ab*t-transform.position).sqrMagnitude;
            if(d<best){best=d;nearest=(i+1)%waypoints.Length;}
        }
        index = nearest;
    }

    void Update()
    {
        DriveableVehicle vehicle = cachedVehicle!=null?cachedVehicle:GetComponent<DriveableVehicle>();
        if (vehicle != null && vehicle.IsDriven) return;
        if (waypoints == null || waypoints.Length == 0) return;

        Transform target = waypoints[index];
        Vector3 flat = target.position - transform.position;
        flat.y = 0f;
        if (flat.magnitude < 2f)
        {
            index = (index + 1) % waypoints.Length;
            target = waypoints[index];
            flat = target.position - transform.position;
            flat.y = 0f;
        }

        float obstacleFactor = CheckObstacleFactor();
        laneTimer-=Time.deltaTime;parkingCountdown-=Time.deltaTime;
        bool straight=flat.magnitude>22f && Vector3.Dot(flat.normalized,transform.forward)>0.98f;
        // Only pass within our half of the road, away from junctions and red signals.
        float desiredOffset=0f;
        if(straight && obstacleFactor>0f && obstacleFactor<0.7f && laneTimer<=0f && LaneClear(-transform.right*1.4f))laneTimer=4f;
        if(laneTimer>0f && straight)desiredOffset=-1.4f;
        if(straight && parkingCountdown<=0f && LaneClear(transform.right*2.2f))
        {parked=true;parkedTimer=Random.Range(8f,16f);parkingCountdown=Random.Range(80f,150f);}
        if(parked)
        {
            desiredOffset=2.1f;parkedTimer-=Time.deltaTime;
            if(parkedTimer<=0f && LaneClear(-transform.right*2.1f)){parked=false;laneTimer=0f;}
        }
        float oldOffset=lateralOffset;lateralOffset=Mathf.MoveTowards(lateralOffset,desiredOffset,Time.deltaTime*0.65f);
        transform.position+=transform.right*(lateralOffset-oldOffset);
        if(parked)obstacleFactor=0f;
        float desired = speed * speedBias * obstacleFactor;
        float rate = desired < currentSpeed ? braking : acceleration;
        currentSpeed = Mathf.MoveTowards(currentSpeed, desired, Time.deltaTime * rate);

        if (flat.sqrMagnitude > 0.01f)
        {
            Quaternion wanted = Quaternion.LookRotation(flat.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, wanted, turnSpeed * Time.deltaTime);
        }
        transform.position += transform.forward * Mathf.Min(currentSpeed * Time.deltaTime,flat.magnitude);

        float spin = currentSpeed * 85f * Time.deltaTime;
        if (wheels != null)
            foreach (Transform wheel in wheels)
                if (wheel != null) wheel.Rotate(Vector3.right, spin, Space.Self);
    }

    float CheckObstacleFactor()
    {
        float factor = 1f;
        Vector3 baseOrigin = transform.position + Vector3.up * 0.65f + transform.forward * 1.35f;
        Vector3[] offsets = { Vector3.zero, transform.right * 0.52f, -transform.right * 0.52f };
        Vector3[] directions = {
            transform.forward,
            (transform.forward + transform.right * 0.08f).normalized,
            (transform.forward - transform.right * 0.08f).normalized
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            int count=Physics.RaycastNonAlloc(baseOrigin+offsets[i],directions[i],sensorHits,sensorDistance,~0,QueryTriggerInteraction.Collide);
            for(int j=0;j<count;j++)
            {
                var hit=sensorHits[j];
                if(hit.collider.transform.IsChildOf(transform))continue;
                TrafficStopZone zone=hit.collider.GetComponent<TrafficStopZone>();
                if(zone!=null){if(zone.IsRed)return 0f;continue;}
                if(!hit.collider.isTrigger)factor=Mathf.Min(factor,Mathf.InverseLerp(2f,sensorDistance,hit.distance));
            }
        }

        return Mathf.Clamp01(factor);
    }
    bool LaneClear(Vector3 offset)
    {
        int count=Physics.BoxCastNonAlloc(transform.position+Vector3.up*0.9f,new Vector3(0.9f,0.4f,2.5f),offset.normalized,sensorHits,transform.rotation,offset.magnitude+0.5f,~0,QueryTriggerInteraction.Ignore);
        for(int i=0;i<count;i++)if(!sensorHits[i].collider.transform.IsChildOf(transform))return false;
        return true;
    }
}
