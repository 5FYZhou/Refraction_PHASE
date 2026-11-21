using System;
using Refraction.Data;
using Refraction.Logic.Combat;
using Refraction.Presentation.Actor;
using Refraction.Presentation.Effects;
using UnityEngine;

namespace Refraction.Presentation.Combat
{
    /// <summary>黑形态近战表现端：按方向点亮判定盒，命中回报给玩家</summary>
    /// <remarks>三只盒共用本对象做 IAttacker：没点亮的两只不产生触发，所以一次挥击每个目标只吃一次伤害，
    /// 不必再挂 IIntervalAttacker（那会让同一帧里的第二个目标被判成"间隔内"而漏掉）。
    /// 三只盒必须留在 Default 层：敌人受击盒在 ExcludeLayers 里排掉了 Player(7)，挂回玩家层就再也碰不到敌人。
    /// 普攻点亮 BlackDash/Ahead 并摘下来冻在原地，突进点亮 Up / Down 并且整段跟着玩家跑。
    /// 弹反盒（BlackRebound/xxxCollider）相反：不摘下来，跟攻击盒同时开关，靠根节点翻 scale 跟随转身；
    /// 子弹的碰撞表排掉了 Default 层，所以弹反不用触发器判定，改成每物理帧拿盒子的真实形状扫活子弹
    /// 命中回报有两条路，别混：ProduceDamage 里的 _onHit 取伤害那一刻就回调、不看扣血结果，只给逻辑用
    /// （普攻换命中后坐力 / 突进提前结束）；IHitLanded 要等 AttackBox 确认 TakeDamage 返回 true 才回调，
    /// 接到 PlayerLogic.OnMeleeHitLanded 涨极性并发顿帧。白攻击的子弹不经过本类，所以子弹命中不顿帧</remarks>
    public sealed class BlackAttackView : IBlackAttackView, IReboundView, IAttacker, IHitLanded, IHitBodyLanded
    {
        private const string ReboundRootPath = "BlackRebound";

        private const string ReboundAheadPath = "BlackRebound/AheadCollider";
        private const string ReboundUpPath = "BlackRebound/UpCollider";
        private const string ReboundDownPath = "BlackRebound/DownCollider";

        private const string DashAheadPath = "BlackDash/AheadCollider";
        private const string DashUpPath = "BlackDash/UpCollider";
        private const string DashDownPath = "BlackDash/DownCollider";

        private const string RangeName = "Range";

        private const string SweepName = "DashSweep";

        private const float Diagonal = 0.70710678f;

        private static readonly Color RangeColor = new(0.65f, 0.35f, 1f, 0.3f);

        private static readonly Color ReboundColor = new(1f, 0.92f, 0.02f, 0.35f);

        private static readonly Color SweepColor = new(0.2f, 1f, 0.35f, 0.22f);

        private static Material _rangeMaterial;
        private static Material _reboundMaterial;
        private static Material _sweepMaterial;

        private readonly Collider2D _reboundAhead;
        private readonly Collider2D _reboundUp;
        private readonly Collider2D _reboundDown;
        private readonly Transform _reboundRoot;
        private readonly float _reboundBaseScaleX;
        private readonly AttackBox _dashAhead;
        private readonly AttackBox _dashUp;
        private readonly AttackBox _dashDown;
        private readonly Anchor _dashAheadHome;
        private readonly HitLedger _dashHits = new();
        private readonly PlayerConfig _config;
        private readonly Func<int> _face;
        private readonly Action _onHitLanded;
        private readonly Action _onReflected;
        private readonly EffectModule _effects;

        private static bool _warnedNoRebound;

        private Collider2D _reboundActive;
        private AttackBox _activeBox;
        private Collider2D _hurt;
        private Func<IBulletLauncher> _launcher;
        private BlackAttackKind _kind;
        private Action _onHit;
        private Color _attackSparkColor = Color.white;
        private Color _reboundSparkColor = Color.white;
        private int _swingFace = 1;
        private bool _dashMode;
        private GameObject _sweep;
        private Mesh _sweepMesh;

        /// <summary>root 是玩家根节点，层级里按 BlackDash/xxxCollider 找；朝向开打那一帧取定</summary>
        /// <remarks>onReflected 只在真弹回一颗子弹时回调一次，用来还空中突进次数；打不回去的（受击范围内、已被弹过）不算</remarks>
        public BlackAttackView(Transform root, PlayerConfig config, Func<int> face, Action onHitLanded = null, EffectModule effects = null, Action onReflected = null)
        {
            _config = config;
            _face = face;
            _onHitLanded = onHitLanded;
            _onReflected = onReflected;
            _effects = effects;

            _reboundAhead = FindRebound(root, ReboundAheadPath);
            _reboundUp = FindRebound(root, ReboundUpPath);
            _reboundDown = FindRebound(root, ReboundDownPath);

            _reboundRoot = root != null ? root.Find(ReboundRootPath) : null;
            _reboundBaseScaleX = _reboundRoot != null ? _reboundRoot.localScale.x : 1f;

            _dashAhead = Find(root, DashAheadPath);
            _dashUp = Find(root, DashUpPath);
            _dashDown = Find(root, DashDownPath);
            _dashAheadHome = new(_dashAhead);

            BindDash(_dashAhead);
            BindDash(_dashUp);
            BindDash(_dashDown);

            // 弹反盒同样按真实碰撞形状画，换成明黄；盒不点亮时子物体跟着隐藏，和攻击盒一致
            Paint(_reboundAhead, ReboundMaterial);
            Paint(_reboundUp, ReboundMaterial);
            Paint(_reboundDown, ReboundMaterial);

            Paint(_dashAhead);
            Paint(_dashUp);
            Paint(_dashDown);

            End();
            HideRebound();
        }

        public void SetSparkColors(Color attack, Color rebound)
        {
            _attackSparkColor = attack;
            _reboundSparkColor = rebound;
        }

        /// <summary>普攻用：点亮 BlackDash 下的正前方盒，开打那一帧摘下来冻在原地，直到 End 才装回去</summary>
        public void BeginFlat()
        {
            End();

            _kind = BlackAttackKind.Ahead;
            _swingFace = _face != null ? _face() : 1;

            if (_dashAhead == null) return;

            _dashHits.Clear();
            _dashAhead.transform.SetParent(null, true);
            SetActive(_dashAhead, true);
            _activeBox = _dashAhead;
        }

        /// <summary>黑突进用：点亮对应突进盒与弹反盒，突进盒不摘下来，整段跟着玩家跑</summary>
        public void BeginDash(BlackAttackKind kind)
        {
            End();

            _kind = kind;
            _dashMode = true;
            _swingFace = _face != null ? _face() : 1;

            _dashHits.Clear();
            SetActive(DashBoxOf(kind), true);
            _activeBox = DashBoxOf(kind);
            PaintSweep(kind);
        }

        public void End()
        {
            Close(_dashAhead, _dashAheadHome);
            SetActive(_dashUp, false);
            SetActive(_dashDown, false);

            ClearSweep();
            _dashMode = false;
            _activeBox = null;
        }

        private static void Close(AttackBox box, in Anchor home)
        {
            if (box == null) return;

            SetActive(box, false);
            home.Restore(box);
        }

        /// <summary>弹反盒不摘下来：留在玩家层级里，靠 BlackRebound 的 scale.x 翻到指令那一侧</summary>
        public void ShowRebound(BlackAttackKind kind, int side)
        {
            Collider2D zone = ReboundOf(kind);

            if (zone == null) return;

            if (_reboundActive != zone)
            {
                SetActive(_reboundAhead, false);
                SetActive(_reboundUp, false);
                SetActive(_reboundDown, false);
                SetActive(zone, true);
                _reboundActive = zone;
            }

            MirrorRebound(side);
        }

        public void HideRebound()
        {
            _reboundActive = null;

            SetActive(_reboundAhead, false);
            SetActive(_reboundUp, false);
            SetActive(_reboundDown, false);
        }

        /// <summary>根节点已经按朝向翻过 scale，指令那一侧与朝向相反时把 BlackRebound 再翻一次</summary>
        private void MirrorRebound(int side)
        {
            if (_reboundRoot == null) return;

            int face = _face != null ? _face() : 1;
            float x = _reboundBaseScaleX * (side == face ? 1f : -1f);
            Vector3 scale = _reboundRoot.localScale;

            if (Mathf.Approximately(scale.x, x)) return;

            scale.x = x;
            _reboundRoot.localScale = scale;
        }

        private Collider2D ReboundOf(BlackAttackKind kind)
        {
            switch (kind)
            {
                case BlackAttackKind.Up: return _reboundUp;
                case BlackAttackKind.Down: return _reboundDown;
                default: return _reboundAhead;
            }
        }

        /// <summary>接上玩家自己的开火口与身体受击盒：弹反要借玩家的池重发一发，受击盒用来判优先级</summary>
        public void BindRebound(Func<IBulletLauncher> launcher, Collider2D hurt)
        {
            _launcher = launcher;
            _hurt = hurt;
        }

        /// <summary>每物理帧扫一遍活子弹：压在身体上的让给受击，落在弹反范围内的敌弹反向打回去</summary>
        public void ReboundTick()
        {
            Collider2D zone = _reboundActive;
            if (zone == null) return;

            IBulletLauncher launcher = _launcher?.Invoke();
            if (launcher == null) return;

            float damage = _config.bulletDamage;
            float knockback = _config.bulletKnockback;
            float transfer = _config.bulletTransferSpeed;

            for (int i = Bullet.LiveCount - 1; i >= 0; i--)
            {
                Bullet bullet = Bullet.LiveAt(i);
                if (bullet == null || bullet.PlayerOwned) continue;

                Vector2 pos = bullet.Position;
                if (InHurtRange(pos)) continue;
                if (!zone.OverlapPoint(pos)) continue;

                Vector2 reflectDir = bullet.ReflectDirection;

                if (!bullet.TryReflect(launcher, damage, knockback, transfer)) continue;

                _effects?.PlayReboundSpark(pos, reflectDir, _reboundSparkColor);
                _onReflected?.Invoke();
            }
        }

        /// <summary>身体受击范围与弹反范围按设计不重叠；真撞在同一颗子弹上时受击优先，不许被弹走</summary>
        public bool InHurtRange(Vector2 pos)
        {
            if (_hurt == null || !_hurt.enabled) return false;

            Bounds bounds = _hurt.bounds;

            return pos.x >= bounds.min.x && pos.x <= bounds.max.x
                   && pos.y >= bounds.min.y && pos.y <= bounds.max.y;
        }

        /// <summary>逻辑侧命中钩子：盒真碰到受击盒才回调（反冲 / 突进提前结束），不是每次挥空都算命中</summary>
        public void SetHitHandler(Action onHit) => _onHit = onHit;

        public Damage ProduceDamage()
        {
            // AttackBox 每次接触都会先取伤害再结算；扣血失败的（无敌窗、站桩靶子）也算砍到了实体
            _onHit?.Invoke();

            return _dashMode
                ? new Damage(_config.blackDashDamage, DamageSource.Melee, DirectionOf(), _config.blackDashKnockback)
                : new Damage(_config.flatAttackDamage, DamageSource.Melee, DirectionOf(), _config.flatAttackKnockback);
        }

        private Vector2 DirectionOf()
        {
            int face = _swingFace;
            float x = face < 0 ? -1f : 1f;

            return _kind switch
            {
                BlackAttackKind.Up => new Vector2(x * Diagonal, Diagonal),
                BlackAttackKind.Down => new Vector2(x * Diagonal, -Diagonal),
                _ => new Vector2(x, 0f),
            };
        }

        private void BindDash(AttackBox box)
        {
            if (box == null) return;

            box.SetAttack(this);
            box.SetOnceLedger(_dashHits);
        }

        /// <summary>突进只认斜上 / 斜下；Ahead 那只留给普攻，突进这一路不碰它</summary>
        private AttackBox DashBoxOf(BlackAttackKind kind)
            => kind == BlackAttackKind.Down ? _dashDown : _dashUp;

        /// <summary>绿色扫掠面：把起手形状沿突进位移平移一份，取两组点的凸包，位置固定在世界里</summary>
        private void PaintSweep(BlackAttackKind kind)
        {
            AttackBox box = DashBoxOf(kind);

            if (box == null) return;

            Collider2D col = box.GetComponent<Collider2D>();
            Vector2[] path = col != null ? PathOf(col) : null;

            if (path == null || path.Length < 3) return;

            Vector2 travel = DirectionOf() * TravelOf(kind);
            Vector3[] points = new Vector3[path.Length * 2];

            for (int i = 0; i < path.Length; i++)
            {
                Vector3 start = box.transform.TransformPoint(path[i]);

                points[i] = start;
                points[i + path.Length] = start + (Vector3)travel;
            }

            Mesh mesh = HullMesh(points);

            if (mesh == null) return;

            mesh.hideFlags = HideFlags.HideAndDontSave;

            GameObject go = new(SweepName) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(null, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = SweepMaterial;
            mr.sortingOrder = 29999;

            _sweep = go;
            _sweepMesh = mesh;
        }

        private float TravelOf(BlackAttackKind kind)
            => kind == BlackAttackKind.Down
                ? _config.blackDashSpeedDown * _config.blackDashTimeDown
                : _config.blackDashSpeedUp * _config.blackDashTimeUp;

        private void ClearSweep()
        {
            if (_sweep != null)
            {
                // 先停用再销毁：运行期 Destroy 要到帧末才落地，先停用才查不到这层残影
                _sweep.SetActive(false);

                if (Application.isPlaying) UnityEngine.Object.Destroy(_sweep);
                else UnityEngine.Object.DestroyImmediate(_sweep);
            }

            if (_sweepMesh != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_sweepMesh);
                else UnityEngine.Object.DestroyImmediate(_sweepMesh);
            }

            _sweep = null;
            _sweepMesh = null;
        }

        private static Mesh HullMesh(Vector3[] points)
        {
            Vector2[] hull = Hull(points);

            if (hull == null || hull.Length < 3) return null;

            Vector3[] vertices = new Vector3[hull.Length];
            int[] triangles = new int[(hull.Length - 2) * 3];

            for (int i = 0; i < hull.Length; i++) vertices[i] = hull[i];

            for (int i = 0; i < hull.Length - 2; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            Mesh mesh = new() { vertices = vertices, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector2[] Hull(Vector3[] points)
        {
            if (points == null || points.Length < 3) return null;

            Vector2[] sorted = new Vector2[points.Length];

            for (int i = 0; i < points.Length; i++) sorted[i] = points[i];

            System.Array.Sort(sorted, ComparePoint);

            Vector2[] hull = new Vector2[sorted.Length * 2];
            int k = 0;

            for (int i = 0; i < sorted.Length; i++)
            {
                while (k >= 2 && Cross(hull[k - 2], hull[k - 1], sorted[i]) <= 0f) k--;

                hull[k++] = sorted[i];
            }

            for (int i = sorted.Length - 2, floor = k + 1; i >= 0; i--)
            {
                while (k >= floor && Cross(hull[k - 2], hull[k - 1], sorted[i]) <= 0f) k--;

                hull[k++] = sorted[i];
            }

            System.Array.Resize(ref hull, k - 1);
            return hull;
        }

        private static int ComparePoint(Vector2 a, Vector2 b)
            => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y);

        private static float Cross(Vector2 o, Vector2 a, Vector2 b)
            => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        /// <summary>攻击盒真扣到目标身上之后才回调：涨极性与顿帧全挂这条路上，子弹命中不经过本类</summary>
        public void OnHitLanded() => OnHitLanded(null);

        /// <summary>火花贴在命中表面的最近点上，朝向按这一刀的黑攻击方向</summary>
        public void OnHitLanded(Collider2D other)
        {
            PlayAttackSpark(other);
            _onHitLanded?.Invoke();
        }

        private void PlayAttackSpark(Collider2D other)
        {
            if (_effects == null || other == null) return;

            Vector2 origin = _activeBox != null ? (Vector2)_activeBox.transform.position : (Vector2)other.bounds.center;

            _effects.PlayAttackSpark(other.ClosestPoint(origin), DirectionOf(), _attackSparkColor);
        }

        private static void SetActive(Component target, bool active)
        {
            if (target != null && target.gameObject.activeSelf != active) target.gameObject.SetActive(active);
        }

        /// <summary>盒在预制体里的原样：摘出去冻住之后靠这份数据装回原父级，下一次攻击再按新站位重新摘</summary>
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
        private static void Paint(AttackBox box) => Paint(box != null ? box.GetComponent<Collider2D>() : null, RangeMaterial);

        private static void Paint(Collider2D col, Material material)
        {
            if (col == null || material == null) return;
            if (col.transform.Find(RangeName) != null) return;

            Mesh mesh = col.CreateMesh(false, false);

            if (mesh == null || mesh.vertexCount < 3) mesh = ShapeMesh(col);

            if (mesh == null) return;

            mesh.hideFlags = HideFlags.HideAndDontSave;

            GameObject go = new(RangeName);
            go.transform.SetParent(col.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
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

        private static Material RangeMaterial => MaterialOf(RangeColor, ref _rangeMaterial);

        private static Material ReboundMaterial => MaterialOf(ReboundColor, ref _reboundMaterial);

        private static Material SweepMaterial => MaterialOf(SweepColor, ref _sweepMaterial);

        private static Material MaterialOf(Color color, ref Material cache)
        {
            if (cache != null) return cache;

            Shader shader = Shader.Find("Sprites/Default");

            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) return null;

            Material mat = new(shader) { color = color, hideFlags = HideFlags.HideAndDontSave };

            if (mat.HasProperty("_MainTex")) mat.mainTexture = Texture2D.whiteTexture;

            return cache = mat;
        }

        private static Collider2D FindRebound(Transform root, string path)
        {
            Transform found = root != null ? root.Find(path) : null;
            Collider2D col = found != null ? found.GetComponent<Collider2D>() : null;

            if (col == null && !_warnedNoRebound)
            {
                _warnedNoRebound = true;
                Debug.LogWarning($"[BlackAttackView] 玩家层级里找不到 {path}（或该节点没碰撞体），黑形态这一路弹反不会生效。");
            }

            return col;
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
