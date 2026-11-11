using System.Collections.Generic;
using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Combat;
using Refraction.Presentation.Actor;
using UnityEngine;

namespace Refraction.Presentation.Combat
{
    /// <summary>残影与折跃连线的表现：按逻辑层账本摆残影，用 1×1 方块图拉伸成连线并结算线上伤害</summary>
    /// <remarks>挂在 CombatRoot 自己身上，玩家死了残影也不会跟着消失；只读 AfterimageLogic 给的结果，不自己判断该不该生成或连线</remarks>
    public sealed class AfterimageField : MonoBehaviour
    {
        private sealed class Slot
        {
            public GameObject Go;
            public Transform Tr;

            /// <summary>残影里的 View 副本：每个槽位只建一次，落影时改它的局部姿态</summary>
            public Transform Body;

            public SpriteRenderer Renderer;

            /// <summary>子弹靠它认出这是第几枚残影</summary>
            public AfterimageAnchor Anchor;

            /// <summary>落影那一帧抄下来的 RGB，透明度每帧另算，不覆盖它</summary>
            public Color Tint = Color.white;

            /// <summary>这一格绑定的残影序号；序号不变就绝不再碰它的姿态与颜色</summary>
            public int Index = -1;
        }

        private const string ContainerName = "Afterimages";
        private const string ViewName = "View";

        // 淡出与闪烁先写死：末态透明度、闪烁窗口、闪烁频率
        private const float FadeEndAlpha = 0.12f;
        private const float BlinkWindow = 1f;
        private const float BlinkHz = 6f;

        private readonly Dictionary<int, Slot> _live = new();
        private readonly List<int> _stale = new();
        private readonly List<SpriteRenderer> _lines = new();
        private readonly Collider2D[] _hits = new Collider2D[16];

        // 同一目标在间隔内只吃一次：几段连线共用一张表，键是受击盒，值是上次结算时刻
        private readonly Dictionary<HurtBox, float> _lastHitTime = new();

        private Pool<Slot> _slots;
        private AfterimageLogic _afterimages;
        private PlayerConfig _config;
        private Transform _view;
        private Transform _playerRoot;
        private SpriteRenderer _viewRenderer;
        private Transform _container;
        private GameObject _prefab;
        private float _z;

        /// <summary>由 CombatRoot 装配；view 是残影里那张皮，playerRoot 是账本里 Pos 所属的枢轴，prefab 是带 Collider 的残影外壳</summary>
        public void Init(AfterimageLogic afterimages, PlayerConfig config, Transform view, Transform playerRoot, GameObject prefab)
        {
            _afterimages = afterimages;
            _config = config;
            _view = view;
            _playerRoot = playerRoot;
            _prefab = prefab;
            _viewRenderer = view.GetComponent<SpriteRenderer>();
            _z = view.position.z;

            if (_prefab == null)
            {
                Debug.LogError("[AfterimageField] 没有残影预制体，残影与子弹传输都不会工作。");
                return;
            }

            GameObject container = new(ContainerName);
            _container = container.transform;
            _container.SetParent(transform, false);

            // 残影体交给通用池：容量跟残影上限走，预创建一批，落影时不再现场 Instantiate
            int capacity = Mathf.Max(1, config.maxAfterimages);

            _slots = new Pool<Slot>(
                CreateSlot,
                onGet: slot => slot.Go.SetActive(true),
                onRelease: slot => slot.Go.SetActive(false),
                name: "Afterimage",
                maxSize: capacity,
                onDestroy: slot => { if (slot.Go != null) Destroy(slot.Go); });

            _slots.Prewarm(capacity);
        }

        /// <summary>每个物理帧同步一次：残影不移动，放在固定帧刷新不会抖，判定也刚好落在物理查询该在的时机</summary>
        public void Sync()
        {
            if (_afterimages == null || _config == null || _view == null || _slots == null) return;

            AfterimageLedger ledger = _afterimages.Ledger;
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
                _slots.Release(dead);
            }

            for (int i = 0; i < count; i++)
            {
                Afterimage snap = ledger[i];

                if (!_live.TryGetValue(snap.Index, out Slot slot))
                {
                    slot = _slots.Get();
                    if (slot == null) continue;

                    _live.Add(snap.Index, slot);
                    Capture(slot, in snap);
                }

                ApplyFade(slot, in snap, now);
            }

            SyncLinks();
        }

        private void OnDestroy()
        {
            _slots?.Dispose();

            if (_container != null) Destroy(_container.gameObject);
        }

        /// <summary>外壳用残影预制体（自带吸引子弹的 Collider），里面塞一份玩家 View 的副本当皮</summary>
        private Slot CreateSlot()
        {
            GameObject go = Instantiate(_prefab, _container);
            go.name = "Afterimage";

            Collider2D[] shell = go.GetComponentsInChildren<Collider2D>(true);

            Behaviour[] behaviours = go.GetComponentsInChildren<Behaviour>(true);
            for (int i = 0; i < behaviours.Length; i++) behaviours[i].enabled = false;

            for (int i = 0; i < shell.Length; i++) shell[i].enabled = true;

            GameObject body = Instantiate(_view.gameObject, go.transform);
            body.name = ViewName;

            behaviours = body.GetComponentsInChildren<Behaviour>(true);
            for (int i = 0; i < behaviours.Length; i++) behaviours[i].enabled = false;

            Collider2D[] colliders = body.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;

            SpriteRenderer renderer = body.GetComponent<SpriteRenderer>();
            if (renderer != null && _viewRenderer != null)
            {
                renderer.sortingLayerID = _viewRenderer.sortingLayerID;
                renderer.sortingOrder = _viewRenderer.sortingOrder - 1;
            }

            AfterimageAnchor anchor = go.AddComponent<AfterimageAnchor>();

            go.SetActive(false);

            return new Slot
            {
                Go = go,
                Tr = go.transform,
                Body = body.transform,
                Renderer = renderer,
                Anchor = anchor
            };
        }

        /// <summary>落影那一帧把 View 定妆：位置、朝向、缩放、贴图与染色只抄这一次，之后本体怎么动都不再回头改</summary>
        private void Capture(Slot slot, in Afterimage snap)
        {
            slot.Index = snap.Index;

            // 根节点落在账本给的世界坐标上；朝向翻转在根节点的负缩放里，View 副本只管自己的姿态
            Vector3 world = _playerRoot.lossyScale;
            Vector3 host = _container.lossyScale;

            slot.Tr.position = new Vector3(snap.Pos.x, snap.Pos.y, _z);
            slot.Tr.rotation = _playerRoot.rotation;
            slot.Tr.localScale = new(
                host.x != 0f ? world.x / host.x : world.x,
                host.y != 0f ? world.y / host.y : world.y,
                1f);

            slot.Body.localPosition = _view.localPosition;
            slot.Body.localRotation = _view.localRotation;
            slot.Body.localScale = _view.localScale;

            slot.Anchor.Bind(_afterimages, snap.Index, snap.Facing, _playerRoot);

            if (slot.Renderer == null) return;

            Color tint = Color.white;

            if (_viewRenderer != null)
            {
                slot.Renderer.sprite = _viewRenderer.sprite;
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

        private void SyncLinks()
        {
            int capacity = _afterimages.LinkCapacity;
            int used = 0;

            bool damage = _config.afterimageLinkDamage > 0f;

            for (int i = 0; i < capacity; i++)
            {
                if (!_afterimages.TryGetLink(i, out Vector2 from, out Vector2 to)) continue;

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
                if (hit.transform.IsChildOf(_playerRoot)) continue;

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
