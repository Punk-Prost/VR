using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 4.5f;
    public float runSpeed = 7.5f;
    public float rotationSpeed = 12f;
    public float acceleration = 12f;

    [Header("Jump")]
    public float jumpHeight = 1.5f;
    public float gravity = -24f;

    [Header("References")]
    public Transform cameraTransform;
    public HumanoidVisual visual;
    public LegacyCharacterAnimator animatedVisual;
    public AnimatorMotionBridge animatorBridge;

    CharacterController controller;
    float verticalVelocity;
    float smoothedSpeed;

    public bool IsGrounded => controller != null && controller.isGrounded;
    public float VerticalVelocity => verticalVelocity;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (visual == null) visual = GetComponentInChildren<HumanoidVisual>();
        if (animatedVisual == null) animatedVisual = GetComponentInChildren<LegacyCharacterAnimator>();
        if (animatorBridge == null) animatorBridge = GetComponentInChildren<AnimatorMotionBridge>();
    }

    void Start()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        if (cameraTransform == null || GameSession.BlockInput) return;

        Vector2 input = Vector2.zero;
        if (Input.GetKey(KeyCode.W)) input.y += 1f;
        if (Input.GetKey(KeyCode.S)) input.y -= 1f;
        if (Input.GetKey(KeyCode.D)) input.x += 1f;
        if (Input.GetKey(KeyCode.A)) input.x -= 1f;
        input = Vector2.ClampMagnitude(input, 1f);

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDir = forward * input.y + right * input.x;
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        bool moving = moveDir.sqrMagnitude > 0.001f;
        bool running = moving && Input.GetKey(GameSession.SprintKey);
        float targetSpeed = moving ? (running ? runSpeed : walkSpeed) : 0f;
        smoothedSpeed = Mathf.MoveTowards(smoothedSpeed, targetSpeed, acceleration * Time.deltaTime);

        if (moving)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }
        ThirdPersonCamera thirdPerson = cameraTransform.GetComponent<ThirdPersonCamera>();
        if (thirdPerson != null && thirdPerson.IsAiming)
        {
            Vector3 aimForward = cameraTransform.forward; aimForward.y = 0f;
            if (aimForward.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(aimForward), rotationSpeed * 1.4f * Time.deltaTime);
        }

        if (controller.isGrounded)
        {
            if (verticalVelocity < 0f) verticalVelocity = -2f;
            if (Input.GetKeyDown(KeyCode.Space))
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 velocity = moveDir * smoothedSpeed;
        velocity.y = verticalVelocity;
        controller.Move(velocity * Time.deltaTime);

        if (visual != null)
        {
            float animSpeed = runSpeed <= 0.01f ? 0f : smoothedSpeed / runSpeed;
            visual.SetMotion(animSpeed, controller.isGrounded, verticalVelocity);
        }
        if (animatedVisual != null)
        {
            float animSpeed = runSpeed <= 0.01f ? 0f : smoothedSpeed / runSpeed;
            animatedVisual.SetMotion(animSpeed, controller.isGrounded, verticalVelocity);
        }
        if (animatorBridge != null)
        {
            float animSpeed = runSpeed <= 0.01f ? 0f : smoothedSpeed / runSpeed;
            animatorBridge.SetMotion(animSpeed, controller.isGrounded, verticalVelocity);
        }
    }
}
