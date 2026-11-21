using UnityEngine;

namespace Refraction.Presentation.Actor
{
    /// <summary>敌人底色：只跟随世界极性取反 View 的初始色，任何状态都不改色</summary>
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
        public void Apply(bool black)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                Color c = _base[i];

                _renderers[i].color = black
                    ? new Color(1f - c.r, 1f - c.g, 1f - c.b, c.a)
                    : c;
            }
        }
    }
}
