#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class CityInfrastructure
{
    static Material Mat(string name)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/Materials/"+name+".mat");
    static GameObject Box(string name,Vector3 position,Vector3 size,Material material,Transform parent,bool collider=true)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent);go.transform.position=position;go.transform.localScale=size;
        go.GetComponent<Renderer>().sharedMaterial=material;if(!collider)Object.DestroyImmediate(go.GetComponent<Collider>());return go;
    }
    static void Beam(string name,Vector3 a,Vector3 b,float width,float thickness,Material material,Transform parent)
    {
        var beam=Box(name,(a+b)*0.5f,new Vector3(width,thickness,Vector3.Distance(a,b)),material,parent);
        beam.transform.rotation=Quaternion.LookRotation(b-a,Vector3.up);
    }
    public static void Build()
    {
        var root=new GameObject("Bridge Tunnel Airport").transform;
        Material road=Mat("Asphalt"),concrete=Mat("ConcreteDark"),white=Mat("White");
        Box("Northern Canal",new Vector3(0,0.15f,199),new Vector3(27,0.2f,53),Mat("Water"),root,false);
        Beam("BridgeDeck",new Vector3(-160,8,190),new Vector3(160,8,190),14,0.7f,road,root);
        Beam("West approach",new Vector3(-226,0.15f,190),new Vector3(-160,8,190),14,0.7f,road,root);
        Beam("East approach",new Vector3(160,8,190),new Vector3(226,0.15f,190),14,0.7f,road,root);
        for(int i=-4;i<=4;i++)if(i!=0)
            Box("Bridge Pier",new Vector3(i*32,3.6f,190),new Vector3(2,7,10),concrete,root);
        foreach(float z in new[]{182.8f,197.2f})
        {
            Beam("Bridge Guardrail",new Vector3(-160,8.9f,z),new Vector3(160,8.9f,z),0.35f,1f,concrete,root);
            Beam("Ramp Guardrail",new Vector3(-226,1.1f,z),new Vector3(-160,8.9f,z),0.35f,1f,concrete,root);
            Beam("Ramp Guardrail",new Vector3(160,8.9f,z),new Vector3(226,1.1f,z),0.35f,1f,concrete,root);
        }
        for(int x=-156;x<160;x+=12)Box("Bridge lane marking",new Vector3(x,8.365f,190),new Vector3(5,0.02f,0.15f),white,root,false);
        // An arched, drivable tunnel on the western ring road, open at both ends.
        var tunnel=new GameObject("West Ring Tunnel");tunnel.transform.SetParent(root);tunnel.transform.position=new Vector3(-226,0,-120);
        var mesh=new Mesh{name="Tunnel arch"};var vertices=new Vector3[104];var triangles=new int[25*12];
        for(int i=0;i<=25;i++)
        {
            float angle=Mathf.PI*i/25f;float x=Mathf.Cos(angle)*8.5f,y=2f+Mathf.Sin(angle)*8f;
            vertices[i*2]=new Vector3(x,y,-30);vertices[i*2+1]=new Vector3(x,y,30);
            vertices[52+i*2]=vertices[i*2];vertices[53+i*2]=vertices[i*2+1];
        }
        int ti=0;
        for(int i=0;i<25;i++)
        {
            int a=i*2,b=a+1,c=a+2,d=a+3;
            foreach(int v in new[]{a,c,b,b,c,d,b+52,c+52,a+52,d+52,c+52,b+52})triangles[ti++]=v;
        }
        mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
        string path="Assets/Generated/TunnelArch.asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);mesh=old;}
        tunnel.AddComponent<MeshFilter>().sharedMesh=mesh;tunnel.AddComponent<MeshRenderer>().sharedMaterial=concrete;tunnel.AddComponent<MeshCollider>().sharedMesh=mesh;
        Box("Tunnel west foundation",new Vector3(-234.5f,1,-120),new Vector3(0.6f,2,60),concrete,root);
        Box("Tunnel east foundation",new Vector3(-217.5f,1,-120),new Vector3(0.6f,2,60),concrete,root);
        for(int z=-140;z<=-100;z+=20)
        {
            var light=new GameObject("Tunnel light").AddComponent<Light>();light.transform.SetParent(root);light.transform.position=new Vector3(-226,7,z);
            light.type=LightType.Point;light.range=18;light.intensity=3;light.color=new Color(1f,0.8f,0.55f);light.shadows=LightShadows.None;
        }
        Box("Eastern Airstrip",new Vector3(200,0.1f,119),new Vector3(18,0.16f,65),road,root);
        for(int z=91;z<149;z+=8)Box("Runway mark",new Vector3(200,0.19f,z),new Vector3(0.4f,0.02f,4),white,root,false);
        var aircraft=Object.FindAnyObjectByType<PlayerAircraft>();if(aircraft!=null)aircraft.transform.position=new Vector3(200,1,142);
        // Dedicated routes on sidewalks, plus one signal-controlled crossing.
        foreach(var npc in Object.FindObjectsByType<NPCWalker>())
        {
            if(npc.name!="NPC_1" && npc.name!="NPC_11" && npc.name!="NPC_21" && npc.name!="NPC_31")continue;
            Vector3[] points={new Vector3(-11,0.3f,9),new Vector3(11,0.3f,9),new Vector3(11,0.3f,28),new Vector3(11,0.3f,9),new Vector3(-11,0.3f,9),new Vector3(-11,0.3f,28)};
            var route=new Transform[points.Length];
            for(int i=0;i<points.Length;i++){var p=new GameObject("CrosswalkNode").transform;p.SetParent(root);p.position=points[i];route[i]=p;}
            int number=int.Parse(npc.name.Substring(4));
            npc.waypoints=route;npc.transform.position=new Vector3(number<20?-11:11,0.3f,12+number*0.35f);npc.speed=1.8f;
        }
    }
}
#endif
