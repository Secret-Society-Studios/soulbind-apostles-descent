using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SoulBind.UI
{
    /// <summary>
    /// Turns a rock sprite into a solid 3D rock: its silhouette is given thickness in stacked layers, each
    /// narrower and thicker than the last, so the rock bulges towards its middle. Every face shows the
    /// sprite's own pixels. The rock stands on z = 0 and rises towards -Z, wide along X and thick along Y,
    /// centred on X and Y, at the sprite's own world size.
    ///
    /// The island shader writes no depth, so triangles simply paint over each other in the order they are
    /// listed. Each layer therefore lists its walls before its faces, and the thicker layers come last.
    /// </summary>
    public static class SpriteRockMesh
    {
        private const int Layers = 4;
        private const float LayerInset = 0.07f;  // how much narrower each layer is, as a share of the rock's size
        private const float BaseDepth = 0.1f;    // half the thinnest layer's thickness, as a share too
        private const float LayerDepth = 0.07f;  // half the thickness each layer adds, as a share too
        private const float UvInset = 0.02f;     // of a pixel: stays off its edge, so Point filter never shows the next one
        private const float WallLight = 0.6f;
        private const float TopWallLight = 0.85f;
        private const float BottomWallLight = 0.45f;
        private const float OuterFaceLight = 0.8f;

        // Reused while a mesh is being built.
        private static readonly List<Vector3> Vertices = new List<Vector3>(4096);
        private static readonly List<Vector2> Uvs = new List<Vector2>(4096);
        private static readonly List<Color> Colors = new List<Color>(4096);
        private static readonly List<int> Triangles = new List<int>(8192);

        /// <summary>Builds the rock. shade is how dark its foot gets: 1 = no darkening.</summary>
        public static Mesh Build(Sprite sprite, float shade)
        {
            Rect rect = sprite.textureRect;
            int width = Mathf.RoundToInt(rect.width);
            int height = Mathf.RoundToInt(rect.height);
            bool[] solid = ReadSilhouette(sprite, width, height);

            Crop(solid, width, height, out int left, out int right, out int bottom, out int top);
            var shape = new Shape
            {
                Width = width,
                Height = height,
                Bottom = bottom,
                Top = top,
                CentreX = (left + right + 1) / 2f,
                PixelSize = 1f / sprite.pixelsPerUnit,
                OriginU = rect.x / sprite.texture.width,
                OriginV = rect.y / sprite.texture.height,
                StepU = 1f / sprite.texture.width,
                StepV = 1f / sprite.texture.height,
                Shade = shade
            };

            Vertices.Clear();
            Uvs.Clear();
            Colors.Clear();
            Triangles.Clear();

            int[] depthIn = DistanceFromEdge(solid, width, height, bottom);
            int size = Mathf.Max(right - left + 1, top - bottom + 1);
            float inset = Mathf.Max(2f, size * LayerInset);
            float baseDepth = Mathf.Max(2f, size * BaseDepth);
            float layerDepth = Mathf.Max(1f, size * LayerDepth);

            for (int layer = 0; layer < Layers; layer++)
            {
                int minimum = Mathf.RoundToInt(layer * inset);
                float thickness = (baseDepth + layer * layerDepth) * shape.PixelSize;
                float light = Mathf.Lerp(OuterFaceLight, 1f, layer / (Layers - 1f));
                AddWalls(shape, depthIn, minimum, thickness);
                AddFaces(shape, depthIn, minimum, thickness, light);
            }

            var mesh = new Mesh { name = "SpriteRock " + sprite.name };
            if (Vertices.Count > ushort.MaxValue)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(Vertices);
            mesh.SetUVs(0, Uvs);
            mesh.SetColors(Colors);
            mesh.SetTriangles(Triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // Everything the face builders need to place a pixel, measured in pixels from the sprite's corner.
        private struct Shape
        {
            public int Width, Height, Bottom, Top;
            public float CentreX, PixelSize, OriginU, OriginV, StepU, StepV, Shade;

            public float X(int pixel) => (pixel - CentreX) * PixelSize;
            public float Z(int pixel) => -(pixel - Bottom) * PixelSize;
            public float U(float pixel) => OriginU + pixel * StepU;
            public float V(float pixel) => OriginV + pixel * StepV;

            // Light on a face at this pixel height: darker towards the foot.
            public Color Lit(int pixel, float light)
            {
                float share = (pixel - Bottom) / Mathf.Max(1f, Top + 1 - Bottom);
                float value = Mathf.Lerp(Shade, 1f, share) * light;
                return new Color(value, value, value, 1f);
            }
        }

        // ---------- Reading the sprite ----------

        // Which of the sprite's pixels are rock, first row at the bottom. Goes through a render texture
        // so the art needs no Read/Write import setting.
        private static bool[] ReadSilhouette(Sprite sprite, int width, int height)
        {
            Texture2D texture = sprite.texture;
            Rect rect = sprite.textureRect;
            RenderTexture copy = RenderTexture.GetTemporary(texture.width, texture.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(texture, copy);
            RenderTexture.active = copy;

            var readable = new Texture2D(width, height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(rect.x, rect.y, width, height), 0, 0, false);
            readable.Apply(false);
            Color32[] pixels = readable.GetPixels32();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(copy);
            Object.Destroy(readable);

            var solid = new bool[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
                solid[i] = pixels[i].a > 127;
            return solid;
        }

        // The box around the solid pixels: slices usually keep a clear border, which would lift the rock
        // off the ground.
        private static void Crop(bool[] solid, int width, int height, out int left, out int right, out int bottom,
            out int top)
        {
            left = width;
            right = -1;
            bottom = height;
            top = -1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!solid[y * width + x])
                        continue;
                    left = Mathf.Min(left, x);
                    right = Mathf.Max(right, x);
                    bottom = Mathf.Min(bottom, y);
                    top = Mathf.Max(top, y);
                }
            }
        }

        // How many steps each solid pixel is from the silhouette's edge (0 = not rock, 1 = on the edge).
        // The ground is not an edge, so every layer reaches down to it.
        private static int[] DistanceFromEdge(bool[] solid, int width, int height, int bottom)
        {
            var distance = new int[solid.Length];
            int far = width + height;
            for (int i = 0; i < solid.Length; i++)
                distance[i] = solid[i] ? far : 0;

            for (int y = bottom; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    if (distance[i] == 0)
                        continue;
                    int fromLeft = x > 0 ? distance[i - 1] : 0;
                    int fromBelow = y > bottom ? distance[i - width] : far;
                    distance[i] = Mathf.Min(distance[i], Mathf.Min(fromLeft, fromBelow) + 1);
                }
            }
            for (int y = height - 1; y >= bottom; y--)
            {
                for (int x = width - 1; x >= 0; x--)
                {
                    int i = y * width + x;
                    if (distance[i] == 0)
                        continue;
                    int fromRight = x < width - 1 ? distance[i + 1] : 0;
                    int fromAbove = y < height - 1 ? distance[i + width] : 0;
                    distance[i] = Mathf.Min(distance[i], Mathf.Min(fromRight, fromAbove) + 1);
                }
            }
            return distance;
        }

        // ---------- Building a layer ----------

        // True when the pixel belongs to the layer.
        private static bool InLayer(in Shape shape, int[] depthIn, int minimum, int x, int y) =>
            x >= 0 && x < shape.Width && y >= 0 && y < shape.Height && depthIn[y * shape.Width + x] > minimum;

        // The sides of the layer: one wall along every edge of its outline, joined along each row.
        private static void AddWalls(in Shape shape, int[] depthIn, int minimum, float thickness)
        {
            for (int y = shape.Bottom; y <= shape.Top; y++)
            {
                for (int x = 0; x < shape.Width; x++)
                {
                    if (!InLayer(shape, depthIn, minimum, x, y))
                        continue;
                    if (!InLayer(shape, depthIn, minimum, x - 1, y))
                        AddSideWall(shape, x, x, y, thickness, Vector3.left);
                    if (!InLayer(shape, depthIn, minimum, x + 1, y))
                        AddSideWall(shape, x, x + 1, y, thickness, Vector3.right);
                }

                AddLevelWalls(shape, depthIn, minimum, thickness, y, y + 1, y + 1, TopWallLight, Vector3.back);
                if (y > shape.Bottom)
                    AddLevelWalls(shape, depthIn, minimum, thickness, y, y - 1, y, BottomWallLight, Vector3.forward);
            }
        }

        // An upright wall at the left or right of one pixel, showing that pixel.
        private static void AddSideWall(in Shape shape, int pixelX, int edgeX, int y, float thickness, Vector3 outward)
        {
            float x = shape.X(edgeX);
            float low = shape.Z(y);
            float high = shape.Z(y + 1);
            Vector2 uv = new Vector2(shape.U(pixelX + 0.5f), shape.V(y + 0.5f));
            AddQuad(new Vector3(x, -thickness, low), new Vector3(x, thickness, low),
                new Vector3(x, thickness, high), new Vector3(x, -thickness, high),
                uv, uv, uv, uv, shape.Lit(y, WallLight), shape.Lit(y + 1, WallLight), outward);
        }

        // The flat walls above or below a row: every stretch of its pixels with no layer beside them there.
        private static void AddLevelWalls(in Shape shape, int[] depthIn, int minimum, float thickness, int y,
            int neighbourY, int edgeY, float light, Vector3 outward)
        {
            float z = shape.Z(edgeY);
            float v = shape.V(y + 0.5f);
            Color color = shape.Lit(edgeY, light);

            int x = 0;
            while (x < shape.Width)
            {
                if (!InLayer(shape, depthIn, minimum, x, y) || InLayer(shape, depthIn, minimum, x, neighbourY))
                {
                    x++;
                    continue;
                }
                int start = x;
                while (x < shape.Width && InLayer(shape, depthIn, minimum, x, y)
                       && !InLayer(shape, depthIn, minimum, x, neighbourY))
                    x++;

                float a = shape.X(start), b = shape.X(x);
                var uvA = new Vector2(shape.U(start + UvInset), v);
                var uvB = new Vector2(shape.U(x - UvInset), v);
                AddQuad(new Vector3(a, -thickness, z), new Vector3(b, -thickness, z),
                    new Vector3(b, thickness, z), new Vector3(a, thickness, z),
                    uvA, uvB, uvB, uvA, color, color, outward);
            }
        }

        // The front and back of the layer, one strip per run of pixels in each row. The camera only ever
        // sees one of the two: the other faces away and is skipped.
        private static void AddFaces(in Shape shape, int[] depthIn, int minimum, float thickness, float light)
        {
            for (int y = shape.Bottom; y <= shape.Top; y++)
            {
                float low = shape.Z(y);
                float high = shape.Z(y + 1);
                float vLow = shape.V(y + UvInset);
                float vHigh = shape.V(y + 1 - UvInset);
                Color lowColor = shape.Lit(y, light);
                Color highColor = shape.Lit(y + 1, light);

                int x = 0;
                while (x < shape.Width)
                {
                    if (!InLayer(shape, depthIn, minimum, x, y))
                    {
                        x++;
                        continue;
                    }
                    int start = x;
                    while (x < shape.Width && InLayer(shape, depthIn, minimum, x, y))
                        x++;

                    float a = shape.X(start), b = shape.X(x);
                    var uvA = new Vector2(shape.U(start + UvInset), vLow);
                    var uvB = new Vector2(shape.U(x - UvInset), vLow);
                    var uvC = new Vector2(shape.U(x - UvInset), vHigh);
                    var uvD = new Vector2(shape.U(start + UvInset), vHigh);

                    AddQuad(new Vector3(a, -thickness, low), new Vector3(b, -thickness, low),
                        new Vector3(b, -thickness, high), new Vector3(a, -thickness, high),
                        uvA, uvB, uvC, uvD, lowColor, highColor, Vector3.down);
                    AddQuad(new Vector3(a, thickness, low), new Vector3(b, thickness, low),
                        new Vector3(b, thickness, high), new Vector3(a, thickness, high),
                        uvA, uvB, uvC, uvD, lowColor, highColor, Vector3.up);
                }
            }
        }

        // One quad facing outward. a and b are its low corners, c and d the high ones.
        private static void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uvA, Vector2 uvB, Vector2 uvC,
            Vector2 uvD, Color low, Color high, Vector3 outward)
        {
            int first = Vertices.Count;
            Vertices.Add(a);
            Vertices.Add(b);
            Vertices.Add(c);
            Vertices.Add(d);
            Uvs.Add(uvA);
            Uvs.Add(uvB);
            Uvs.Add(uvC);
            Uvs.Add(uvD);
            Colors.Add(low);
            Colors.Add(low);
            Colors.Add(high);
            Colors.Add(high);

            // Same rule as IslandMesh: flip the corners when the quad would face inward.
            bool flip = Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f;
            Triangles.Add(first);
            Triangles.Add(first + (flip ? 2 : 1));
            Triangles.Add(first + (flip ? 1 : 2));
            Triangles.Add(first);
            Triangles.Add(first + (flip ? 3 : 2));
            Triangles.Add(first + (flip ? 2 : 3));
        }
    }
}
