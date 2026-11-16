using Refraction.Data;
using Refraction.Logic.Actor;
using UnityEngine;

namespace Refraction.Presentation.Actor
{
    /// <summary>敌人换色：平时用 View 的初始色（黑世界取反），只有攻击三段才换成配置色</summary>
    public sealed class EnemyTint
    {
        private const string ViewChildName = "View";

        private readonly SpriteRenderer[] _renderers;
        private readonly Color[] _base;

        public EnemyTint(Transform root)
        {
            Transform view = root.Find(ViewChildName);

            _renderers = (view != null ? view : root).GetComponentsInChildren<SpriteRenderer>(true);
            _base = new Color[_renderers.Length];

            for (int i = 0; i < _renderers.Length; i++) _base[i] = _renderers[i].color;
        }

        /// <summary>初始色：黑世界按 255 - rgb 取反，alpha 不动</summary>
        public void ApplyBase(bool black)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                Color c = _base[i];

                _renderers[i].color = black
                    ? new Color(1f - c.r, 1f - c.g, 1f - c.b, c.a)
                    : c;
            }
        }

        /// <summary>按展示态上色：只有前摇 / 攻击 / 后摇读 EnemyVisualConfig，其余回初始色</summary>
        public void Apply(EnemyVisual visual, bool black)
        {
            if (!TryAttackColor(visual, black, out Color color))
            {
                ApplyBase(black);
                return;
            }

            for (int i = 0; i < _renderers.Length; i++) _renderers[i].color = color;
        }

        private static bool TryAttackColor(EnemyVisual visual, bool black, out Color color)
        {
            EnemyVisualConfig config = EnemyVisualConfig.Default;

            switch (visual)
            {
                case EnemyVisual.Windup: color = black ? config.windupBlack : config.windupWhite; return true;
                case EnemyVisual.Strike: color = black ? config.strikeBlack : config.strikeWhite; return true;
                case EnemyVisual.Recover: color = black ? config.recoverBlack : config.recoverWhite; return true;
            }

            color = default;
            return false;
        }
    }
}
