using System;
using Refraction.Data;
using Refraction.Logic.Combat;
using Refraction.Presentation.Actor;
using UnityEngine;

namespace Refraction.Presentation.Combat
{
    /// <summary>黑攻击表现端：按判定方向点亮 Player/BlackAttack 下对应的攻击盒，命中回报给玩家</summary>
    /// <remarks>三只盒共用本对象做 IAttacker：没点亮的两只不产生触发，所以一次挥击每个目标只吃一次伤害，
    /// 不必再挂 IIntervalAttacker（那会让同一帧里的第二个目标被判成"间隔内"而漏掉）。
    /// 三只盒必须留在 Default 层：敌人受击盒在 ExcludeLayers 里排掉了 Player(7)，挂回玩家层就再也碰不到敌人</remarks>
    public sealed class BlackAttackView : IBlackAttackView, IAttacker, IHitLanded
    {
        private const string AheadPath = "BlackAttack/AheadCollider";
        private const string UpPath = "BlackAttack/UpCollider";
        private const string DownPath = "BlackAttack/DownCollider";

        private const string RangeName = "Range";

        private const float Diagonal = 0.70710678f;

        private static readonly Color RangeColor = new(0.65f, 0.35f, 1f, 0.3f);

        private static Material _rangeMaterial;

        private readonly AttackBox _ahead;
        private readonly AttackBox _up;
        private readonly AttackBox _down;
        private readonly Anchor _aheadHome;
        private readonly Anchor _upHome;
        private readonly Anchor _downHome;
        private readonly PlayerConfig _config;
        private readonly Func<int> _face;
        private readonly Action _onHitLanded;

        private BlackAttackKind _kind;
        private Action _onHit;
        private int _swingFace = 1;

        /// <summary>root 是玩家根节点，层级里按 BlackAttack/xxxCollider 找；朝向开打那一帧取定</summary>
        public BlackAttackView(Transform root, PlayerConfig config, Func<int> face, Action onHitLanded = null)
        {
            _config = config;
            _face = face;
            _onHitLanded = onHitLanded;

            _ahead = Find(root, AheadPath);
            _up = Find(root, UpPath);
            _down = Find(root, DownPath);

            _aheadHome = new(_ahead);
            _upHome = new(_up);
            _downHome = new(_down);

            // 攻击盒把结算委托给 Attacker，三只盒共用同一份伤害与方向
            if (_ahead != null) _ahead.SetAttack(this);
            if (_up != null) _up.SetAttack(this);
            if (_down != null) _down.SetAttack(this);

            Paint(_ahead);
            Paint(_up);
            Paint(_down);

            End();
        }

        /// <summary>点亮那一帧把盒从玩家身上摘下来冻在世界里，之后玩家走开判定也不跟着走，直到 End 才装回去</summary>
        public void Begin(BlackAttackKind kind)
        {
            _kind = kind;
            End();

            AttackBox box = BoxOf(kind);
            if (box == null) return;

            _swingFace = _face != null ? _face() : 1;

            box.transform.SetParent(null, true);
            box.gameObject.SetActive(true);
        }

        public void End()
        {
            Close(_ahead, _aheadHome);
            Close(_up, _upHome);
            Close(_down, _downHome);
        }

        private static void Close(AttackBox box, in Anchor home)
        {
            if (box == null) return;

            SetActive(box, false);
            home.Restore(box);
        }

        /// <summary>逻辑侧命中钩子：盒真碰到受击盒才给反冲力，不是每次挥空气都推自己一把</summary>
        public void SetHitHandler(Action onHit) => _onHit = onHit;

        public Damage ProduceDamage()
        {
            // AttackBox 每次接触都会先取伤害再结算；扣血失败的（无敌窗、站桩靶子）也算砍到了实体
            _onHit?.Invoke();

            int face = _swingFace;
            float x = face < 0 ? -1f : 1f;

            Vector2 direction = _kind switch
            {
                BlackAttackKind.Up => new Vector2(x * Diagonal, Diagonal),
                BlackAttackKind.Down => new Vector2(x * Diagonal, -Diagonal),
                _ => new Vector2(x, 0f),
            };

            return new Damage(_config.blackAttackDamage, DamageSource.Melee, direction, _config.blackAttackKnockback);
        }

        public void OnHitLanded() => _onHitLanded?.Invoke();

        private AttackBox BoxOf(BlackAttackKind kind)
        {
            switch (kind)
            {
                case BlackAttackKind.Up: return _up;
                case BlackAttackKind.Down: return _down;
                default: return _ahead;
            }
        }

        private static void SetActive(AttackBox box, bool active)
        {
            if (box != null && box.gameObject.activeSelf != active) box.gameObject.SetActive(active);
        }

        /// <summary>盒在预制体里的原样：摘出去冻住之后靠这份数据装回 BlackAttack 下，下一次攻击再按新站位重新摘</summary>
        private readonly struct Anchor
        {
            private readonly Transform _parent;
            private readonly Vector3 _position;
            private readonly Quaternion _rotation;
            private readonly Vector3 _scale;

            public Anchor(AttackBox box)
            {
                _parent = box != null ? box.transform.parent : null;
                _position = box != null ? box.transform.localPosition : Vector3.zero;
                _rotation = box != null ? box.transform.localRotation : Quaternion.identity;
                _scale = box != null ? box.transform.localScale : Vector3.one;
            }

            public void Restore(AttackBox box)
            {
                if (box == null || _parent == null) return;

                Transform t = box.transform;
                t.SetParent(_parent, false);
                t.localPosition = _position;
                t.localRotation = _rotation;
                t.localScale = _scale;
            }
        }

        /// <summary>把盒的真实碰撞形状画成半透明网格：网格顶点在盒的局部空间，子物体随盒一起显隐</summary>
        private static void Paint(AttackBox box)
        {
            if (box == null) return;

            Collider2D col = box.GetComponent<Collider2D>();

            if (col == null || box.transform.Find(RangeName) != null) return;

            Mesh mesh = col.CreateMesh(false, false);

            if (mesh == null || mesh.vertexCount < 3) mesh = ShapeMesh(col);

            if (mesh == null) return;

            mesh.hideFlags = HideFlags.HideAndDontSave;

            GameObject go = new(RangeName);
            go.transform.SetParent(box.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = RangeMaterial;
            mr.sortingOrder = 30000;
        }

        private static Mesh ShapeMesh(Collider2D col)
        {
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

            Mesh mesh = new() { vertices = vertices, triangles = triangles };
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
                default:
                    return null;
            }
        }

        private static Material RangeMaterial
        {
            get
            {
                if (_rangeMaterial != null) return _rangeMaterial;

                Shader shader = Shader.Find("Sprites/Default");

                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader == null) return null;

                Material mat = new(shader) { color = RangeColor, hideFlags = HideFlags.HideAndDontSave };

                if (mat.HasProperty("_MainTex")) mat.mainTexture = Texture2D.whiteTexture;

                return _rangeMaterial = mat;
            }
        }

        private static AttackBox Find(Transform root, string path)
        {
            Transform found = root != null ? root.Find(path) : null;

            if (found == null)
            {
                Debug.LogWarning($"[BlackAttackView] 玩家层级里找不到 {path}，黑形态这一路判定不会生效。");
                return null;
            }

            AttackBox box = found.GetComponent<AttackBox>();

            if (box == null) Debug.LogWarning($"[BlackAttackView] {path} 上没挂 AttackBox，黑形态这一路判定不会生效。");

            return box;
        }
    }
}
