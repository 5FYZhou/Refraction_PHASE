using NUnit.Framework;
using Refraction.Data;
using Refraction.Presentation.Actor;
using UnityEngine;

namespace Refraction.Tests
{
    public sealed class 敌人碰撞体Test
    {
        private const int DefaultLayer = 0;

        private GameObject _root;
        private EnemyColliderConfig _collider;

        [SetUp]
        public void 建立敌人()
        {
            _root = new GameObject(nameof(敌人碰撞体Test));
            _collider = ScriptableObject.CreateInstance<EnemyColliderConfig>();
        }

        [TearDown]
        public void 清理()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            if (_collider != null) Object.DestroyImmediate(_collider);
        }

        [Test]
        public void 默认配置_与工程层号与子物体名一致()
        {
            Assert.AreEqual(7, _collider.playerLayer, "玩家在第 7 层");
            Assert.AreEqual(8, _collider.enemyLayer, "敌人在第 8 层");
            Assert.AreEqual("BodyCollider", _collider.bodyChildName, "三根判定盒按名字找");
            Assert.AreEqual("AttackCollider", _collider.attackChildName, "三根判定盒按名字找");
            Assert.AreEqual("DetectCollider", _collider.detectChildName, "三根判定盒按名字找");
        }

        [Test]
        public void 身体盒_排除玩家与敌人层_保持实体()
        {
            BoxCollider2D body = Box(_collider.bodyChildName);

            EnemyColliderSetup.Apply(_root.transform, _collider);

            Assert.IsFalse(body.isTrigger, "身体盒要挡物理，不能变成触发器");
            Assert.AreEqual(_collider.BodyExcludeMask, body.excludeLayers.value, "玩家与同类都不参与推挤");
        }

        [Test]
        public void 攻击盒_多颗碰撞体全部设成触发器_只排敌人层()
        {
            Box(_collider.bodyChildName);

            Transform attack = Child(_collider.attackChildName);
            BoxCollider2D box = attack.gameObject.AddComponent<BoxCollider2D>();
            CircleCollider2D circle = attack.gameObject.AddComponent<CircleCollider2D>();

            box.isTrigger = false;
            circle.excludeLayers = 0;

            EnemyColliderSetup.Apply(_root.transform, _collider);

            Assert.IsTrue(box.isTrigger, "预制体上漏勾的盒子也要变成触发器");
            Assert.IsTrue(circle.isTrigger, "本来就勾着的保持触发器");
            Assert.AreEqual(_collider.EnemyMask, box.excludeLayers.value, "攻击盒排除敌人层，不误伤同类");
            Assert.AreEqual(_collider.EnemyMask, circle.excludeLayers.value, "第二颗碰撞体同样被追上");
        }

        [Test]
        public void 探测盒_只看玩家层_其它层全部排除()
        {
            Box(_collider.bodyChildName);

            BoxCollider2D box = Box(_collider.detectChildName);

            box.isTrigger = false;
            box.includeLayers = ~0;
            box.excludeLayers = 0;

            EnemyColliderSetup.Apply(_root.transform, _collider);

            Assert.IsTrue(box.isTrigger, "探测盒必须是触发器");
            Assert.AreEqual(_collider.PlayerMask, box.includeLayers.value, "只强制包含玩家层");
            Assert.AreEqual(0, box.excludeLayers.value & _collider.PlayerMask, "玩家层不能出现在排除表里");
            Assert.AreEqual(_collider.EnemyMask, box.excludeLayers.value & _collider.EnemyMask, "同类不进视线");
            Assert.AreEqual(1 << DefaultLayer, box.excludeLayers.value & (1 << DefaultLayer), "场景默认层也不进视线");
        }

        [Test]
        public void 换层号只改配置_判定盒跟着走()
        {
            _collider.playerLayer = 9;
            _collider.enemyLayer = 10;

            BoxCollider2D body = Box(_collider.bodyChildName);
            BoxCollider2D detect = Box(_collider.detectChildName);

            EnemyColliderSetup.Apply(_root.transform, _collider);

            Assert.AreEqual((1 << 9) | (1 << 10), body.excludeLayers.value, "身体盒读配置里的两个层号");
            Assert.AreEqual(1 << 9, detect.includeLayers.value, "探测盒只认配置里写的玩家层");
        }

        [Test]
        public void 关掉身体盒排同类_只有身体盒放开()
        {
            _collider.bodyExcludeEnemy = false;

            BoxCollider2D body = Box(_collider.bodyChildName);
            BoxCollider2D attack = Box(_collider.attackChildName);
            BoxCollider2D detect = Box(_collider.detectChildName);

            EnemyColliderSetup.Apply(_root.transform, _collider);

            Assert.AreEqual(_collider.PlayerMask, body.excludeLayers.value, "身体盒只剩玩家被排掉，同类会互相挡");
            Assert.AreEqual(_collider.EnemyMask, attack.excludeLayers.value, "攻击盒照旧排敌人层，不受开关影响");
            Assert.AreEqual(_collider.PlayerMask, detect.includeLayers.value, "探测盒照旧只认玩家层");
            Assert.AreEqual(_collider.EnemyMask, detect.excludeLayers.value & _collider.EnemyMask, "同类照旧不进视线");
        }

        [Test]
        public void 缺子物体_只报警告_不抛异常()
        {
            Assert.DoesNotThrow(() => EnemyColliderSetup.Apply(_root.transform, _collider), "碰撞体缺一根不能把装配卡死");
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
