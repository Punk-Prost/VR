using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FleeingDriver : MonoBehaviour
{
    public float speed = 4.2f;
    public float lifetime = 8f;
    public float turnSpeed = 8f;
    public HumanoidVisual visual;
    public LegacyCharacterAnimator animatedVisual;
    public AnimatorMotionBridge animatorBridge;

    CharacterController controller;
    Vector3 fleeDirection = Vector3.forward;
    float yVelocity;
    float remaining;
    bool initialized;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (visual == null) visual = GetComponentInChildren<HumanoidVisual>();
        if (animatedVisual == null) animatedVisual = GetComponentInChildren<LegacyCharacterAnimator>();
        if (animatorBridge == null) animatorBridge = GetComponentInChildren<AnimatorMotionBridge>();
        remaining = lifetime;
    }

    public void Initialize(Vector3 threatPosition)
    {
        Vector3 away = transform.position - threatPosition;
        away.y = 0f;
        if (away.sqrMagnitude < 0.05f) away = -transform.forward;
        fleeDirection = away.normalized;
        initialized = true;
        remaining = lifetime;
    }

    void Update()
    {
        remaining -= Time.deltaTime;
        if (remaining <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        if (!initialized) fleeDirection = transform.forward;

        if (Physics.Raycast(transform.position + Vector3.up * 0.7f, fleeDirection, 1.2f, ~0, QueryTriggerInteraction.Ignore))
        {
            float side = Mathf.Sin(Time.time * 3.1f) >= 0f ? 1f : -1f;
            fleeDirection = Quaternion.Euler(0f, 70f * side, 0f) * fleeDirection;
        }

        if (fleeDirection.sqrMagnitude > 0.01f)
        {
            Quaternion wanted = Quaternion.LookRotation(fleeDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, wanted, turnSpeed * Time.deltaTime);
        }

        if (controller.isGrounded && yVelocity < 0f) yVelocity = -1f;
        else yVelocity += -20f * Time.deltaTime;

        Vector3 move = fleeDirection * speed;
        move.y = yVelocity;
        controller.Move(move * Time.deltaTime);

        if (visual != null)
            visual.SetMotion(0.85f, controller.isGrounded, yVelocity);
        if (animatedVisual != null)
            animatedVisual.SetMotion(0.85f, controller.isGrounded, yVelocity);
        if (animatorBridge != null)
            animatorBridge.SetMotion(0.85f, controller.isGrounded, yVelocity);
    }
}
