using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Rendering/Add Barycentric (Wireframe)")]
public class AddBarycentric : MonoBehaviour
{
    public bool applyOnAwake = true;

    void Awake(){ if (applyOnAwake) Apply(); }

    [ContextMenu("Apply Now")]
    public void Apply()
    {
        var mf = GetComponent<MeshFilter>();
        var smr = GetComponent<SkinnedMeshRenderer>();
        Mesh src = null;

        if (mf && mf.sharedMesh) src = mf.sharedMesh;
        if (!src && smr && smr.sharedMesh) src = smr.sharedMesh;
        if (!src){ Debug.LogWarning("[AddBarycentric] Nessuna mesh trovata."); return; }

        Mesh dst = BuildBarycentricMesh(src);

        if (mf)  mf.mesh  = dst;        // istanza
        if (smr) smr.sharedMesh = dst;  // ok: copia bindposes/weights
    }

    Mesh BuildBarycentricMesh(Mesh src)
    {
        src.RecalculateBounds();

        var triangles = src.triangles;
        var v0 = src.vertices;
        var n0 = src.normals;
        var t0 = src.tangents;
        var u0 = src.uv;
        var u1 = src.uv2;
        var u2 = src.uv3;
        var u3 = src.uv4;
        var bw = src.boneWeights;
        var bp = src.bindposes;

        int triCount = triangles.Length / 3;
        int newVertCount = triCount * 3;

        var nv  = new Vector3[newVertCount];
        Vector3[] nn = (n0 != null && n0.Length == v0.Length) ? new Vector3[newVertCount] : null;
        Vector4[] nt = (t0 != null && t0.Length == v0.Length) ? new Vector4[newVertCount] : null;
        Vector2[] nu0= (u0 != null && u0.Length == v0.Length) ? new Vector2[newVertCount] : null;
        Vector2[] nu1= (u1 != null && u1.Length == v0.Length) ? new Vector2[newVertCount] : null;
        Vector2[] nu2= (u2 != null && u2.Length == v0.Length) ? new Vector2[newVertCount] : null;
        Vector2[] nu3= (u3 != null && u3.Length == v0.Length) ? new Vector2[newVertCount] : null;
        BoneWeight[] nbw = (bw != null && bw.Length == v0.Length) ? new BoneWeight[newVertCount] : null;
        var col = new Color[newVertCount];
        var tri = new int[newVertCount];

        int o = 0;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int i0 = triangles[i];
            int i1 = triangles[i+1];
            int i2 = triangles[i+2];

            nv[o+0] = v0[i0]; nv[o+1] = v0[i1]; nv[o+2] = v0[i2];
            if (nn != null){ nn[o+0] = n0[i0]; nn[o+1] = n0[i1]; nn[o+2] = n0[i2]; }
            if (nt != null){ nt[o+0] = t0[i0]; nt[o+1] = t0[i1]; nt[o+2] = t0[i2]; }
            if (nu0!= null){ nu0[o+0]= u0[i0]; nu0[o+1]= u0[i1]; nu0[o+2]= u0[i2]; }
            if (nu1!= null){ nu1[o+0]= u1[i0]; nu1[o+1]= u1[i1]; nu1[o+2]= u1[i2]; }
            if (nu2!= null){ nu2[o+0]= u2[i0]; nu2[o+1]= u2[i1]; nu2[o+2]= u2[i2]; }
            if (nu3!= null){ nu3[o+0]= u3[i0]; nu3[o+1]= u3[i1]; nu3[o+2]= u3[i2]; }
            if (nbw!= null){ nbw[o+0]= bw[i0]; nbw[o+1]= bw[i1]; nbw[o+2]= bw[i2]; }

            // baricentriche: (1,0,0), (0,1,0), (0,0,1)
            col[o+0] = new Color(1,0,0,1);
            col[o+1] = new Color(0,1,0,1);
            col[o+2] = new Color(0,0,1,1);

            tri[o+0] = o+0; tri[o+1] = o+1; tri[o+2] = o+2;
            o += 3;
        }

        var m = new Mesh();
        m.name = src.name + "_Bary";
        m.vertices = nv;
        if (nn != null) m.normals = nn; else m.RecalculateNormals();
        if (nt != null) m.tangents = nt;
        if (nu0!= null) m.uv  = nu0;
        if (nu1!= null) m.uv2 = nu1;
        if (nu2!= null) m.uv3 = nu2;
        if (nu3!= null) m.uv4 = nu3;
        if (nbw!= null) { m.boneWeights = nbw; m.bindposes = bp; }
        m.colors = col;
        m.triangles = tri;
        m.RecalculateBounds();
        return m;
    }
}
