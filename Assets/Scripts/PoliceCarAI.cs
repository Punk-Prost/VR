using UnityEngine;

public class PoliceCarAI : MonoBehaviour
{
    public WantedSystem wanted;
    public Transform[] patrolWaypoints;
    public Transform[] wheels;
    public float patrolSpeed = 6.5f;
    public float chaseSpeed = 11.5f;
    public float turnSpeed = 4.5f;
    public float sensorDistance = 5f;

    int patrolIndex;
    bool responseActive;
    float stopTimer;
    float currentSpeed;
    System.Collections.Generic.List<Vector3> pursuitRoute;
    int pursuitIndex;
    float reroute;

    public float CurrentSpeed => currentSpeed;
    public bool ResponseActive => responseActive;

    public void SetResponseActive(bool active)
    {
        responseActive = active;
        stopTimer = 0f;
    }

    void OnEnable()
    {
        RecalculateNearestPatrolPoint();
    }

    void RecalculateNearestPatrolPoint()
    {
        if (patrolWaypoints == null || patrolWaypoints.Length == 0) return;
        int nearest = 0;
        float best = float.MaxValue;
        for (int i = 0; i < patrolWaypoints.Length; i++)
        {
            float d = (patrolWaypoints[i].position - transform.position).sqrMagnitude;
            if (d < best)
            {
                best = d;
                nearest = i;
            }
        }
        patrolIndex = nearest;
    }

    void Update()
    {
        DriveableVehicle vehicle = GetComponent<DriveableVehicle>();
        if (vehicle != null && vehicle.IsDriven) return;

        Vector3 targetPosition;
        float desiredSpeed;

        Transform chaseTarget = wanted != null ? wanted.GetTarget() : null;
        if (responseActive && wanted != null && wanted.WantedLevel > 0 && chaseTarget != null)
        {
            targetPosition = chaseTarget.position;
            reroute-=Time.deltaTime;
            if(reroute<=0f || pursuitRoute==null)
            {pursuitRoute=RoadNavigation.FindPath(transform.position,chaseTarget.position);pursuitIndex=0;reroute=2f;}
            if(pursuitRoute!=null && pursuitIndex<pursuitRoute.Count)
            {
                while(pursuitIndex<pursuitRoute.Count-1 && Vector3.Distance(transform.position,pursuitRoute[pursuitIndex])<5f)pursuitIndex++;
                targetPosition=pursuitRoute[pursuitIndex];
            }
            desiredSpeed = chaseSpeed + wanted.WantedLevel * 0.65f;

            float d = Vector3.Distance(transform.position, chaseTarget.position);
            if (d < 4.5f && wanted.GetTargetSpeed() < 4.5f)
            {
                stopTimer += Time.deltaTime;
                if (stopTimer > 2.2f)
                {
                    wanted.ResolvePoliceStop();
                    stopTimer = 0f;
                }
            }
            else stopTimer = 0f;
        }
        else
        {
            if (patrolWaypoints == null || patrolWaypoints.Length == 0) return;
            targetPosition = patrolWaypoints[patrolIndex].position;
            desiredSpeed = patrolSpeed;
            if (Vector3.Distance(transform.position, targetPosition) < 2.2f)
                patrolIndex = (patrolIndex + 1) % patrolWaypoints.Length;
        }

        Vector3 flat = targetPosition - transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude > 0.04f)
        {
            Quaternion wantedRotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, wantedRotation, turnSpeed * Time.deltaTime);
        }

        bool blocked = false;
        Vector3 origin = transform.position + Vector3.up * 0.65f + transform.forward * 1.25f;
        if (Physics.Raycast(origin, transform.forward, out RaycastHit hit, sensorDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!hit.transform.IsChildOf(transform))
                blocked = hit.distance < 2.5f;
        }

        float targetSpeed = blocked ? 0f : desiredSpeed;
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, 8f * Time.deltaTime);
        transform.position += transform.forward * currentSpeed * Time.deltaTime;

        float spin = currentSpeed * 85f * Time.deltaTime;
        if (wheels != null)
            foreach (Transform wheel in wheels)
                if (wheel != null) wheel.Rotate(Vector3.right, spin, Space.Self);
    }
}
