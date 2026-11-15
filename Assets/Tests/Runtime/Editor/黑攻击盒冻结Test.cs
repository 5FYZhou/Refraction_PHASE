using NUnit.Framework;
using Refraction.Data;
using Refraction.Logic.Combat;
using Refraction.Presentation.Actor;
using Refraction.Presentation.Combat;
using UnityEngine;

namespace Refraction.Tests
{
    /// <summary>判定盒点亮后冻在开打那一刻的世界位置、不随玩家移动，攻击结束装回玩家层级、下一次按新站位重新初始化</summary>
    public sealed class 黑攻击盒冻结Test
    {
        private const float Eps = 1e-3f;

        private PlayerConfig _config;
        private GameObject _root;
        private GameObject _black;
        private GameObject _ahead;
        private GameObject _up;
        private GameObject _down;
        private BlackAttackView _view;

        [SetUp]
        public void 搭建玩家层级()
        {
            _config = ScriptableObject.CreateInstance<PlayerConfig>();

            _root = new GameObject("Player");
            _black = Child(_root, "BlackAttack");

            _ahead = Box(_black, "AheadCollider", new Vector3(1f, 0f, 0f));
            _up = Box(_black, "UpCollider", new Vector3(0f, 1f, 0f));
            _down = Box(_black, "DownCollider", new Vector3(0f, -1f, 0f));

            _view = new(_root.transform, _config, () => 1);
        }

        [TearDown]
        public void 拆掉层级()
        {
            // 盒开打时被摘成根物体，光销毁玩家带不走它们
            Dispose(_ahead);
            Dispose(_up);
            Dispose(_down);
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void 点亮后_盒脱开玩家并按开打位置冻住()
        {
            _view.Begin(BlackAttackKind.Ahead);

            Assert.IsNull(_ahead.transform.parent, "点亮那一帧盒就不再挂在玩家身上");
            Assert.IsTrue(_ahead.activeSelf, "点亮的那只开着");

            Vector3 frozen = _ahead.transform.position;
            _root.transform.position = new Vector3(5f, 3f, 0f);

            Assert.AreEqual(frozen.x, _ahead.transform.position.x, Eps, "玩家横向走开，判定盒不跟");
            Assert.AreEqual(frozen.y, _ahead.transform.position.y, Eps, "玩家纵向移动，判定盒不跟");
        }

        [Test]
        public void 只冻点亮的_另两只留在玩家身上且关着()
        {
            _view.Begin(BlackAttackKind.Up);

            Assert.IsTrue(_up.activeSelf, "上劈点亮 UpCollider");
            Assert.IsNull(_up.transform.parent, "点亮的盒脱开玩家");
            Assert.AreEqual(_black.transform, _ahead.transform.parent, "没点亮的平推盒仍在玩家层级里");
            Assert.IsFalse(_ahead.activeSelf, "平推盒关着");
            Assert.IsFalse(_down.activeSelf, "斜下盒关着");
        }

        [Test]
        public void 结束后_关掉并装回玩家层级()
        {
            _view.Begin(BlackAttackKind.Ahead);
            _root.transform.position = new Vector3(5f, 3f, 0f);
            _view.End();

            Assert.IsFalse(_ahead.activeSelf, "攻击结束就关掉");
            Assert.AreEqual(_black.transform, _ahead.transform.parent, "装回 BlackAttack 下面");
            Assert.AreEqual(1f, _ahead.transform.localPosition.x, Eps, "局部位置回到预制体那份");
            Assert.AreEqual(0f, _ahead.transform.localPosition.y, Eps, "局部纵向也回到预制体那份");
        }

        [Test]
        public void 下一次攻击_按玩家新站位重新冻结()
        {
            _root.transform.position = new Vector3(2f, 0f, 0f);
            _view.Begin(BlackAttackKind.Ahead);

            Assert.AreEqual(3f, _ahead.transform.position.x, Eps, "第一次攻击冻在 2 + 1");

            _view.End();
            _root.transform.position = new Vector3(6f, 4f, 0f);
            _view.Begin(BlackAttackKind.Ahead);

            Assert.AreEqual(7f, _ahead.transform.position.x, Eps, "第二次攻击按新站位重新初始化");
            Assert.AreEqual(4f, _ahead.transform.position.y, Eps, "纵向同样跟着玩家新位置");
        }

        [Test]
        public void 冻住之后_玩家转身也不影响判定盒()
        {
            _view.Begin(BlackAttackKind.Ahead);
            Vector3 frozen = _ahead.transform.position;

            Vector3 scale = _root.transform.localScale;
            scale.x = -1f;
            _root.transform.localScale = scale;

            Assert.AreEqual(frozen.x, _ahead.transform.position.x, Eps, "玩家翻面，冻住的盒不跟着镜像");
        }

        private static GameObject Child(GameObject parent, string name)
        {
            GameObject go = new(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        private static GameObject Box(GameObject parent, string name, Vector3 local)
        {
            GameObject go = Child(parent, name);

            go.transform.localPosition = local;
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<AttackBox>();

            return go;
        }

        private static void Dispose(GameObject go)
        {
            if (go != null) Object.DestroyImmediate(go);
        }
    }
}
