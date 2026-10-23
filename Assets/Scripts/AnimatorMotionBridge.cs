using UnityEngine;

public class AnimatorMotionBridge : MonoBehaviour
{
    public Animator animator;
    [HideInInspector] public float motion;
    [HideInInspector] public bool grounded = true;
    [HideInInspector] public float verticalVelocity;

    int speedHash = Animator.StringToHash("Speed");
    int moveHash = Animator.StringToHash("MoveSpeed");
    int movingHash = Animator.StringToHash("IsMoving");
    int runningHash = Animator.StringToHash("IsRunning");
    int groundedHash = Animator.StringToHash("Grounded");
    int jumpHash = Animator.StringToHash("Jump");
    bool wasGrounded = true;

    void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (animator != null) animator.applyRootMotion = false;
    }

    public void SetMotion(float normalizedSpeed, bool isGrounded, float yVelocity)
    {
        motion = Mathf.Clamp01(normalizedSpeed);
        grounded = isGrounded;
        verticalVelocity = yVelocity;
    }

    void Update()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.nameHash == speedHash && p.type == AnimatorControllerParameterType.Float) animator.SetFloat(speedHash, motion, 0.12f, Time.deltaTime);
            else if (p.nameHash == moveHash && p.type == AnimatorControllerParameterType.Float) animator.SetFloat(moveHash, motion, 0.12f, Time.deltaTime);
            else if (p.nameHash == movingHash && p.type == AnimatorControllerParameterType.Bool) animator.SetBool(movingHash, motion > 0.05f);
            else if (p.nameHash == runningHash && p.type == AnimatorControllerParameterType.Bool) animator.SetBool(runningHash, motion > 0.72f);
            else if (p.nameHash == groundedHash && p.type == AnimatorControllerParameterType.Bool) animator.SetBool(groundedHash, grounded);
            else if (p.nameHash == jumpHash && p.type == AnimatorControllerParameterType.Trigger && wasGrounded && !grounded) animator.SetTrigger(jumpHash);
        }
        wasGrounded = grounded;
    }
}
