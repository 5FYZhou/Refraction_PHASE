using NUnit.Framework;
using Refraction.Data;
using Refraction.Logic.Actor;
using Refraction.Presentation.Actor;
using UnityEngine;

namespace Refraction.Tests
{
    public sealed class 敌人配色Test
    {
        private const float Tolerance = 0.0001f;

        private static readonly Color Design = new(0.2f, 0.4f, 0.6f, 0.5f);

        private GameObject _root;
        private SpriteRenderer _view;

        [SetUp]
        public void 建立敌人()
        {
            _root = new GameObject(nameof(敌人配色Test));

            _view = Child("View").gameObject.AddComponent<SpriteRenderer>();
            _view.color = Design;
        }

        [TearDown]
        public void 清理()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [Test]
        public void 白世界_巡逻用初始色()
        {
            EnemyTint tint = new(_root.transform);

            tint.Apply(EnemyVisual.Patrol, false);

            颜色应等于(Design, _view.color, "平时不读资产，摆什么就是什么");
        }

        [Test]
        public void 黑世界_初始色rgb取反_alpha不动()
        {
            EnemyTint tint = new(_root.transform);

            tint.Apply(EnemyVisual.Chase, true);

            颜色应等于(new Color(0.8f, 0.6f, 0.4f, 0.5f), _view.color, "黑世界只反 rgb，alpha 保留");
        }

        [Test]
        public void 反复反转_不会越取越偏()
        {
            EnemyTint tint = new(_root.transform);

            for (int i = 0; i < 5; i++)
            {
                tint.Apply(EnemyVisual.Patrol, true);
                tint.Apply(EnemyVisual.Patrol, false);
            }

            颜色应等于(Design, _view.color, "缓存的是设计态颜色，取反不写回缓存");
        }

        [Test]
        public void 攻击三段_读配置色()
        {
            EnemyTint tint = new(_root.transform);
            EnemyVisualConfig config = EnemyVisualConfig.Default;

            tint.Apply(EnemyVisual.Windup, false);

            颜色应等于(config.windupWhite, _view.color, "前摇读资产");

            tint.Apply(EnemyVisual.Strike, true);

            颜色应等于(config.strikeBlack, _view.color, "攻击读资产，黑世界取黑色那格");

            tint.Apply(EnemyVisual.Recover, false);

            颜色应等于(config.recoverWhite, _view.color, "后摇读资产");
        }

        [Test]
        public void 攻击结束回巡逻_恢复初始色()
        {
            EnemyTint tint = new(_root.transform);

            tint.Apply(EnemyVisual.Strike, false);
            tint.Apply(EnemyVisual.Patrol, false);

            颜色应等于(Design, _view.color, "配置色只借用，不收招不留在身上");
        }

        [Test]
        public void 没有View子物体_直接改根节点下的renderer()
        {
            Object.DestroyImmediate(_view.gameObject);

            SpriteRenderer fallback = _root.AddComponent<SpriteRenderer>();

            fallback.color = Design;

            EnemyTint tint = new(_root.transform);

            tint.Apply(EnemyVisual.Return, true);

            颜色应等于(new Color(0.8f, 0.6f, 0.4f, 0.5f), fallback.color, "找不到 View 就退回根节点，不能一个都不改");
        }

        private Transform Child(string childName)
        {
            GameObject child = new(childName);
            child.transform.SetParent(_root.transform, false);

            return child.transform;
        }

        private static void 颜色应等于(Color expected, Color actual, string why)
        {
            Assert.AreEqual(expected.r, actual.r, Tolerance, why + "（r）");
            Assert.AreEqual(expected.g, actual.g, Tolerance, why + "（g）");
            Assert.AreEqual(expected.b, actual.b, Tolerance, why + "（b）");
            Assert.AreEqual(expected.a, actual.a, Tolerance, why + "（a）");
        }
    }
}
