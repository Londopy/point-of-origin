using System.Collections.Generic;
using UnityEngine;

namespace PointOfOrigin
{
    /// <summary>
    /// One mesh, rebuilt every frame from boxes with per-face vertex colours.
    /// Flat shading is baked into the colours: the top is the given colour and
    /// the two sides the camera sees are darkened copies.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class WorldView : MonoBehaviour
    {
        public const float SideShadeA = 0.58f;   // the -x face
        public const float SideShadeB = 0.74f;   // the -z face

        readonly List<Vector3> verts = new List<Vector3>(24000);
        readonly List<Color32> cols = new List<Color32>(24000);
        readonly List<int> tris = new List<int>(36000);
        Mesh mesh;

        public void Init(Material material)
        {
            mesh = new Mesh { name = "world", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = GetComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        public void Begin()
        {
            verts.Clear();
            cols.Clear();
            tris.Clear();
        }

        public void Commit()
        {
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0, false);
            mesh.RecalculateBounds();
        }

        public static Color32 Shade(Color32 c, float f) =>
            new Color32((byte)(c.r * f), (byte)(c.g * f), (byte)(c.b * f), 255);

        /// <summary>A clockwise quad seen from its front.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 color)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            cols.Add(color); cols.Add(color); cols.Add(color); cols.Add(color);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        /// <summary>An axis-aligned box from (x0,y0,z0) to (x1,y1,z1) without a bottom face.</summary>
        public void Box(float x0, float y0, float z0, float x1, float y1, float z1, Color32 top)
        {
            var sideA = Shade(top, SideShadeA);
            var sideB = Shade(top, SideShadeB);
            // top (+y)
            Quad(new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0), top);
            // -z face
            Quad(new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y0, z0), sideB);
            // +z face
            Quad(new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), new Vector3(x0, y0, z1), sideB);
            // -x face
            Quad(new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x0, y1, z0), new Vector3(x0, y0, z0), sideA);
            // +x face
            Quad(new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1), sideA);
        }

        /// <summary>A thin frame lying on top of a box, inset from its edges.</summary>
        public void Rim(float x0, float z0, float x1, float z1, float y, float thickness, Color32 color)
        {
            float e = 0.004f;
            Box(x0, y - e, z0, x1, y + e, z0 + thickness, color);
            Box(x0, y - e, z1 - thickness, x1, y + e, z1, color);
            Box(x0, y - e, z0 + thickness, x0 + thickness, y + e, z1 - thickness, color);
            Box(x1 - thickness, y - e, z0 + thickness, x1, y + e, z1 - thickness, color);
        }
    }
}
