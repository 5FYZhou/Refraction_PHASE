using UnityEngine;
using UnityEngine.Rendering;

namespace Refraction.Presentation.Effects
{
    /// <summary>弹反范围高亮：把碰撞体形状烘成半透明红色网格，弹反成功时亮一下再淡出</summary>
    /// <remarks>网格顶点在碰撞体局部空间、并挂成它的子物体，所以转身与缩放自动跟随；
    /// 材质每实例一份，否则一只亮会让同屏所有敌人一起亮</remarks>
    public sealed class ReboundFlash
    {
        private const string ShaderName = "Sprites/Default";
        private const string FallbackShaderName = "Unlit/Color";
        private const string FlashName = "ReboundFlash";
        private const int SortingOrder = 30000;

        private static readonly Color FlashColor = new(1f, 0.15f, 0.15f, 0.55f);

        private readonly float _duration;
        private readonly GameObject _flash;
        private readonly Mesh _mesh;
        private readonly Material _material;
        private readonly MeshRenderer _renderer;

        private float _left;

        public ReboundFlash(Collider2D zone, float duration)
        {
            _duration = Mathf.Max(0.01f, duration);

            if (zone == null) return;

            _mesh = BuildMesh(zone);
            if (_mesh == null) return;

            Shader shader = Shader.Find(ShaderName);
            if (shader == null) shader = Shader.Find(FallbackShaderName);

            if (shader == null)
            {
                Debug.LogWarning($"[ReboundFlash] 找不到 {ShaderName} 与 {FallbackShaderName}：弹反高亮不会显示。");
                return;
            }

            _mesh.hideFlags = HideFlags.HideAndDontSave;

            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, color = FlashColor };

            if (_material.HasProperty("_MainTex")) _material.mainTexture = Texture2D.whiteTexture;

            _flash = new GameObject(FlashName);
            _flash.transform.SetParent(zone.transform, false);
            _flash.AddComponent<MeshFilter>().sharedMesh = _mesh;

            _renderer = _flash.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _material;
            _renderer.sortingOrder = SortingOrder;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;

            _flash.SetActive(false);
        }

        public void Play()
        {
            if (_flash == null) return;

            _left = _duration;

            ApplyAlpha(1f);
            _flash.SetActive(true);
        }

        /// <summary>按渲染或物理帧推进淡出：只能由控制器驱动，本工程不许自带 Update</summary>
        public void Tick(float deltaTime)
        {
            if (_flash == null || _left <= 0f) return;

            _left -= deltaTime;

            if (_left <= 0f)
            {
                _flash.SetActive(false);
                return;
            }

            ApplyAlpha(_left / _duration);
        }

        public void Dispose()
        {
            Kill(_flash);
            Kill(_mesh);
            Kill(_material);
        }

        private void ApplyAlpha(float scale)
        {
            if (_material == null) return;

            Color color = FlashColor;
            color.a *= scale;

            _material.color = color;
        }

        /// <summary>碰撞体自带的网格最准；拿不到（圆形之类）就按形状自己拼一个扇形</summary>
        private static Mesh BuildMesh(Collider2D col)
        {
            Mesh mesh = col.CreateMesh(false, false);

            if (mesh != null && mesh.vertexCount >= 3) return mesh;

            Vector2[] path = PathOf(col);

            if (path == null || path.Length < 3) return null;

            Vector3[] vertices = new Vector3[path.Length];
            int[] triangles = new int[(path.Length - 2) * 3];

            for (int i = 0; i < path.Length; i++) vertices[i] = path[i];

            for (int i = 0; i < path.Length - 2; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            mesh = new Mesh { vertices = vertices, triangles = triangles };
            mesh.RecalculateBounds();

            return mesh;
        }

        private static Vector2[] PathOf(Collider2D col)
        {
            switch (col)
            {
                case PolygonCollider2D polygon when polygon.pathCount > 0:
                {
                    Vector2[] path = polygon.GetPath(0);
                    Vector2 offset = polygon.offset;

                    for (int i = 0; i < path.Length; i++) path[i] += offset;

                    return path;
                }

                case BoxCollider2D box:
                {
                    Vector2 half = box.size * 0.5f;
                    Vector2 o = box.offset;

                    return new[]
                    {
                        o + new Vector2(-half.x, -half.y),
                        o + new Vector2(-half.x, half.y),
                        o + new Vector2(half.x, half.y),
                        o + new Vector2(half.x, -half.y),
                    };
                }

                case CircleCollider2D circle:
                {
                    const int Segments = 24;

                    Vector2[] path = new Vector2[Segments];

                    for (int i = 0; i < Segments; i++)
                    {
                        float angle = i * Mathf.PI * 2f / Segments;

                        path[i] = circle.offset + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * circle.radius;
                    }

                    return path;
                }

                default:
                    return null;
            }
        }

        private static void Kill(Object target)
        {
            if (target == null) return;

            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }
    }
}
