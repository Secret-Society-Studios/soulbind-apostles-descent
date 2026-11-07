using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace SoulBind.UI
{
    /// <summary>
    /// A card drawn the way the Collections' 3D card looks, inside the UI: rounded corners, a coloured
    /// edge with real thickness, a soft shadow and the moving gloss from the CardFaceUi shader. It is a
    /// single UI mesh, so hands, piles and flights need no camera or render texture of their own.
    /// The sizes are shares of the card's width, so they match the 3D card at any size.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CardBody : MaskableGraphic
    {
        /// <summary>The corner radius, as a share of the card's width (the 3D card's).</summary>
        public const float CornerShare = 0.06f;

        private const float ThicknessShare = 0.02f; // the 3D card's thickness
        private const float RimShare = 0.003f;      // the edge showing around a card seen face on
        private const float SoftEdge = 0.75f;       // canvas pixels: smooths the outline, there is no MSAA
        private const float ShadowSoftShare = 0.05f;
        private const float WallDarkness = 0.65f;   // the side of the card, in shade
        private const int CornerSteps = 6;
        private const int OutlinePoints = 4 * (CornerSteps + 1);

        private static readonly Vector2 ShadowOffsetShare = new Vector2(0.012f, -0.03f);
        private static readonly Color32 ShadowColor = new Color32(0, 0, 0, 110);
        private static readonly Vector2 SolidUv = new Vector2(-1f, -1f); // the shader's "no art here"

        private static Material _material;

        // Serialized so copies of a card back keep them.
        [SerializeField] private Sprite art;
        [SerializeField] private Color edgeColor = UiKit.Gold;

        private readonly Vector2[] _corners = new Vector2[OutlinePoints];
        private readonly Vector2[] _outward = new Vector2[OutlinePoints];
        private Rect _rect;
        private Vector4 _artUv;

        /// <summary>Adds a body filling parent, at this place among parent's children.</summary>
        public static CardBody Create(Transform parent, int siblingIndex)
        {
            RectTransform rect = UiKit.Stretch(UiKit.CreateRect("CardBody", parent));
            rect.SetSiblingIndex(siblingIndex);
            CardBody body = rect.gameObject.AddComponent<CardBody>();
            body.raycastTarget = false;
            if (_material == null)
                _material = new Material(Resources.Load<Shader>("CardFaceUi")) { name = "CardFaceUi" };
            body.material = _material;
            return body;
        }

        /// <summary>The art filling the card's face.</summary>
        public void Show(Sprite sprite)
        {
            if (art == sprite)
                return;
            art = sprite;
            SetAllDirty();
        }

        /// <summary>The colour of the card's edge.</summary>
        public void SetEdge(Color color)
        {
            edgeColor = color;
            SetVerticesDirty();
        }

        public override Texture mainTexture => art != null ? art.texture : s_WhiteTexture;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            _rect = GetPixelAdjustedRect();
            float width = _rect.width;
            if (width <= 0f || _rect.height <= 0f)
                return;
            BuildOutline(width * CornerShare);
            _artUv = art != null ? DataUtility.GetOuterUV(art) : new Vector4(0f, 0f, 1f, 1f);

            float depth = width * ThicknessShare; // +z is behind the face
            float rim = width * RimShare;
            Vector2 shadow = ShadowOffsetShare * width;
            float soft = width * ShadowSoftShare;
            Color32 edge = edgeColor;
            Color32 wall = Color.Lerp(Color.black, edgeColor, WallDarkness);
            Color32 clearEdge = Clear(edge);

            // Back to front: the UI draws in order, with no depth test.
            AddFill(mesh, 0f, shadow, depth, ShadowColor);
            AddBand(mesh, 0f, depth, ShadowColor, soft, depth, Clear(ShadowColor), shadow);
            AddBand(mesh, rim, depth, edge, rim + SoftEdge, depth, clearEdge, Vector2.zero);
            AddFill(mesh, rim, Vector2.zero, depth, edge);
            AddBand(mesh, rim, depth, wall, rim, 0f, wall, Vector2.zero);
            AddBand(mesh, rim, 0f, edge, rim + SoftEdge, 0f, clearEdge, Vector2.zero);
            AddFill(mesh, rim, Vector2.zero, 0f, edge);
            AddFill(mesh, 0f, Vector2.zero, 0f, color, true);
        }

        private static Color32 Clear(Color32 color)
        {
            color.a = 0;
            return color;
        }

        // The rounded rectangle, as corner circle centres and the way out of each.
        private void BuildOutline(float radius)
        {
            Vector2 middle = _rect.center;
            float x = _rect.width / 2f - radius, y = _rect.height / 2f - radius;
            for (int corner = 0, n = 0; corner < 4; corner++)
            {
                var centre = new Vector2(corner == 0 || corner == 3 ? x : -x, corner < 2 ? y : -y);
                for (int step = 0; step <= CornerSteps; step++, n++)
                {
                    float angle = (corner + (float)step / CornerSteps) * 90f * Mathf.Deg2Rad;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    _corners[n] = middle + centre + direction * radius;
                    _outward[n] = direction;
                }
            }
        }

        // The whole shape, grown by this much. With art, it shows the art instead of a flat colour.
        private void AddFill(VertexHelper mesh, float grow, Vector2 offset, float z, Color32 tint, bool withArt = false)
        {
            int start = mesh.currentVertCount;
            Vector2 middle = _rect.center + offset;
            AddVertex(mesh, middle, z, tint, withArt);
            for (int i = 0; i < OutlinePoints; i++)
                AddVertex(mesh, _corners[i] + _outward[i] * grow + offset, z, tint, withArt);
            for (int i = 0; i < OutlinePoints; i++)
                mesh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % OutlinePoints);
        }

        // A strip following the outline from one loop to another: the card's side, or a soft edge.
        private void AddBand(VertexHelper mesh, float growA, float zA, Color32 tintA, float growB, float zB,
            Color32 tintB, Vector2 offset)
        {
            int start = mesh.currentVertCount;
            for (int i = 0; i < OutlinePoints; i++)
            {
                AddVertex(mesh, _corners[i] + _outward[i] * growA + offset, zA, tintA, false);
                AddVertex(mesh, _corners[i] + _outward[i] * growB + offset, zB, tintB, false);
            }
            for (int i = 0; i < OutlinePoints; i++)
            {
                int a = start + 2 * i, b = a + 1;
                int c = start + 2 * ((i + 1) % OutlinePoints), d = c + 1;
                mesh.AddTriangle(a, c, b);
                mesh.AddTriangle(c, d, b);
            }
        }

        private void AddVertex(VertexHelper mesh, Vector2 point, float z, Color32 tint, bool withArt)
        {
            Vector2 uv = SolidUv;
            if (withArt)
            {
                float u = Mathf.InverseLerp(_rect.xMin, _rect.xMax, point.x);
                float v = Mathf.InverseLerp(_rect.yMin, _rect.yMax, point.y);
                uv = new Vector2(Mathf.Lerp(_artUv.x, _artUv.z, u), Mathf.Lerp(_artUv.y, _artUv.w, v));
            }
            mesh.AddVert(new Vector3(point.x, point.y, z), tint, uv);
        }
    }
}
