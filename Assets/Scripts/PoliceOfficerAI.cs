using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PoliceOfficerAI : MonoBehaviour
{
    public Transform home;
    public float patrolRadius=12f;
    public float walkSpeed=2f;
    public float chaseSpeed=5.2f;
    public AnimatorMotionBridge animatorBridge;
    CharacterController controller;
    Vector3 patrolTarget;
    float nextPatrol;
    float stopTimer;
    Vector3 homePosition;

    void Awake(){controller=GetComponent<CharacterController>();}
    void Start(){homePosition=home!=null?home.position:transform.position;ChoosePatrol();}
    void Update()
    {
        WantedSystem wanted=WantedSystem.Instance;
        Transform target=wanted!=null && wanted.WantedLevel>0?wanted.GetTarget():null;
        Vector3 destination; float speed;
        if(target!=null){destination=target.position;speed=chaseSpeed;}
        else {if(Time.time>nextPatrol || Vector3.Distance(transform.position,patrolTarget)<1f)ChoosePatrol();destination=patrolTarget;speed=walkSpeed;}
        Vector3 move=destination-transform.position;move.y=0f;
        if(move.sqrMagnitude>0.2f){transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(move.normalized),8f*Time.deltaTime);controller.SimpleMove(move.normalized*speed);if(animatorBridge!=null)animatorBridge.SetMotion(speed/chaseSpeed,controller.isGrounded,0f);}
        if(target!=null && move.magnitude<2.2f){stopTimer+=Time.deltaTime;if(stopTimer>2f){wanted.ResolvePoliceStop();stopTimer=0f;}}else stopTimer=0f;
    }
    void ChoosePatrol(){Vector2 r=Random.insideUnitCircle*patrolRadius;patrolTarget=homePosition+new Vector3(r.x,0f,r.y);nextPatrol=Time.time+Random.Range(4f,9f);}
}
