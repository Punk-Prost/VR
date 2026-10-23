using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(DriveableVehicle))]
public class VehicleDamage : MonoBehaviour
{
    public float maxHealth=100f, damageThreshold=5.5f, collisionDamageScale=1.35f;
    public Transform smokePoint;
    public float Health {get;private set;}
    public float NormalizedHealth => Mathf.Clamp01(Health/Mathf.Max(1f,maxHealth));
    DriveableVehicle vehicle;
    TrafficCarAI traffic;
    PoliceCarAI police;
    ParticleSystem smoke;
    readonly Dictionary<MeshFilter,Mesh> originals=new Dictionary<MeshFilter,Mesh>();
    readonly List<Renderer> brokenGlass=new List<Renderer>();
    WheelCollider[] tyres;
    float[] radii;
    bool disabled;
    void Awake()
    {
        vehicle=GetComponent<DriveableVehicle>();traffic=GetComponent<TrafficCarAI>();police=GetComponent<PoliceCarAI>();Health=maxHealth;
        tyres=GetComponentsInChildren<WheelCollider>();radii=new float[tyres.Length];
        for(int i=0;i<tyres.Length;i++)radii[i]=tyres[i].radius;
    }
    void OnCollisionEnter(Collision collision)
    {
        float amount=(collision.relativeVelocity.magnitude-damageThreshold)*collisionDamageScale;
        if(amount<=0f || collision.contactCount==0)return;
        ContactPoint hit=collision.GetContact(0);ApplyDamage(amount,hit.point,-hit.normal);
    }
    public void ApplyDamage(float amount)=>ApplyDamage(amount,transform.position+transform.forward,-transform.forward);
    public void ApplyDamage(float amount,Vector3 point,Vector3 direction)
    {
        if(amount<=0f || disabled)return;
        Health=Mathf.Max(0f,Health-amount);
        // Local body deformation; never shrink an entire car or its skeleton.
        float radius=Mathf.Clamp(amount*0.025f,0.3f,1.1f);
        foreach(MeshFilter filter in GetComponentsInChildren<MeshFilter>())
        {
            var mesh=filter.sharedMesh;var renderer=filter.GetComponent<Renderer>();string n=filter.name.ToLowerInvariant();
            if(mesh==null || renderer==null || !mesh.isReadable || mesh.vertexCount>180000 || n.Contains("wheel") || n.Contains("tire") || n.Contains("tyre"))continue;
            if((renderer.bounds.ClosestPoint(point)-point).sqrMagnitude>radius*radius)continue;
            bool glassMaterial=renderer.sharedMaterials.Length==1 && renderer.sharedMaterial!=null && renderer.sharedMaterial.name.ToLowerInvariant().Contains("glass");
            if(n.Contains("glass") || n.Contains("window") || glassMaterial)
            {if(amount>10f && !brokenGlass.Contains(renderer)){brokenGlass.Add(renderer);renderer.enabled=false;}continue;}
            if(!originals.ContainsKey(filter)){originals[filter]=mesh;mesh=Instantiate(mesh);filter.sharedMesh=mesh;}
            Vector3[] vertices=mesh.vertices;
            Vector3 localDirection=filter.transform.InverseTransformVector(direction.normalized*Mathf.Min(0.22f,amount*0.004f));
            for(int i=0;i<vertices.Length;i++)
            {float distance=Vector3.Distance(filter.transform.TransformPoint(vertices[i]),point);if(distance<radius)vertices[i]+=localDirection*(1f-distance/radius);}
            mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();
        }
        if(amount>14f)for(int i=0;i<tyres.Length;i++)if(Vector3.Distance(tyres[i].transform.position,point)<0.75f)tyres[i].radius=radii[i]*0.65f;
        UpdateCondition();
        if(Health<=0f)
        {
            disabled=true;vehicle.SetPerformanceMultiplier(0f);
            if(traffic!=null)traffic.enabled=false;if(police!=null)police.enabled=false;
            foreach(var wheel in tyres){wheel.motorTorque=0f;wheel.brakeTorque=9000f;}
            if(vehicle.IsDriven)GameSession.Notify("Двигатель повреждён. Нужен ремонт в гараже");
        }
    }
    void UpdateCondition()
    {
        vehicle.SetPerformanceMultiplier(Health<=0f?0f:Mathf.Lerp(0.45f,1f,NormalizedHealth));
        if(NormalizedHealth<0.4f && smoke==null)
        {
            var go=new GameObject("Engine smoke");go.transform.SetParent(transform,false);go.transform.localPosition=new Vector3(0,1,1.3f);
            smoke=go.AddComponent<ParticleSystem>();var main=smoke.main;
            main.startLifetime=1.6f;main.startSpeed=0.7f;main.startSize=0.3f;main.startColor=new Color(0.18f,0.18f,0.18f,0.7f);main.maxParticles=70;
            var shape=smoke.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=12;shape.radius=0.1f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial=RuntimeMaterials.Particle;
        }
        if(smoke!=null){var emission=smoke.emission;emission.rateOverTime=NormalizedHealth<0.4f?18f:0f;}
    }
    public void RestoreCondition(float health){Health=Mathf.Clamp(health,0f,maxHealth);disabled=Health<=0f;UpdateCondition();}
    public void RepairFull()
    {
        foreach(var pair in originals)if(pair.Key!=null){Mesh damaged=pair.Key.sharedMesh;pair.Key.sharedMesh=pair.Value;if(damaged!=pair.Value)Destroy(damaged);}
        originals.Clear();foreach(var r in brokenGlass)if(r!=null)r.enabled=true;brokenGlass.Clear();
        for(int i=0;i<tyres.Length;i++){tyres[i].radius=radii[i];tyres[i].brakeTorque=0f;}
        bool wasDisabled=disabled;disabled=false;Health=maxHealth;vehicle.enabled=true;UpdateCondition();
        if(wasDisabled && !vehicle.IsDriven){if(traffic!=null)traffic.enabled=true;if(police!=null)police.enabled=true;}
    }
    void OnDestroy(){foreach(var pair in originals)if(pair.Key!=null && pair.Key.sharedMesh!=pair.Value)Destroy(pair.Key.sharedMesh);}
}

public static class RuntimeMaterials
{
    static Material particle;
    public static Material Particle
    {
        get
        {
            if(particle!=null)return particle;
            particle=Resources.Load<Material>("CityParticle");if(particle!=null)return particle;
            Shader shader=Shader.Find("Universal Render Pipeline/Particles/Unlit");if(shader==null)shader=Shader.Find("Sprites/Default");
            particle=new Material(shader){name="Shared runtime particles"};return particle;
        }
    }
}
