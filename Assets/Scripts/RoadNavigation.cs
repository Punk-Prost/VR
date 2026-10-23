using System.Collections.Generic;
using UnityEngine;

public static class RoadNavigation
{
    public static readonly float[] Coordinates={-226,-160,-80,0,80,160,226};
    static Vector3 Node(int id)=>new Vector3(Coordinates[id%7],0.1f,Coordinates[id/7]);
    public static Vector3 Snap(Vector3 p)
    {
        float x=Coordinates[0],z=x;
        foreach(float c in Coordinates){if(Mathf.Abs(p.x-c)<Mathf.Abs(p.x-x))x=c;if(Mathf.Abs(p.z-c)<Mathf.Abs(p.z-z))z=c;}
        return Mathf.Abs(p.x-x)<Mathf.Abs(p.z-z)?new Vector3(x,0.1f,Mathf.Clamp(p.z,-226,226)):new Vector3(Mathf.Clamp(p.x,-226,226),0.1f,z);
    }
    public static List<Vector3> FindPath(Vector3 from,Vector3 to)
    {
        Vector3 start=Snap(from),end=Snap(to);
        var distance=new float[51];var previous=new int[51];var closed=new bool[51];
        for(int i=0;i<51;i++){distance[i]=float.PositiveInfinity;previous[i]=-1;}distance[49]=0f;
        for(int visit=0;visit<51;visit++)
        {
            int current=-1;float best=float.PositiveInfinity;
            for(int i=0;i<51;i++)if(!closed[i] && distance[i]<best){best=distance[i];current=i;}
            if(current<0 || current==50)break;closed[current]=true;
            Vector3 a=current==49?start:Node(current);
            for(int next=0;next<51;next++)
            {
                if(next==current || next==49 || closed[next])continue;
                Vector3 b=next==50?end:Node(next);
                bool aligned=Mathf.Abs(a.x-b.x)<0.01f || Mathf.Abs(a.z-b.z)<0.01f;
                bool adjacent=current==49 || next==50 || (Mathf.Abs(current%7-next%7)+Mathf.Abs(current/7-next/7)==1);
                if(!aligned || !adjacent)continue;
                float cost=best+Vector3.Distance(a,b);
                if(cost<distance[next]){distance[next]=cost;previous[next]=current;}
            }
        }
        var path=new List<Vector3>();int cursor=50;
        for(int i=0;i<52 && cursor>=0;i++){path.Add(cursor==50?end:cursor==49?start:Node(cursor));cursor=previous[cursor];}
        path.Reverse();return path;
    }
}
