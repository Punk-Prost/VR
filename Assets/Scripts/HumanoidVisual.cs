using UnityEngine;

public class HumanoidVisual : MonoBehaviour
{
    [Header("Rig references")]
    public Transform visualRoot;
    public Transform torso;
    public Transform head;
    public Transform leftArm;
    public Transform rightArm;
    public Transform leftLeg;
    public Transform rightLeg;

    [HideInInspector] public float motion;
    [HideInInspector] public bool grounded = true;
    [HideInInspector] public float verticalVelocity;

    float phase;
    float currentMotion;
    Vector3 torsoBasePos;
    Quaternion torsoBaseRot;
    Quaternion headBaseRot;
    Quaternion leftArmBase;
    Quaternion rightArmBase;
    Quaternion leftLegBase;
    Quaternion rightLegBase;

    void Awake()
    {
        CachePose();
        phase = Random.Range(0f, 10f);
    }

    public void CachePose()
    {
        if (torso != null)
        {
            torsoBasePos = torso.localPosition;
            torsoBaseRot = torso.localRotation;
        }
        if (head != null) headBaseRot = head.localRotation;
        if (leftArm != null) leftArmBase = leftArm.localRotation;
        if (rightArm != null) rightArmBase = rightArm.localRotation;
        if (leftLeg != null) leftLegBase = leftLeg.localRotation;
        if (rightLeg != null) rightLegBase = rightLeg.localRotation;
    }

    public void SetMotion(float normalizedSpeed, bool isGrounded, float yVelocity)
    {
        motion = Mathf.Clamp01(normalizedSpeed);
        grounded = isGrounded;
        verticalVelocity = yVelocity;
    }

    void Update()
    {
        if (torso == null) return;

        currentMotion = Mathf.MoveTowards(currentMotion, motion, Time.deltaTime * 4.8f);
        float cycleSpeed = Mathf.Lerp(1.35f, 10.2f, currentMotion);
        phase += Time.deltaTime * cycleSpeed;

        if (!grounded)
        {
            ApplyAirPose();
            return;
        }

        float stride = Mathf.Sin(phase) * Mathf.Lerp(0f, 46f, currentMotion);
        float secondary = Mathf.Sin(phase * 2f);
        float bob = Mathf.Abs(Mathf.Sin(phase)) * 0.055f * currentMotion;
        float lean = Mathf.Lerp(0f, 9f, currentMotion);
        float idleBreath = Mathf.Sin(Time.time * 1.8f + phase * 0.08f) * 0.010f * (1f - currentMotion);

        torso.localPosition = torsoBasePos + Vector3.up * (bob + idleBreath);
        torso.localRotation = torsoBaseRot * Quaternion.Euler(lean, 0f, secondary * 1.8f * currentMotion);

        if (head != null)
            head.localRotation = headBaseRot * Quaternion.Euler(-lean * 0.38f, Mathf.Sin(Time.time * 0.55f + phase) * 2.2f * (1f - currentMotion), 0f);
        if (leftArm != null)
            leftArm.localRotation = leftArmBase * Quaternion.Euler(-stride * 0.88f, 0f, 4f + secondary * currentMotion);
        if (rightArm != null)
            rightArm.localRotation = rightArmBase * Quaternion.Euler(stride * 0.88f, 0f, -4f - secondary * currentMotion);
        if (leftLeg != null)
            leftLeg.localRotation = leftLegBase * Quaternion.Euler(stride, 0f, -secondary * 1.5f * currentMotion);
        if (rightLeg != null)
            rightLeg.localRotation = rightLegBase * Quaternion.Euler(-stride, 0f, secondary * 1.5f * currentMotion);
    }

    void ApplyAirPose()
    {
        torso.localPosition = torsoBasePos;
        float pitch = verticalVelocity > 0f ? -7f : 8f;
        torso.localRotation = Quaternion.Slerp(torso.localRotation, torsoBaseRot * Quaternion.Euler(pitch, 0f, 0f), Time.deltaTime * 8f);
        if (head != null) head.localRotation = Quaternion.Slerp(head.localRotation, headBaseRot, Time.deltaTime * 8f);
        if (leftArm != null) leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, leftArmBase * Quaternion.Euler(-22f, 0f, 10f), Time.deltaTime * 8f);
        if (rightArm != null) rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation, rightArmBase * Quaternion.Euler(-22f, 0f, -10f), Time.deltaTime * 8f);
        if (leftLeg != null) leftLeg.localRotation = Quaternion.Slerp(leftLeg.localRotation, leftLegBase * Quaternion.Euler(18f, 0f, 0f), Time.deltaTime * 8f);
        if (rightLeg != null) rightLeg.localRotation = Quaternion.Slerp(rightLeg.localRotation, rightLegBase * Quaternion.Euler(-14f, 0f, 0f), Time.deltaTime * 8f);
    }
}
