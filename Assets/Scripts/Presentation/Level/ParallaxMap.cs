using System;
using Refraction.Logic.Level;
using UnityEngine;

namespace Refraction.Presentation.Level
{
    /// <summary>视差背景：按镜头位移把各层往后拖（x 主拖、y 微量），第一层最近（跟世界 1:1），越往后越跟不上镜头；世界转黑时整图与树下所有粒子一并取反</summary>
    /// <remarks>层就是本物体的直接子物体，锚点取各自的 localPosition.x，所以这个物体本身不能跟着动。镜头位置读在 Update，比 Cinemachine 的 LateUpdate 慢一帧：背景整体慢一帧但不会抖。叫 BG 的那层只换色不淡入，其余层与粒子跟着世界反转淡入</remarks>
    public sealed class ParallaxMap : MonoBehaviour, ISceneRoot, IRenderTicked, IReverse, IReverseFade
    {
        private const string BgName = "BG";
        [Tooltip("视差基准镜头。留空则用 Camera.main；读的是它的世界 x。")]
        [SerializeField]
        private Transform playerCamera;

        [Tooltip("最远那一层跟着镜头走多少（0 = 完全不动，1 = 完全跟着镜头）。第一层固定 0，中间各层按层数均分。")]
        [Range(0f, 0.99f)]
        [SerializeField]
        private float farthestFollow = 0.75f;

        [Tooltip("最远那一层在 y 方向跟着镜头走多少（比例同 x，但取值要小，否则跳跃时背景整幅上下甩）。第一层固定 0。")]
        [Range(0f, 0.5f)]
        [SerializeField]
        private float farthestFollowY = 0.06f;

        public int Order => SceneOrder.World;

        private GameRoot _root;
        private Transform _camera;
        private Transform[] _layers;
        private float[] _anchorX;
        private float[] _anchorY;
        private float[] _follow;
        private float[] _followY;
        private SpriteRenderer[] _sprites;
        private Color[] _spriteColors;
        private bool[] _spriteFades;
        private ParticleSystem[] _particles;
        private ParticleSystem.MinMaxGradient[] _particleColors;
        private ParticleSystem.MinMaxGradient[] _particleColorsBlack;
        private bool[] _particleFades;

        private WorldPolarity _world;
        private bool _hasWorld;
        private int _bgIndex = -1;
        private float _fade = 1f;

        private float _cameraStartX;
        private float _cameraStartY;
        private float _lastOffsetX = float.NaN;
        private float _lastOffsetY = float.NaN;

        private void Awake()
        {
            CacheLayers();
            CacheSprites();
            CacheParticles();
        }

        private void Start()
        {
            _root = GameRoot.Instance;
            _root?.RegisterSceneRoot(this);
        }

        private void OnDestroy()
        {
            _root?.UnregisterSceneRoot(this);
            LevelRoot.Current?.UnregisterReverse(this);
            LevelRoot.Current?.UnregisterReverseFade(this);
        }

        public void Attach()
        {
            if (_layers == null) CacheLayers();
            if (_sprites == null) CacheSprites();
            if (_particles == null) CacheParticles();

            EnsureCamera();

            _root ??= GameRoot.Instance;
            LevelRoot.Current?.RegisterReverse(this);
            LevelRoot.Current?.RegisterReverseFade(this);
        }

        public void RenderTick(float deltaTime)
        {
            if (!EnsureCamera()) return;

            float offsetX = _camera.position.x - _cameraStartX;
            float offsetY = _camera.position.y - _cameraStartY;
            if (Mathf.Approximately(offsetX, _lastOffsetX) && Mathf.Approximately(offsetY, _lastOffsetY)) return;

            _lastOffsetX = offsetX;
            _lastOffsetY = offsetY;

            for (int i = 0; i < _layers.Length; i++)
            {
                Vector3 pos = _layers[i].localPosition;
                pos.x = _anchorX[i] + offsetX * _follow[i];
                pos.y = _anchorY[i] + offsetY * _followY[i];
                _layers[i].localPosition = pos;
            }
        }

        private void CacheLayers()
        {
            int count = transform.childCount;

            _layers = new Transform[count];
            _anchorX = new float[count];
            _anchorY = new float[count];
            _follow = new float[count];
            _followY = new float[count];
            _bgIndex = -1;

            for (int i = 0; i < count; i++)
            {
                Transform layer = transform.GetChild(i);

                _layers[i] = layer;
                _anchorX[i] = layer.localPosition.x;
                _anchorY[i] = layer.localPosition.y;

                if (_bgIndex < 0 && string.Equals(layer.name, BgName, StringComparison.OrdinalIgnoreCase)) _bgIndex = i;

                // 第一层（最近）跟世界 1:1 不动，最远那层按 farthestFollow 拖，中间线性过渡
                float ratio = count <= 1 ? 0f : (float)i / (count - 1);
                _follow[i] = farthestFollow * ratio;
                _followY[i] = farthestFollowY * ratio;
            }
        }

        private void CacheSprites()
        {
            _sprites = GetComponentsInChildren<SpriteRenderer>(true);
            if (_spriteColors == null || _spriteColors.Length != _sprites.Length) _spriteColors = new Color[_sprites.Length];
            if (_spriteFades == null || _spriteFades.Length != _sprites.Length) _spriteFades = new bool[_sprites.Length];

            Transform bg = _bgIndex >= 0 ? _layers[_bgIndex] : null;

            for (int i = 0; i < _sprites.Length; i++)
            {
                _spriteColors[i] = _sprites[i].color;
                _spriteFades[i] = bg == null || !_sprites[i].transform.IsChildOf(bg);
            }
        }

        private void CacheParticles()
        {
            _particles = GetComponentsInChildren<ParticleSystem>(true);

            int count = _particles.Length;

            if (_particleColors == null || _particleColors.Length != count)
            {
                _particleColors = new ParticleSystem.MinMaxGradient[count];
                _particleColorsBlack = new ParticleSystem.MinMaxGradient[count];
                _particleFades = new bool[count];
            }

            Transform bg = _bgIndex >= 0 ? _layers[_bgIndex] : null;

            for (int i = 0; i < count; i++)
            {
                // 设计态起始色必须在这里抓：Apply 每换一次极性都要按它重算，不能拿翻转后的再翻
                _particleColors[i] = _particles[i].main.startColor;
                _particleColorsBlack[i] = Invert(_particleColors[i]);
                _particleFades[i] = bg == null || !_particles[i].transform.IsChildOf(bg);
            }
        }

        /// <summary>镜头可能比本组件晚就位，拿不到就每帧再试一次；换场景后旧引用会变伪 null，这里会重新抓并重设基准</summary>
        private bool EnsureCamera()
        {
            if (_camera != null) return true;

            _camera = playerCamera != null ? playerCamera : Camera.main != null ? Camera.main.transform : null;
            if (_camera == null) return false;

            _cameraStartX = _camera.position.x;
            _cameraStartY = _camera.position.y;
            _lastOffsetX = float.NaN;
            _lastOffsetY = float.NaN;

            return true;
        }

        public void Apply(WorldPolarity world)
        {
            // 已在场的粒子是就地翻转的，同极性重复报表（进关会重播一次）翻两下就回到原色，必须先挡住
            if (_hasWorld && _world == world) return;

            _hasWorld = true;
            _world = world;

            bool black = world == WorldPolarity.Black;

            for (int i = 0; i < _sprites.Length; i++)
            {
                Color c = black ? Invert(_spriteColors[i]) : _spriteColors[i];
                c.a = _spriteColors[i].a * (_spriteFades[i] ? _fade : 1f);

                _sprites[i].color = c;
            }

            for (int i = 0; i < _particles.Length; i++) InvertParticles(i, black);
        }

        public void ApplyFade(float scale)
        {
            _fade = scale;
            RefreshFade();
        }

        /// <summary>每帧只重写参与淡入的东西：BG 层常驻不碰，颜色已由 Apply 定好</summary>
        private void RefreshFade()
        {
            for (int i = 0; i < _sprites.Length; i++)
            {
                if (!_spriteFades[i]) continue;

                Color c = _sprites[i].color;
                c.a = _spriteColors[i].a * _fade;
                _sprites[i].color = c;
            }

            bool black = _hasWorld && _world == WorldPolarity.Black;

            for (int i = 0; i < _particles.Length; i++)
            {
                if (!_particleFades[i]) continue;

                ParticleSystem ps = _particles[i];
                if (ps == null) continue;

                ParticleSystem.MainModule main = ps.main;
                main.startColor = ScaleAlpha(black ? _particleColorsBlack[i] : _particleColors[i], _fade);
            }
        }

        private void InvertParticles(int index, bool black)
        {
            ParticleSystem ps = _particles[index];
            if (ps == null) return;

            // 换世界就把在场粒子抹掉：淡入是从空场重新长，留着旧粒子只会在半透明里透出上一版的颜色
            ps.Clear();

            float scale = _particleFades[index] ? _fade : 1f;

            ParticleSystem.MainModule main = ps.main;
            main.startColor = ScaleAlpha(black ? _particleColorsBlack[index] : _particleColors[index], scale);
        }

        /// <summary>只缩透明度；渐变模式（Gradient / TwoGradients）的彩键不参与，恒按设计值播</summary>
        private static ParticleSystem.MinMaxGradient ScaleAlpha(ParticleSystem.MinMaxGradient g, float scale)
        {
            switch (g.mode)
            {
                case ParticleSystemGradientMode.Color:
                    Color single = g.color;
                    single.a *= scale;
                    return new ParticleSystem.MinMaxGradient(single);

                case ParticleSystemGradientMode.TwoColors:
                    Color min = g.colorMin;
                    Color max = g.colorMax;
                    min.a *= scale;
                    max.a *= scale;
                    return new ParticleSystem.MinMaxGradient(min, max);

                default:
                    return g;
            }
        }

        private static Color Invert(Color c) => new(1f - c.r, 1f - c.g, 1f - c.b, c.a);

        /// <summary>颜色随生命模块的彩键不参与翻转，只有白键渐变（纯 alpha 淡出）才是精确的</summary>
        private static ParticleSystem.MinMaxGradient Invert(ParticleSystem.MinMaxGradient g)
        {
            switch (g.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(Invert(g.color));
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(Invert(g.colorMin), Invert(g.colorMax));
                case ParticleSystemGradientMode.Gradient:
                    return new ParticleSystem.MinMaxGradient(Invert(g.gradient));
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(Invert(g.gradientMin), Invert(g.gradientMax));
                default:
                    return g;
            }
        }

        private static Gradient Invert(Gradient source)
        {
            GradientColorKey[] keys = source.colorKeys;

            for (int i = 0; i < keys.Length; i++) keys[i].color = Invert(keys[i].color);

            Gradient inverted = new();
            inverted.SetKeys(keys, source.alphaKeys);
            inverted.mode = source.mode;

            return inverted;
        }
    }
}
