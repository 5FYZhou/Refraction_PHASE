using NUnit.Framework;
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
        public void 白世界_用初始色()
        {
            EnemyTint tint = new(_root.transform);

            tint.Apply(false);

            颜色应等于(Design, _view.color, "白世界摆什么就是什么");
        }

        [Test]
        public void 黑世界_初始色rgb取反_alpha不动()
        {
            EnemyTint tint = new(_root.transform);

            tint.Apply(true);

            颜色应等于(new Color(0.8f, 0.6f, 0.4f, 0.5f), _view.color, "黑世界只反 rgb，alpha 保留");
        }

        [Test]
        public void 反复反转_不会越取越偏()
        {
            EnemyTint tint = new(_root.transform);

            for (int i = 0; i < 5; i++)
            {
                tint.Apply(true);
                tint.Apply(false);
            }

            颜色应等于(Design, _view.color, "缓存的是设计态颜色，取反不写回缓存");
        }

        [Test]
        public void 同一节点多个renderer_一起取反()
        {
            SpriteRenderer extra = Child("Extra").gameObject.AddComponent<SpriteRenderer>();

            extra.color = new Color(1f, 0f, 0.25f, 1f);

            EnemyTint tint = new(_root.transform);

            tint.Apply(true);

            颜色应等于(new Color(0.8f, 0.6f, 0.4f, 0.5f), _view.color, "View 下的 renderer 都算");
            颜色应等于(new Color(0f, 1f, 0.75f, 1f), extra.color, "第二颗同样取反");
        }

        [Test]
        public void 没有View子物体_直接改根节点下的renderer()
        {
            Object.DestroyImmediate(_view.gameObject);

            SpriteRenderer fallback = _root.AddComponent<SpriteRenderer>();

            fallback.color = Design;

            EnemyTint tint = new(_root.transform);

            tint.Apply(true);

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
