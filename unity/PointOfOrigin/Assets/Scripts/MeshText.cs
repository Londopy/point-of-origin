using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace PointOfOrigin
{
    /// <summary>
    /// Loads a mesh written by Houdini as a plain triangle list: one vertex per
    /// line, "x y z r g b", three lines per triangle. Winding is fixed so every
    /// face points away from the mesh's centre, whatever the source handedness.
    /// </summary>
    public static class MeshText
    {
        public static Mesh Load(TextAsset text, string name)
        {
            var verts = new List<Vector3>();
            var cols = new List<Color32>();
            var inv = CultureInfo.InvariantCulture;
            foreach (var raw in text.text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                var f = line.Split(' ');
                if (f.Length < 6) continue;
                verts.Add(new Vector3(
                    float.Parse(f[0], inv), float.Parse(f[1], inv), float.Parse(f[2], inv)));
                // Houdini writes linear values; the shader expects sRGB (it converts back), so store the gamma form
                var linear = new Color(float.Parse(f[3], inv), float.Parse(f[4], inv), float.Parse(f[5], inv), 1f);
                cols.Add((Color32)linear.gamma);
            }
            int triCount = verts.Count / 3;
            var centre = Vector3.zero;
            foreach (var v in verts) centre += v;
            if (verts.Count > 0) centre /= verts.Count;

            var tris = new int[triCount * 3];
            for (int t = 0; t < triCount; t++)
            {
                int a = t * 3, b = a + 1, c = a + 2;
                var n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
                var faceCentre = (verts[a] + verts[b] + verts[c]) / 3f;
                bool outward = Vector3.Dot(n, faceCentre - centre) >= 0f;
                tris[a] = a;
                tris[b] = outward ? b : c;
                tris[c] = outward ? c : b;
            }

            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
