#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class CityLOD
{
    const string Folder="Assets/Generated/LOD";
    public static void PrepareImports()
    {
        string[] roots={"Assets/ThirdParty/Kenney/CarKit/Models/FBX format","Assets/ThirdParty/Kenney/CityCommercial/Models/FBX format","Assets/ThirdParty/Kenney/CitySuburban/Models/FBX format","Assets/HDRPLab/Scenes/HDRPLabController/Models"};
        foreach(string guid in AssetDatabase.FindAssets("t:Model",roots))
        {
            var importer=AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as ModelImporter;
            if(importer==null || importer.isReadable)continue;importer.isReadable=true;importer.SaveAndReimport();
        }
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Generated","LOD");
    }
    public static void Add(GameObject root)
    {
        var originals=root.GetComponentsInChildren<MeshRenderer>();
        if(originals.Length==0)return;
        var low=new List<Renderer>();
        foreach(var renderer in originals)
        {
            var filter=renderer.GetComponent<MeshFilter>();
            if(filter==null || filter.sharedMesh==null){low.Add(renderer);continue;}
            Mesh source=filter.sharedMesh;
            if(!source.isReadable || source.vertexCount<120){low.Add(renderer);continue;}
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out string guid,out long localId);
            string path=Folder+"/"+guid+"_"+localId+".asset";
            Mesh simplified=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(simplified==null){simplified=Simplify(source);AssetDatabase.CreateAsset(simplified,path);}
            if(simplified.vertexCount>=source.vertexCount*0.85f){low.Add(renderer);continue;}
            var go=new GameObject(renderer.name+"_LOD1");go.transform.SetParent(renderer.transform,false);
            go.AddComponent<MeshFilter>().sharedMesh=simplified;var r=go.AddComponent<MeshRenderer>();r.sharedMaterials=renderer.sharedMaterials;low.Add(r);
        }
        if(low.Count==0)return;
        var group=root.GetComponent<LODGroup>();if(group==null)group=root.AddComponent<LODGroup>();
        group.fadeMode=LODFadeMode.None;
        group.SetLODs(new[]{new LOD(0.09f,originals),new LOD(0.012f,low.ToArray())});group.RecalculateBounds();
    }
    static Mesh Simplify(Mesh source)
    {
        Vector3[] vertices=source.vertices;Vector2[] uv=source.uv;
        float cell=Mathf.Max(source.bounds.size.x,source.bounds.size.y,source.bounds.size.z)/24f;
        var clusters=new Dictionary<Vector3Int,int>();var sums=new List<Vector3>();var uvSums=new List<Vector2>();var counts=new List<int>();var map=new int[vertices.Length];
        for(int i=0;i<vertices.Length;i++)
        {
            Vector3 p=vertices[i]/Mathf.Max(cell,0.00001f);var key=new Vector3Int(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.y),Mathf.RoundToInt(p.z));
            if(!clusters.TryGetValue(key,out int index)){index=sums.Count;clusters.Add(key,index);sums.Add(Vector3.zero);uvSums.Add(Vector2.zero);counts.Add(0);}
            map[i]=index;sums[index]+=vertices[i];if(uv.Length==vertices.Length)uvSums[index]+=uv[i];counts[index]++;
        }
        for(int i=0;i<sums.Count;i++){sums[i]/=counts[i];uvSums[i]/=counts[i];}
        var mesh=new Mesh{name=source.name+"_LOD1",indexFormat=IndexFormat.UInt32};mesh.SetVertices(sums);mesh.SetUVs(0,uvSums);mesh.subMeshCount=source.subMeshCount;
        for(int s=0;s<source.subMeshCount;s++)
        {
            int[] triangles=source.GetTriangles(s);var reduced=new List<int>();
            for(int i=0;i<triangles.Length;i+=3){int a=map[triangles[i]],b=map[triangles[i+1]],c=map[triangles[i+2]];if(a==b||a==c||b==c)continue;reduced.Add(a);reduced.Add(b);reduced.Add(c);}
            mesh.SetTriangles(reduced,s);
        }
        mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
}
#endif
