using System.Collections.Generic;
using Refraction.Data;
using Refraction.Logic.Actor;
using Refraction.Logic.Combat;
using UnityEngine;

namespace Refraction.Presentation.Actor
{
    /// <summary>残影与折跃连线的表现：按逻辑层账本克隆 View 摆残影，用 1×1 方块图拉伸成连线并结算线上伤害</summary>
    /// <remarks>本组件由 PlayerController 在 Attach 时补挂，不挂预制体；残影是静态快照，位置只来自 AfterimageLedger</remarks>
    public sealed class PlayerAfterimageField : MonoBehaviour
    {
        private sealed class Slot
        {
            public GameObject Go;
            public Transform Tr;
            public SpriteRenderer Renderer;

            /// <summary>落影那一帧抄下来的 RGB，透明度每帧另算，不覆盖它</summary>
            public Color Tint = Color.white;

            /// <summary>这一格绑定的残影序号；序号不变就绝不再碰它的姿态与颜色</summary>
            public int Index = -1;
        }

        private const string ContainerName = "Afterimages";

        // 淡出与闪烁先写死：末态透明度、闪烁窗口、闪烁频率
        private const float FadeEndAlpha = 0.12f;
        private const float BlinkWindow = 1f;
        private const float BlinkHz = 6f;

        private readonly Dictionary<int, Slot> _live = new();
        private readonly List<Slot> _pool = new();
        private readonly List<int> _stale = new();
        private readonly List<SpriteRenderer> _lines = new();
        private readonly Collider2D[] _hits = new Collider2D[16];

        // 同一目标在间隔内只吃一次：几段连线共用一张表，键是受击盒，值是上次结算时刻
        private readonly Dictionary<HurtBox, float> _lastHitTime = new();

        private PlayerLogic _logic;
        private PlayerConfig _config;
        private Transform _view;
        private SpriteRenderer _viewRenderer;
        private Transform _container;
        private float _z;

        /// <summary>由 PlayerController 装配；view 必须非空，残影就是它的副本</summary>
        public void Init(PlayerLogic logic, PlayerConfig config, Transform view)
        {
            _logic = logic;
            _config = config;
            _view = view;
            _viewRenderer = view.GetComponent<SpriteRenderer>();
            _z = view.position.z;

            GameObject container = new(ContainerName);
            _container = container.transform;
        }

        /// <summary>每个物理帧同步一次：残影不移动，放在固定帧刷新不会抖，判定也刚好落在物理查询该在的时机</summary>
        public void Sync()
        {
            if (_logic == null || _config == null || _view == null) return;

            AfterimageLedger ledger = _logic.Afterimages;
            int count = ledger.Count;
            float now = Time.fixedTime;

            // 槽跟着残影序号走，不跟队列位置：最老一枚过期后整排会左移，按位置对号会把还活着的残影重拍成现在的样子
            _stale.Clear();

            foreach (KeyValuePair<int, Slot> pair in _live)
            {
                bool alive = false;

                for (int i = 0; i < count; i++)
                {
                    if (ledger[i].Index == pair.Key)
                    {
                        alive = true;
                        break;
                    }
                }

                if (!alive) _stale.Add(pair.Key);
            }

            for (int i = 0; i < _stale.Count; i++)
            {
                Slot dead = _live[_stale[i]];
                _live.Remove(_stale[i]);
                dead.Go.SetActive(false);
                _pool.Add(dead);
            }

            for (int i = 0; i < count; i++)
            {
                Afterimage snap = ledger[i];

                if (!_live.TryGetValue(snap.Index, out Slot slot))
                {
                    slot = TakeSlot();
                    _live.Add(snap.Index, slot);
                    Capture(slot, in snap);
                    slot.Go.SetActive(true);
                }

                ApplyFade(slot, in snap, now);
            }

            SyncLinks(ledger);
        }

        private void OnDestroy()
        {
            if (_container != null) Destroy(_container.gameObject);
        }

        private Slot TakeSlot()
        {
            if (_pool.Count == 0) return CreateSlot();

            int last = _pool.Count - 1;
            Slot slot = _pool[last];
            _pool.RemoveAt(last);

            return slot;
        }

        private Slot CreateSlot()
        {
            GameObject go = Instantiate(_view.gameObject, _container);
            go.name = "Afterimage";

            // 克隆只留画：行为与碰撞体一律关掉，残影不会自己动，也不会被当成第二个身体
            Behaviour[] behaviours = go.GetComponentsInChildren<Behaviour>(true);
            for (int i = 0; i < behaviours.Length; i++) behaviours[i].enabled = false;

            Collider2D[] colliders = go.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;

            SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                if (_viewRenderer != null)
                {
                    renderer.sortingLayerID = _viewRenderer.sortingLayerID;
                    renderer.sortingOrder = _viewRenderer.sortingOrder - 1;
                }
            }

            go.SetActive(false);

            return new Slot { Go = go, Tr = go.transform, Renderer = renderer };
        }

        /// <summary>落影那一帧把 View 定妆：位置、朝向、缩放、贴图与染色只抄这一次，之后本体怎么动都不再回头改</summary>
        private void Capture(Slot slot, in Afterimage snap)
        {
            slot.Index = snap.Index;

            // 蹲下时 View 整体下移，残影得加上当时的偏移，否则会悬在落影点上方
            Vector3 offset = _view.position - transform.position;

            slot.Tr.position = new Vector3(snap.Pos.x + offset.x, snap.Pos.y + offset.y, _z);
            slot.Tr.rotation = _view.rotation;
            slot.Tr.localScale = _view.lossyScale;

            if (slot.Renderer == null) return;

            Color tint = Color.white;

            if (_viewRenderer != null)
            {
                slot.Renderer.sprite = _viewRenderer.sprite;
                slot.Renderer.flipX = _viewRenderer.flipX;
                tint = _viewRenderer.color;
            }

            tint.a = 1f;
            slot.Tint = tint;
        }

        /// <summary>淡到写死的终止透明度，最后一秒开始闪：残影快到点要先看得出来，否则像凭空消失</summary>
        private void ApplyFade(Slot slot, in Afterimage snap, float now)
        {
            if (slot.Renderer == null) return;

            float alpha = _config.afterimageAlpha;

            if (snap.Duration > 0f)
            {
                float age = now - snap.SpawnTime;
                alpha = Mathf.Lerp(_config.afterimageAlpha, FadeEndAlpha, Mathf.Clamp01(age / snap.Duration));

                if (snap.Duration - age <= BlinkWindow)
                {
                    float blink = 0.5f + 0.5f * Mathf.Sin(age * BlinkHz * Mathf.PI * 2f);
                    alpha *= 0.25f + 0.75f * blink;
                }
            }

            slot.Renderer.color = new Color(slot.Tint.r, slot.Tint.g, slot.Tint.b, alpha);
        }

        private void SyncLinks(AfterimageLedger ledger)
        {
            int capacity = ledger.LinkCapacity;
            int used = 0;

            bool damage = _config.afterimageLinkDamage > 0f;

            for (int i = 0; i < capacity; i++)
            {
                if (!ledger.TryGetLink(i, _config.afterimageLinkSenseRadius, out Vector2 from, out Vector2 to)) continue;

                Vector2 delta = to - from;
                float length = delta.magnitude;

                SpriteRenderer line = EnsureLine(used);
                used++;

                float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

                Transform tr = line.transform;
                tr.position = new Vector3((from.x + to.x) * 0.5f, (from.y + to.y) * 0.5f, _z);
                tr.rotation = Quaternion.Euler(0f, 0f, angle);
                tr.localScale = new Vector3(length, _config.afterimageLinkThickness, 1f);

                line.color = _config.afterimageLinkColor;
                if (!line.gameObject.activeSelf) line.gameObject.SetActive(true);

                if (damage) DamageAlong(from, to, length, angle, _config.afterimageLinkThickness);
            }

            for (int i = used; i < _lines.Count; i++)
            {
                if (_lines[i].gameObject.activeSelf) _lines[i].gameObject.SetActive(false);
            }

            // 没有连线时清表：残留的目标键会一直卡着已销毁的对象
            if (used == 0 && _lastHitTime.Count > 0) _lastHitTime.Clear();
        }

        private SpriteRenderer EnsureLine(int i)
        {
            while (_lines.Count <= i) _lines.Add(CreateLine(_lines.Count));

            return _lines[i];
        }

        private SpriteRenderer CreateLine(int index)
        {
            GameObject go = new($"Link_{index}", typeof(SpriteRenderer));
            go.transform.SetParent(_container, false);

            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = SquareSprite;
            sr.color = _config.afterimageLinkColor;

            if (_viewRenderer != null)
            {
                sr.sortingLayerID = _viewRenderer.sortingLayerID;
                sr.sortingOrder = _viewRenderer.sortingOrder - 2;
            }

            go.SetActive(false);
            return sr;
        }

        /// <summary>连线判定 = 一根绕中点旋转的方块：长度取两残影间距，宽度就是粗细，和画面上拉伸的方块图完全重合</summary>
        private void DamageAlong(Vector2 from, Vector2 to, float length, float angle, float thickness)
        {
            Vector2 center = (from + to) * 0.5f;
            Vector2 size = new(length, thickness);

            int count = Physics2D.OverlapBoxNonAlloc(center, size, angle, _hits, Physics2D.DefaultRaycastLayers);

            float interval = _config.afterimageLinkDamageInterval;
            float now = Time.fixedTime;

            for (int i = 0; i < count; i++)
            {
                Collider2D hit = _hits[i];
                if (hit == null) continue;
                if (hit.transform.root == transform.root) continue;

                HurtBox box = hit.GetComponent<HurtBox>();
                if (box == null || box.Attackable == null) continue;

                if (interval > 0f && _lastHitTime.TryGetValue(box, out float last) && now - last < interval) continue;

                _lastHitTime[box] = now;
                box.Attackable.TakeDamage(new Damage(_config.afterimageLinkDamage, DamageSource.AfterimageLink));
            }
        }

        /// <summary>1×1 白图按 1 像素 = 1 米生成、中心枢轴：拉 localScale 就是一条粗细可调的实心方块，不必依赖美术给方形图</summary>
        private static Sprite _square;

        private static Sprite SquareSprite
        {
            get
            {
                if (_square != null) return _square;

                Texture2D texture = new(1, 1, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    name = "AfterimageLinkSquare"
                };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();

                _square = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
                _square.hideFlags = HideFlags.HideAndDontSave;

                return _square;
            }
        }
    }
}
