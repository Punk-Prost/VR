using UnityEngine;

public class NPCHealth : MonoBehaviour
{
    public float maxHealth=100f;
    public float Health { get; private set; }
    bool dead;

    void Awake() { Health=maxHealth; }

    public void ApplyDamage(float amount)
    {
        if(dead || amount<=0f) return;
        Health-=amount;
        NPCWalker walker=GetComponent<NPCWalker>(); if(walker!=null) walker.Panic(transform.position-transform.forward*2f,6f);
        if(Health<=0f) Die();
    }

    void Die()
    {
        dead=true;
        NPCWalker walker=GetComponent<NPCWalker>(); if(walker!=null) walker.enabled=false;
        CharacterController controller=GetComponent<CharacterController>(); if(controller!=null) controller.enabled=false;
        Animator animator=GetComponentInChildren<Animator>(); if(animator!=null) animator.enabled=false;
        transform.rotation=Quaternion.Euler(0f,transform.eulerAngles.y,78f);
        Destroy(gameObject,12f);
    }
}
