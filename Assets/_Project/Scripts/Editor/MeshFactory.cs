using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RoadSafety.EditorTools
{
    /// <summary>Generates simple soft meshes for the level (rounded boxes for the cars: "véhicules arrondis").</summary>
    public static class MeshFactory
    {
        /// <summary>Box of the given size with rounded edges and corners (smooth normals).</summary>
        public static Mesh RoundedBox(string name, Vector3 size, float radius, int segments = 5)
        {
            Vector3 h = size * 0.5f;
            radius = Mathf.Clamp(radius, 0.001f, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) - 0.001f);
            Vector3 inner = h - Vector3.one * radius;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            float[] Coords(float half)
            {
                var list = new List<float>();
                for (int i = 0; i <= segments; i++)
                    list.Add(-half + radius * i / segments);
                for (int i = 0; i <= segments; i++)
                    list.Add(half - radius + radius * i / segments);
                return list.ToArray();
            }

            float[] cx = Coords(h.x), cy = Coords(h.y), cz = Coords(h.z);

            void Face(Vector3 normal, float[] us, float[] vs, Func<float, float, Vector3> point)
            {
                int start = vertices.Count;
                for (int i = 0; i < us.Length; i++)
                {
                    for (int j = 0; j < vs.Length; j++)
                    {
                        Vector3 p = point(us[i], vs[j]);
                        Vector3 core = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                        Vector3 n = p - core;
                        n = n.sqrMagnitude > 1e-10f ? n.normalized : normal;
                        vertices.Add(core + n * radius);
                        normals.Add(n);
                        uvs.Add(new Vector2((float)i / (us.Length - 1), (float)j / (vs.Length - 1)));
                    }
                }
                // Winding: Unity draws a triangle (a, b, c) from the side where cross(b - a, c - a) points.
                Vector3 eu = point(us[1], vs[0]) - point(us[0], vs[0]);
                Vector3 ev = point(us[0], vs[1]) - point(us[0], vs[0]);
                bool flip = Vector3.Dot(Vector3.Cross(ev, eu), normal) < 0f;
                int rowLength = vs.Length;
                for (int i = 0; i < us.Length - 1; i++)
                {
                    for (int j = 0; j < vs.Length - 1; j++)
                    {
                        int a = start + i * rowLength + j, b = a + 1, c = a + rowLength, d = c + 1;
                        if (!flip)
                            triangles.AddRange(new[] { a, b, c, c, b, d });
                        else
                            triangles.AddRange(new[] { a, c, b, c, d, b });
                    }
                }
            }

            Face(Vector3.right, cy, cz, (u, v) => new Vector3(h.x, u, v));
            Face(Vector3.left, cy, cz, (u, v) => new Vector3(-h.x, u, v));
            Face(Vector3.up, cx, cz, (u, v) => new Vector3(u, h.y, v));
            Face(Vector3.down, cx, cz, (u, v) => new Vector3(u, -h.y, v));
            Face(Vector3.forward, cx, cy, (u, v) => new Vector3(u, v, h.z));
            Face(Vector3.back, cx, cy, (u, v) => new Vector3(u, v, -h.z));

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>Saves the mesh as an asset, keeping the same asset (and GUID) when it already exists.</summary>
        public static Mesh Save(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = System.IO.Path.GetFileNameWithoutExtension(path);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            mesh.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
