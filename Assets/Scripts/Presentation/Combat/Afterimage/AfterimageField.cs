using System.Collections.Generic;
using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Combat;
using Refraction.Logic.Events;
using Refraction.Presentation.Actor;
using TMPro;
using UnityEngine;

namespace Refraction.Presentation.Combat
{
    /// <summary>残影与折跃连线的表现：按逻辑层账本摆残影，用 1×1 方块图拉伸成连线；子弹穿过某条线时该线变实并沿线结算一次伤害</summary>
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

            /// <summary>头顶标号：场上有几枚就显示 1..N，按落影先后排</summary>
            public TextMeshProUGUI Label;

            /// <summary>标号所在的 Num 节点，根节点翻面后靠它把字反向抵消回来</summary>
            public Transform LabelRoot;

            /// <summary>已经写进 Label 的数字，不加这个每帧都会重排一次文字</summary>
            public int LabelValue = -1;
        }

        private const string ContainerName = "Afterimages";
        private const string ViewName = "View";
        private const string NumName = "Num";

        // 淡出与闪烁先写死：末态透明度、闪烁窗口、闪烁频率
        private const float FadeEndAlpha = 0.12f;
        private const float BlinkWindow = 1f;
        private const float BlinkHz = 6f;

        private readonly Dictionary<int, Slot> _live = new();
        private readonly List<int> _stale = new();
        private readonly List<SpriteRenderer> _lines = new();
        private readonly Collider2D[] _hits = new Collider2D[16];

        // 正在被子弹穿过的连线：键是「起点序号 << 32 | 终点序号」，值是同一段上并行几颗子弹
        private readonly Dictionary<long, int> _hot = new();

        // 一次过线结算里同一个受击盒只吃一次：线上可能压着同一个目标的多个碰撞体
        private readonly HashSet<HurtBox> _hitBoxes = new();

        private Pool<Slot> _slots;
        private AfterimageLogic _afterimages;
        private PlayerConfig _config;
        private Transform _view;
        private Transform _playerRoot;
        private SpriteRenderer _viewRenderer;
        private Transform _container;
        private GameObject _prefab;
        private float _z;

        // 标号文案预先造好，换号时只挪引用，免得在每物理帧的 Sync 里 ToString 造垃圾
        private string[] _labelText;

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

            _labelText = new string[capacity + 1];
            for (int i = 1; i <= capacity; i++) _labelText[i] = i.ToString();

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
                ApplyLabel(slot, i + 1);
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

            Transform num = go.transform.Find(NumName);
            TextMeshProUGUI label = null;

            if (num != null)
            {
                // 上面那一轮把外壳里的 TMP / Canvas 一并关了，标号要单独放回来才画得出字
                Behaviour[] labelParts = num.GetComponentsInChildren<Behaviour>(true);
                for (int i = 0; i < labelParts.Length; i++) labelParts[i].enabled = true;

                label = num.GetComponentInChildren<TextMeshProUGUI>(true);

                // 世界空间 Canvas 的 GraphicRaycaster 还是开着的，字自己不吃点击就不会挡 UI
                if (label != null) label.raycastTarget = false;
            }

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
                Anchor = anchor,
                Label = label,
                LabelRoot = num
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

            // 转向是靠根节点负缩放做的，标号会跟着镜像，所以 Num 要再翻一次抵消掉
            if (slot.LabelRoot != null)
            {
                slot.LabelRoot.localScale = new Vector3(slot.Tr.localScale.x < 0f ? -1f : 1f, 1f, 1f);
            }

            slot.Body.localPosition = _view.localPosition;
            slot.Body.localRotation = _view.localRotation;
            slot.Body.localScale = _view.localScale;

            slot.Anchor.Bind(_afterimages, this, snap.Index, snap.Facing, _playerRoot);

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

        /// <summary>标号按落影先后填：最早的 1、最新的 N，队列前面过期后整排数字往前挪</summary>
        private void ApplyLabel(Slot slot, int number)
        {
            if (slot.Label == null || slot.LabelValue == number) return;

            slot.LabelValue = number;
            slot.Label.text = number < _labelText.Length ? _labelText[number] : number.ToString();
        }

        private void SyncLinks()
        {
            int capacity = _afterimages.LinkCapacity;
            int used = 0;

            AfterimageLedger ledger = _afterimages.Ledger;
            int count = ledger.Count;

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

                int next = (i + 1) % count;

                line.color = _hot.ContainsKey(LinkKey(ledger[i].Index, ledger[next].Index))
                    ? Opaque(_config.afterimageLinkColor)
                    : _config.afterimageLinkColor;

                if (!line.gameObject.activeSelf) line.gameObject.SetActive(true);
            }

            for (int i = used; i < _lines.Count; i++)
            {
                if (_lines[i].gameObject.activeSelf) _lines[i].gameObject.SetActive(false);
            }

            // 没有连线时清表：残留的热度会让后来同号的连线一直顶着不透明
            if (used == 0 && _hot.Count > 0) _hot.Clear();
        }

        /// <summary>子弹开始穿这一段连线：这条线变实并沿线结算一次伤害</summary>
        /// <remarks>同一段可能有几颗子弹同时在路上，热度按次数叠加，谁先到谁结算自己的那一次</remarks>
        public void BeginTraverse(int from, int to)
        {
            long key = LinkKey(from, to);

            _hot.TryGetValue(key, out int count);
            _hot[key] = count + 1;

            HitAlong(from, to);
        }

        /// <summary>子弹走完这一段（或中途放弃）：没人再走了就恢复常态透明度</summary>
        public void EndTraverse(int from, int to)
        {
            long key = LinkKey(from, to);

            if (!_hot.TryGetValue(key, out int count)) return;

            if (count <= 1) _hot.Remove(key);
            else _hot[key] = count - 1;
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

        /// <summary>沿线结算一次：判定方块绕中点旋转，长度取两端间距、宽度就是线粗，和画面上拉伸的方块完全重合</summary>
        /// <remarks>同一目标身上的多个碰撞体在这一刀里只算一次；玩家自己与残影不参与</remarks>
        private void HitAlong(int fromIndex, int toIndex)
        {
            if (_config.afterimageLinkDamage <= 0f) return;
            if (!_afterimages.TryGetPos(fromIndex, out Vector2 from)) return;
            if (!_afterimages.TryGetPos(toIndex, out Vector2 to)) return;

            Vector2 delta = to - from;
            float length = delta.magnitude;

            if (length <= 1e-4f) return;

            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            Vector2 size = new(length, _config.afterimageLinkThickness);
            Vector2 center = (from + to) * 0.5f;

            _hitBoxes.Clear();

            int count = Physics2D.OverlapBoxNonAlloc(center, size, angle, _hits, Physics2D.DefaultRaycastLayers);

            for (int i = 0; i < count; i++)
            {
                Collider2D hit = _hits[i];
                if (hit == null) continue;
                if (hit.transform.IsChildOf(_playerRoot)) continue;

                HurtBox box = hit.GetComponent<HurtBox>();
                if (box == null || box.Hurtable == null) continue;
                if (!_hitBoxes.Add(box)) continue;

                Damage damage = new(_config.afterimageLinkDamage, DamageSource.AfterimageLink);
                if (!box.Hurtable.TakeDamage(in damage)) continue;

                EventBus<AfterimageLinkHit>.Publish(new(hit.bounds.center, _config.afterimageLinkDamage));
            }
        }

        private static long LinkKey(int from, int to) => ((long)from << 32) | (uint)to;

        private static Color Opaque(Color color)
        {
            color.a = 1f;
            return color;
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
