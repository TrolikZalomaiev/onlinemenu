using UnityEngine;

namespace ZombieSurvival
{
    /// <summary>
    /// Генератор низкополигональной земли: плоские грани (у каждого треугольника свои вершины),
    /// ровный центр для посёлка и холмы по краям.
    /// </summary>
    public static class LowPolyTerrain
    {
        public static Mesh Generate(float size, float cellSize, float hillHeight, float flatRadius, int seed)
        {
            int cells = Mathf.CeilToInt(size / cellSize);
            float half = size * 0.5f;
            var rng = new System.Random(seed);
            float ox = (float)rng.NextDouble() * 1000f, oz = (float)rng.NextDouble() * 1000f;

            var heights = new float[cells + 1, cells + 1];
            for (int z = 0; z <= cells; z++)
            for (int x = 0; x <= cells; x++)
            {
                float wx = x * cellSize - half, wz = z * cellSize - half;
                float noise = Mathf.PerlinNoise(ox + wx * 0.025f, oz + wz * 0.025f) * 0.7f
                              + Mathf.PerlinNoise(ox + wx * 0.08f, oz + wz * 0.08f) * 0.3f;
                float dist = Mathf.Sqrt(wx * wx + wz * wz);
                float mask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(flatRadius, flatRadius + 30f, dist));
                float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(half * 0.8f, half, Mathf.Max(Mathf.Abs(wx), Mathf.Abs(wz))));
                float jitter = ((float)rng.NextDouble() - 0.5f) * 0.25f;
                heights[x, z] = noise * hillHeight * mask + edge * hillHeight * 2f + jitter * mask;
            }

            int triCount = cells * cells * 2;
            var vertices = new Vector3[triCount * 3];
            var triangles = new int[triCount * 3];
            int v = 0;
            for (int z = 0; z < cells; z++)
            for (int x = 0; x < cells; x++)
            {
                Vector3 a = P(x, z), b = P(x, z + 1), c = P(x + 1, z + 1), d = P(x + 1, z);
                bool flip = ((x + z) & 1) == 0;
                if (flip) { Tri(a, b, c); Tri(a, c, d); }
                else { Tri(a, b, d); Tri(b, c, d); }
            }

            var mesh = new Mesh { name = "LowPolyGround" };
            mesh.indexFormat = vertices.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;

            Vector3 P(int x, int z) => new Vector3(x * cellSize - half, heights[x, z], z * cellSize - half);

            void Tri(Vector3 p0, Vector3 p1, Vector3 p2)
            {
                vertices[v] = p0; triangles[v] = v; v++;
                vertices[v] = p1; triangles[v] = v; v++;
                vertices[v] = p2; triangles[v] = v; v++;
            }
        }
    }
}
