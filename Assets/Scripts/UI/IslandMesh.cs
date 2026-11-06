using System.Collections.Generic;
using UnityEngine;

namespace SoulBind.UI
{
    /// <summary>
    /// Builds the 3D floating island and its edge rocks as meshes, once, when the match starts.
    /// The board lies in the XY plane and the camera looks from -Z, so the island's flat top is at
    /// z = 0 and its rock hangs down towards +Z. UVs are in texture space (0 to 1 across the texture).
    /// </summary>
    public static class IslandMesh
    {
        private const int Rings = 6;          // rings of rock between the top and the tip
        private const float TipTaper = 0.14f; // the last ring's size, as a share of the top's
        private const float TipDrop = 1.12f;  // the tip hangs this much lower than the last ring

        /// <summary>
        /// How far the rim reaches at each of `segments` angles, as a share of the island's radius
        /// (about 1). The same seed always gives the same bumpy outline.
        /// </summary>
        public static float[] MakeRim(int segments, float roughness, int seed)
        {
            var random = new System.Random(seed);
            float phaseA = (float)random.NextDouble() * 6.28f;
            float phaseB = (float)random.NextDouble() * 6.28f;
            var rim = new float[segments];
            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                // Two slow waves for the overall shape, plus a little noise for jagged stone.
                float wave = Mathf.Sin(3f * angle + phaseA) * 0.5f + Mathf.Sin(7f * angle + phaseB) * 0.3f;
                float noise = ((float)random.NextDouble() - 0.5f) * 0.4f;
                rim[i] = 1f + roughness * (wave + noise);
            }
            return rim;
        }

        /// <summary>
        /// The island: a flat top showing `top` of the texture, and a rock that tapers down to a tip,
        /// showing `cliff` and getting darker (to `shade`) lower down.
        /// </summary>
        public static Mesh BuildIsland(float radiusX, float radiusY, float depth, float[] rim,
            float roughness, Rect top, Rect cliff, float shade)
        {
            int n = rim.Length;
            var vertices = new List<Vector3>(n * (Rings + 2) + 8);
            var uvs = new List<Vector2>(vertices.Capacity);
            var colors = new List<Color>(vertices.Capacity);
            var triangles = new List<int>(n * (Rings + 2) * 6);
            Color dark = new Color(shade, shade, shade, 1f);

            // ---------- Top ----------
            vertices.Add(Vector3.zero);
            uvs.Add(top.center);
            colors.Add(Color.white);
            // One extra vertex closes the ring, so the cliff's texture has a clean seam.
            for (int i = 0; i <= n; i++)
            {
                float angle = i / (float)n * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                float k = rim[i % n];
                vertices.Add(new Vector3(cos * radiusX * k, sin * radiusY * k, 0f));
                // Stay a little inside the art's top, so the edge never samples empty pixels.
                float art = Mathf.Min(k, 1f) * 0.96f;
                uvs.Add(top.center + new Vector2(cos * art * top.width * 0.5f, sin * art * top.height * 0.5f));
                colors.Add(Color.white);
            }
            // The top goes in its own list and is drawn last: depth is off, so whatever is drawn later
            // wins, and the top must cover any chipped cliff that pokes past the rim.
            var topTriangles = new List<int>(n * 3);
            for (int i = 0; i < n; i++)
                AddTriangle(vertices, topTriangles, 0, 1 + i, 2 + i, Vector3.back);

            // ---------- Rock ----------
            int ringStart = vertices.Count;
            var random = new System.Random(23);
            var jag = new float[n];
            for (int r = 0; r <= Rings; r++)
            {
                float t = r / (float)Rings;
                float taper = Mathf.Lerp(1f, TipTaper, Mathf.Pow(t, 1.5f));
                if (r == 1)
                    taper *= 1.03f; // a lip just under the top
                // The top ring matches the top exactly; lower rings get chipped.
                for (int i = 0; i < n; i++)
                    jag[i] = r == 0 ? 1f : 1f + ((float)random.NextDouble() - 0.5f) * roughness * 2f;

                float halfU = Mathf.Lerp(cliff.width * 0.5f, cliff.width * 0.1f, t);
                float v = Mathf.Lerp(cliff.yMax, cliff.yMin, t);
                Color color = Color.Lerp(Color.white, dark, t);
                for (int i = 0; i <= n; i++)
                {
                    float angle = i / (float)n * Mathf.PI * 2f;
                    float k = rim[i % n] * taper * jag[i % n];
                    vertices.Add(new Vector3(Mathf.Cos(angle) * radiusX * k, Mathf.Sin(angle) * radiusY * k, depth * t));
                    // Across the cliff art and back again, so the texture meets itself at the seam.
                    float across = Mathf.Abs(2f * i / n - 1f) * 2f - 1f;
                    uvs.Add(new Vector2(cliff.center.x + across * halfU, v));
                    colors.Add(color);
                }
            }
            for (int r = 0; r < Rings; r++)
            {
                for (int i = 0; i < n; i++)
                {
                    int a = ringStart + r * (n + 1) + i;
                    int c = a + n + 1;
                    Vector3 outward = Outward(vertices[a]);
                    AddTriangle(vertices, triangles, a, c, a + 1, outward);
                    AddTriangle(vertices, triangles, a + 1, c, c + 1, outward);
                }
            }

            // ---------- Tip ----------
            int last = ringStart + Rings * (n + 1);
            int tip = vertices.Count;
            vertices.Add(new Vector3(0f, 0f, depth * TipDrop));
            uvs.Add(new Vector2(cliff.center.x, cliff.yMin));
            colors.Add(dark);
            for (int i = 0; i < n; i++)
                AddTriangle(vertices, triangles, last + i, tip, last + i + 1, Outward(vertices[last + i]));

            triangles.AddRange(topTriangles);
            return Finish("Island", vertices, uvs, colors, triangles);
        }

        /// <summary>
        /// A chunky rock one unit tall (towards -Z, standing on z = 0) and about 0.8 across,
        /// textured from `cliff`. Each seed gives a different rock.
        /// </summary>
        public static Mesh BuildRock(int seed, Rect cliff, float shade)
        {
            const int Sides = 7;
            var random = new System.Random(seed);
            var vertices = new List<Vector3>(Sides * 2 + 4);
            var uvs = new List<Vector2>(vertices.Capacity);
            var colors = new List<Color>(vertices.Capacity);
            var triangles = new List<int>(Sides * 12);
            Color dark = new Color(shade, shade, shade, 1f);
            Vector3 middle = new Vector3(0f, 0f, -0.45f);

            // A base ring a bit into the ground, a narrower ring part way up, and a leaning peak.
            float lean = (float)random.NextDouble() * 6.28f;
            Vector3 peak = new Vector3(Mathf.Cos(lean) * 0.12f, Mathf.Sin(lean) * 0.12f, -1f);
            for (int ring = 0; ring < 2; ring++)
            {
                float radius = ring == 0 ? 0.42f : 0.3f;
                float z = ring == 0 ? 0.1f : -0.6f;
                for (int i = 0; i < Sides; i++)
                {
                    float angle = (i + (float)random.NextDouble() * 0.5f) / Sides * Mathf.PI * 2f;
                    float r = radius * (0.75f + (float)random.NextDouble() * 0.5f);
                    Vector3 p = new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, z);
                    if (ring == 1)
                        p += new Vector3(peak.x, peak.y, 0f) * 0.5f;
                    vertices.Add(p);
                    uvs.Add(new Vector2(cliff.x + cliff.width * (0.3f + 0.4f * i / Sides),
                        Mathf.Lerp(cliff.yMin, cliff.yMax, ring == 0 ? 0.25f : 0.65f)));
                    colors.Add(ring == 0 ? dark : Color.Lerp(dark, Color.white, 0.7f));
                }
            }
            int top = vertices.Count;
            vertices.Add(peak);
            uvs.Add(new Vector2(cliff.center.x, cliff.yMax - cliff.height * 0.1f));
            colors.Add(Color.white);
            int bottom = vertices.Count;
            vertices.Add(new Vector3(0f, 0f, 0.15f));
            uvs.Add(new Vector2(cliff.center.x, cliff.yMin));
            colors.Add(dark);

            for (int i = 0; i < Sides; i++)
            {
                int next = (i + 1) % Sides;
                int a = i, b = next, c = Sides + i, d = Sides + next;
                AddTriangle(vertices, triangles, a, c, b, Away(vertices, middle, a, c, b));
                AddTriangle(vertices, triangles, b, c, d, Away(vertices, middle, b, c, d));
                AddTriangle(vertices, triangles, c, top, d, Away(vertices, middle, c, top, d));
                AddTriangle(vertices, triangles, a, b, bottom, Away(vertices, middle, a, b, bottom));
            }
            return Finish("Rock", vertices, uvs, colors, triangles);
        }

        /// <summary>A rect given as 0 to 1 across a sprite, turned into 0 to 1 across its texture.</summary>
        public static Rect ToTexture(Sprite sprite, Rect area)
        {
            Texture2D texture = sprite.texture;
            Rect rect = sprite.textureRect;
            return new Rect(
                (rect.x + area.x * rect.width) / texture.width,
                (rect.y + area.y * rect.height) / texture.height,
                area.width * rect.width / texture.width,
                area.height * rect.height / texture.height);
        }

        // ---------- Helpers ----------

        // Away from the island's centre line, level with the board.
        private static Vector3 Outward(Vector3 point) => new Vector3(point.x, point.y, 0f);

        // Points from the island's middle to a triangle's centre, to tell which way it faces.
        private static Vector3 Away(List<Vector3> vertices, Vector3 middle, int a, int b, int c) =>
            (vertices[a] + vertices[b] + vertices[c]) / 3f - middle;

        // Unity draws a triangle's front when its corners go clockwise as seen by the camera; the back
        // is skipped. Flip the corners if the triangle would face inward.
        private static void AddTriangle(List<Vector3> vertices, List<int> triangles, int a, int b, int c,
            Vector3 outward)
        {
            Vector3 normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (Vector3.Dot(normal, outward) < 0f)
            {
                int swap = b;
                b = c;
                c = swap;
            }
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        // Packs the lists into a mesh and works out its bounds.
        private static Mesh Finish(string name, List<Vector3> vertices, List<Vector2> uvs, List<Color> colors,
            List<int> triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
