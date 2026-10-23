using UnityEngine;

[RequireComponent(typeof(DriveableVehicle))]
public class VehicleAudio : MonoBehaviour
{
    static AudioClip engineClip, skidClip, impactClip;
    AudioSource engine, skid, impact;
    DriveableVehicle car;
    Rigidbody body;
    void Awake()
    {
        car=GetComponent<DriveableVehicle>();body=GetComponent<Rigidbody>();
        if(engineClip==null)
        {
            engineClip=MakeClip("Engine synth",1f,0);
            skidClip=MakeClip("Tyre noise synth",0.6f,1);
            impactClip=MakeClip("Impact synth",0.32f,2);
        }
        engine=Source(engineClip,true);skid=Source(skidClip,true);impact=Source(impactClip,false);
        engine.volume=0f;skid.volume=0f;engine.Play();skid.Play();
    }
    AudioSource Source(AudioClip clip,bool loop)
    {
        var s=gameObject.AddComponent<AudioSource>();s.clip=clip;s.loop=loop;s.spatialBlend=1f;
        s.rolloffMode=AudioRolloffMode.Linear;s.minDistance=4f;s.maxDistance=42f;s.playOnAwake=false;s.dopplerLevel=0.3f;return s;
    }
    static AudioClip MakeClip(string name,float seconds,int kind)
    {
        const int rate=22050;var values=new float[Mathf.CeilToInt(seconds*rate)];var random=new System.Random(47+kind);
        for(int i=0;i<values.Length;i++)
        {
            float t=(float)i/rate;float noise=(float)random.NextDouble()*2f-1f;
            values[i]=kind==0 ? (Mathf.Sin(t*2f*Mathf.PI*55f)+0.35f*Mathf.Sin(t*2f*Mathf.PI*110f)+0.12f*noise)*0.22f : kind==1 ? noise*0.15f : noise*Mathf.Exp(-t*18f)*0.6f;
        }
        var clip=AudioClip.Create(name,values.Length,1,rate,false);clip.SetData(values,0);return clip;
    }
    void Update()
    {
        bool moving=car.IsDriven || car.HasNPCDriver;
        engine.volume=Mathf.MoveTowards(engine.volume,moving ? 0.23f+Mathf.Abs(car.ThrottleInput)*0.13f:0f,Time.unscaledDeltaTime);
        engine.pitch=Mathf.Lerp(0.7f,2.7f,Mathf.InverseLerp(850f,6000f,car.EngineRPM));
        float slip=body.isKinematic?0f:Mathf.Abs(Vector3.Dot(body.linearVelocity,transform.right));
        skid.volume=car.IsDriven && car.Speed>3f ? Mathf.Clamp01((slip-1f)/5f)*0.55f:0f;
    }
    void OnCollisionEnter(Collision collision)
    { if(collision.relativeVelocity.magnitude>2f) impact.PlayOneShot(impactClip,Mathf.Clamp01(collision.relativeVelocity.magnitude/16f)); }
}
