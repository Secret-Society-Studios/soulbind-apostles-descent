using System.Collections.Generic;
using SoulBind.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Kit = SoulBind.UI.UiKit;

namespace SoulBind.UI
{
    /// <summary>
    /// The Collections' preview: one card as a real 3D object, with thickness, turning slowly above its
    /// reflection on the floor. A glossy sheen slides over it as it turns. Drag to spin it (it keeps
    /// spinning, then settles back to a slow turn) and up or down to tip it.
    /// The card has its own small scene, far from everything, and a camera that draws it into a texture
    /// only while this preview is on screen.
    /// </summary>
    public sealed class CardViewer360 : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const float AutoSpin = 25f;      // degrees per second while nobody drags
        private const float DragSpin = 0.45f;    // degrees per pixel dragged sideways
        private const float DragTip = 0.25f;     // degrees per pixel dragged up or down
        private const float MaxTip = 25f;        // the most it tips forward or back
        private const float Settle = 2.5f;       // how fast a flick slows back to the slow turn

        // The card's size in its scene (about the art's 5:7), and how the camera frames it.
        private const float CardWidth = 1f, CardHeight = 1.4f, CardThickness = 0.02f, CornerRadius = 0.06f;
        private const int CornerSteps = 6;
        private const float FloorBelow = 0.8f;   // from the card's middle down to the floor
        private const float CameraDistance = 4.2f, CameraDrop = 0.3f, CameraFieldOfView = 28f;
        private const int TextureScale = 2;      // texture pixels per design pixel
        private const int RigLayer = 31;         // nothing else uses it, so no other camera draws the card
        private static readonly Vector3 RigPosition = new Vector3(0f, 2000f, 0f);
        private static readonly int MirrorId = Shader.PropertyToID("_Mirror");
        private static readonly int FloorId = Shader.PropertyToID("_FloorY");

        private GameObject _rig;
        private Camera _camera;
        private RenderTexture _texture;
        private Mesh _mesh;
        private Transform _card;
        private Transform _reflection;
        private Material _front;
        private Material _back;
        private Material _edge;
        private float _yaw;      // degrees around the card's upright axis; 0 = front facing you
        private float _tip;      // degrees forward or back
        private float _spin;     // degrees per second
        private bool _dragging;

        /// <summary>Fills box (width x height in design pixels) with the preview.</summary>
        public static CardViewer360 Create(RectTransform box, float inset, Vector2 size)
        {
            RectTransform rect = Kit.Stretch(Kit.CreateRect("CardViewer360", box), inset);
            RawImage image = rect.gameObject.AddComponent<RawImage>();
            CardViewer360 viewer = rect.gameObject.AddComponent<CardViewer360>();
            viewer.Build(image, size - 2f * inset * Vector2.one);
            return viewer;
        }

        /// <summary>Puts a card's front and back art on the card, and resets the spin.</summary>
        public void Show(CardDefinition card)
        {
            CardArtLibrary art = CardArtLibrary.Current;
            SetArt(_front, art != null ? art.GetFront(card.Id) : null);
            SetArt(_back, art != null ? art.Back : null);

            // Every card starts face on, with a little spin so it is clearly turnable.
            _yaw = 0f;
            _tip = 0f;
            _spin = AutoSpin * 4f;
            _dragging = false;
            Apply();
        }

        // A sprite's part of its texture, or a plain card colour for a side with no art.
        private static void SetArt(Material material, Sprite sprite)
        {
            if (sprite == null)
            {
                material.mainTexture = Texture2D.whiteTexture;
                material.mainTextureScale = Vector2.one;
                material.mainTextureOffset = Vector2.zero;
                material.color = Kit.Panel;
                return;
            }
            Texture2D texture = sprite.texture;
            Rect area = sprite.rect;
            material.mainTexture = texture;
            material.mainTextureScale = new Vector2(area.width / texture.width, area.height / texture.height);
            material.mainTextureOffset = new Vector2(area.x / texture.width, area.y / texture.height);
            material.color = Color.white;
        }

        // ---------- Spinning ----------

        private void OnEnable()
        {
            if (_rig != null)
                _rig.SetActive(true);
        }

        private void OnDisable()
        {
            if (_rig != null)
                _rig.SetActive(false);
        }

        private void OnDestroy()
        {
            Destroy(_rig);
            Destroy(_mesh);
            Destroy(_front);
            Destroy(_back);
            Destroy(_edge);
            if (_texture != null)
                _texture.Release();
            Destroy(_texture);
        }

        private void Update()
        {
            float delta = Time.unscaledDeltaTime;
            if (!_dragging)
            {
                // A flick slows down to the slow turn; the tip eases back to upright.
                _spin = Mathf.Lerp(_spin, AutoSpin, 1f - Mathf.Exp(-Settle * delta));
                _yaw += _spin * delta;
                _tip = Mathf.Lerp(_tip, 0f, 1f - Mathf.Exp(-Settle * delta));
            }
            Apply();
        }

        // Turns the card and its reflection.
        private void Apply()
        {
            _yaw = Mathf.Repeat(_yaw, 360f);
            Quaternion turn = Quaternion.Euler(_tip, _yaw, 0f);
            _card.localRotation = turn;
            _reflection.localRotation = turn;
        }

        // Stops the free spin while the player drags.
        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragging = true;
            _spin = 0f;
        }

        // Turns the card with the drag and remembers the speed for the spin after release.
        public void OnDrag(PointerEventData eventData)
        {
            float turn = eventData.delta.x * DragSpin;
            _yaw -= turn;
            _tip = Mathf.Clamp(_tip + eventData.delta.y * DragTip, -MaxTip, MaxTip);
            float delta = Mathf.Max(Time.unscaledDeltaTime, 0.001f);
            _spin = Mathf.Lerp(_spin, -turn / delta, 0.5f); // keeps the flick's speed for the let-go
        }

        // Lets go: the card keeps spinning at the drag speed and slows down.
        public void OnEndDrag(PointerEventData eventData)
        {
            _dragging = false;
            _spin = Mathf.Clamp(_spin, -900f, 900f);
        }

        // ---------- Building ----------

        // Builds the card, its reflection, and the camera that draws them into image.
        private void Build(RawImage image, Vector2 size)
        {
            _rig = new GameObject("CardViewerRig");
            _rig.transform.position = RigPosition;
            _mesh = BuildCardMesh();

            Shader shader = Resources.Load<Shader>("CardGloss");
            _front = new Material(shader);
            _back = new Material(shader);
            _edge = new Material(shader) { color = Kit.Gold };
            var materials = new[] { _front, _back, _edge };

            _card = AddCard("Card", _rig.transform, materials);

            // A mirror image under the floor: flipped upside down around it, and fading away from it.
            float floor = RigPosition.y - FloorBelow;
            var mirror = new GameObject("Mirror").transform;
            mirror.SetParent(_rig.transform, false);
            mirror.localPosition = new Vector3(0f, -FloorBelow, 0f);
            mirror.localScale = new Vector3(1f, -1f, 1f);
            _reflection = AddCard("Reflection", mirror, materials);
            _reflection.localPosition = new Vector3(0f, FloorBelow, 0f);
            var block = new MaterialPropertyBlock();
            block.SetFloat(MirrorId, 1f);
            block.SetFloat(FloorId, floor);
            MeshRenderer reflection = _reflection.GetComponent<MeshRenderer>();
            for (int i = 0; i < materials.Length; i++)
                reflection.SetPropertyBlock(block, i);

            _texture = new RenderTexture(Mathf.RoundToInt(size.x) * TextureScale, Mathf.RoundToInt(size.y) * TextureScale,
                16, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            image.texture = _texture;

            var eye = new GameObject("Camera", typeof(Camera));
            eye.layer = RigLayer;
            eye.transform.SetParent(_rig.transform, false);
            eye.transform.localPosition = new Vector3(0f, -CameraDrop, -CameraDistance);
            _camera = eye.GetComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Kit.Inset; // the preview box's own colour, so the edge never shows
            _camera.fieldOfView = CameraFieldOfView;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 20f;
            _camera.cullingMask = 1 << RigLayer;
            _camera.allowHDR = false;
            _camera.targetTexture = _texture;

            _rig.SetActive(isActiveAndEnabled);
        }

        // One copy of the card mesh.
        private Transform AddCard(string name, Transform parent, Material[] materials)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = RigLayer;
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = _mesh;
            var meshRenderer = go.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = materials;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            return go.transform;
        }

        // A rounded card with thickness, in three parts: front, back and edge.
        private static Mesh BuildCardMesh()
        {
            float halfX = CardWidth / 2f, halfY = CardHeight / 2f, halfZ = CardThickness / 2f;
            float cornerX = halfX - CornerRadius, cornerY = halfY - CornerRadius;

            // The outline, counter-clockwise from the right side of the top-right corner.
            int count = 4 * (CornerSteps + 1);
            var outline = new Vector2[count];
            var outward = new Vector3[count];
            for (int corner = 0, n = 0; corner < 4; corner++)
            {
                var centre = new Vector2(corner == 0 || corner == 3 ? cornerX : -cornerX,
                    corner < 2 ? cornerY : -cornerY);
                for (int step = 0; step <= CornerSteps; step++, n++)
                {
                    float angle = (corner + (float)step / CornerSteps) * 90f * Mathf.Deg2Rad;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    outline[n] = centre + direction * CornerRadius;
                    outward[n] = direction;
                }
            }

            var vertices = new List<Vector3>(4 * count + 2);
            var normals = new List<Vector3>(4 * count + 2);
            var uvs = new List<Vector2>(4 * count + 2);

            // Each face: the centre, then the outline. The back's art is flipped to read right from behind.
            void AddFace(float z, Vector3 normal, bool flip)
            {
                vertices.Add(new Vector3(0f, 0f, z));
                normals.Add(normal);
                uvs.Add(new Vector2(0.5f, 0.5f));
                foreach (Vector2 point in outline)
                {
                    float u = (point.x + halfX) / CardWidth;
                    vertices.Add(new Vector3(point.x, point.y, z));
                    normals.Add(normal);
                    uvs.Add(new Vector2(flip ? 1f - u : u, (point.y + halfY) / CardHeight));
                }
            }

            AddFace(-halfZ, Vector3.back, false);
            var front = new List<int>(3 * count);
            for (int i = 0; i < count; i++)
            {
                front.Add(0);
                front.Add(1 + (i + 1) % count);
                front.Add(1 + i);
            }

            int backStart = vertices.Count;
            AddFace(halfZ, Vector3.forward, true);
            var back = new List<int>(3 * count);
            for (int i = 0; i < count; i++)
            {
                back.Add(backStart);
                back.Add(backStart + 1 + i);
                back.Add(backStart + 1 + (i + 1) % count);
            }

            int edgeStart = vertices.Count;
            for (int i = 0; i < count; i++)
            {
                vertices.Add(new Vector3(outline[i].x, outline[i].y, -halfZ));
                vertices.Add(new Vector3(outline[i].x, outline[i].y, halfZ));
                normals.Add(outward[i]);
                normals.Add(outward[i]);
                uvs.Add(Vector2.zero);
                uvs.Add(Vector2.zero);
            }
            var edge = new List<int>(6 * count);
            for (int i = 0; i < count; i++)
            {
                int a = edgeStart + 2 * i, b = a + 1;
                int c = edgeStart + 2 * ((i + 1) % count), d = c + 1;
                edge.Add(a);
                edge.Add(c);
                edge.Add(b);
                edge.Add(c);
                edge.Add(d);
                edge.Add(b);
            }

            var mesh = new Mesh { name = "Card" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 3;
            mesh.SetTriangles(front, 0);
            mesh.SetTriangles(back, 1);
            mesh.SetTriangles(edge, 2);
            return mesh;
        }
    }
}
