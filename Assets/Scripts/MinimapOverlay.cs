using System.Collections.Generic;
using UnityEngine;

// Schematic map: no second world camera, render target or duplicated city draw calls.
public class MinimapOverlay : MonoBehaviour
{
    public Transform target;
    public float size=180f,worldSize=78f,fullMapWorldSize=265f;
    MissionSystem missions;
    List<Vector3> route=new List<Vector3>();
    float nextRoute;
    void Start(){missions=FindAnyObjectByType<MissionSystem>();}
    void Update()
    {
        if(target==null || Time.time<nextRoute)return;nextRoute=Time.time+1f;
        route=missions!=null && missions.Destination.HasValue?RoadNavigation.FindPath(target.position,missions.Destination.Value):new List<Vector3>();
    }
    public void SetTarget(Transform t)=>target=t;
    Vector2 Project(Vector3 p,Vector3 center,float scale,float extent)=>new Vector2((p.x-center.x+extent)*scale,(center.z-p.z+extent)*scale);
    void Line(Vector2 a,Vector2 b,Color color,float width)
    {
        Color old=GUI.color;GUI.color=color;Vector2 d=b-a;
        // Rotating GUI.matrix inside BeginGroup loses the group's screen origin.
        if(Mathf.Abs(d.x)<0.01f)GUI.DrawTexture(new Rect(a.x-width/2,Mathf.Min(a.y,b.y),width,Mathf.Abs(d.y)),Texture2D.whiteTexture);
        else if(Mathf.Abs(d.y)<0.01f)GUI.DrawTexture(new Rect(Mathf.Min(a.x,b.x),a.y-width/2,Mathf.Abs(d.x),width),Texture2D.whiteTexture);
        else
        {
            int steps=Mathf.CeilToInt(d.magnitude/2f);
            for(int i=0;i<=steps;i++){Vector2 p=Vector2.Lerp(a,b,(float)i/Mathf.Max(1,steps));GUI.DrawTexture(new Rect(p.x-width/2,p.y-width/2,width,width),Texture2D.whiteTexture);}
        }
        GUI.color=old;
    }
    void Dot(Vector2 p,Color color,float radius)
    {Color old=GUI.color;GUI.color=color;GUI.DrawTexture(new Rect(p.x-radius,p.y-radius,radius*2,radius*2),Texture2D.whiteTexture);GUI.color=old;}
    void OnGUI()
    {
        if(target==null || GameSession.BlockInput)return;
        bool full=Input.GetKey(KeyCode.M);float pixels=full?Mathf.Min(Screen.width,Screen.height)*0.78f:size;
        float extent=full?fullMapWorldSize:worldSize;float scale=pixels/(extent*2f);
        Vector3 center=full?Vector3.zero:target.position;
        Rect rect=full?new Rect((Screen.width-pixels)/2,(Screen.height-pixels)/2,pixels,pixels):new Rect(Screen.width-pixels-18,18,pixels,pixels);
        GUI.Box(new Rect(rect.x-4,rect.y-4,pixels+8,pixels+8),"");GUI.BeginGroup(rect);
        GUI.color=new Color(0.08f,0.13f,0.14f);GUI.DrawTexture(new Rect(0,0,pixels,pixels),Texture2D.whiteTexture);GUI.color=Color.white;
        foreach(float c in RoadNavigation.Coordinates)
        {
            Line(Project(new Vector3(c,0,-240),center,scale,extent),Project(new Vector3(c,0,240),center,scale,extent),new Color(0.35f,0.40f,0.42f),Mathf.Max(3,14*scale));
            Line(Project(new Vector3(-240,0,c),center,scale,extent),Project(new Vector3(240,0,c),center,scale,extent),new Color(0.35f,0.40f,0.42f),Mathf.Max(3,14*scale));
        }
        for(int i=1;i<route.Count;i++)Line(Project(route[i-1],center,scale,extent),Project(route[i],center,scale,extent),Color.cyan,3f);
        if(missions!=null && missions.Destination.HasValue)Dot(Project(missions.Destination.Value,center,scale,extent),Color.cyan,5);
        Dot(Project(new Vector3(120,0,-40),center,scale,extent),new Color(0.3f,1f,0.5f),4);
        Dot(Project(new Vector3(-40,0,-40),center,scale,extent),Color.red,4);
        Dot(Project(new Vector3(205,0,125),center,scale,extent),Color.white,4);
        Vector2 player=Project(target.position,center,scale,extent);Dot(player,Color.yellow,4);
        Line(player,player+new Vector2(target.forward.x,-target.forward.z)*12,Color.yellow,2);
        GUI.Label(new Rect(6,2,40,22),"N ↑");GUI.EndGroup();
        if(full)GUI.Box(new Rect(rect.x,rect.yMax+8,pixels,28),"Голубой — маршрут · Зелёный — гараж · Красный — оружие · Белый — самолёт");
    }
}
