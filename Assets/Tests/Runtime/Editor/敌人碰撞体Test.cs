using NUnit.Framework;
using Refraction.Presentation.Actor;
using UnityEngine;

namespace Refraction.Tests
{
    public sealed class 敌人碰撞体Test
    {
        private const int DefaultLayer = 0;
        private const int PlayerLayer = 7;
        private const int EnemyLayer = 8;

        private GameObject _root;

        [SetUp]
        public void 建立敌人()
        {
            _root = new GameObject(nameof(敌人碰撞体Test));
        }

        [TearDown]
        public void 清理()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        }

        [Test]
        public void 身体盒_排除玩家与敌人层_保持实体()
        {
            BoxCollider2D body = Box(EnemyColliderSetup.BodyChildName);

            EnemyColliderSetup.Apply(_root.transform);

            Assert.IsFalse(body.isTrigger, "身体盒要挡物理，不能变成触发器");
            Assert.AreEqual((1 << PlayerLayer) | (1 << EnemyLayer), body.excludeLayers.value, "玩家与同类都不参与推挤");
        }

        [Test]
        public void 攻击盒_多颗碰撞体全部设成触发器_只排敌人层()
        {
            Box(EnemyColliderSetup.BodyChildName);

            Transform attack = Child(EnemyColliderSetup.AttackChildName);
            BoxCollider2D box = attack.gameObject.AddComponent<BoxCollider2D>();
            CircleCollider2D circle = attack.gameObject.AddComponent<CircleCollider2D>();

            box.isTrigger = false;
            circle.excludeLayers = 0;

            EnemyColliderSetup.Apply(_root.transform);

            Assert.IsTrue(box.isTrigger, "预制体上漏勾的盒子也要变成触发器");
            Assert.IsTrue(circle.isTrigger, "本来就勾着的保持触发器");
            Assert.AreEqual(1 << EnemyLayer, box.excludeLayers.value, "攻击盒排除敌人层，不误伤同类");
            Assert.AreEqual(1 << EnemyLayer, circle.excludeLayers.value, "第二颗碰撞体同样被追上");
        }

        [Test]
        public void 探测盒_只看玩家层_其它层全部排除()
        {
            Box(EnemyColliderSetup.BodyChildName);

            BoxCollider2D box = Box(EnemyColliderSetup.DetectChildName);

            box.isTrigger = false;
            box.includeLayers = ~0;
            box.excludeLayers = 0;

            EnemyColliderSetup.Apply(_root.transform);

            Assert.IsTrue(box.isTrigger, "探测盒必须是触发器");
            Assert.AreEqual(1 << PlayerLayer, box.includeLayers.value, "只强制包含玩家层");
            Assert.AreEqual(0, box.excludeLayers.value & (1 << PlayerLayer), "玩家层不能出现在排除表里");
            Assert.AreEqual(1 << EnemyLayer, box.excludeLayers.value & (1 << EnemyLayer), "同类不进视线");
            Assert.AreEqual(1 << DefaultLayer, box.excludeLayers.value & (1 << DefaultLayer), "场景默认层也不进视线");
        }

        [Test]
        public void 缺子物体_只报警告_不抛异常()
        {
            Assert.DoesNotThrow(() => EnemyColliderSetup.Apply(_root.transform), "碰撞体缺一根不能把装配卡死");
        }

        private Transform Child(string childName)
        {
            GameObject child = new(childName);
            child.transform.SetParent(_root.transform, false);

            return child.transform;
        }

        private BoxCollider2D Box(string childName) => Child(childName).gameObject.AddComponent<BoxCollider2D>();
    }
}
