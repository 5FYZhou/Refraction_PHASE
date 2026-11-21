using NUnit.Framework;
using Refraction.Data;
using Refraction.Logic;
using Refraction.Logic.Actor;
using Refraction.Logic.Combat;
using Refraction.Logic.Events;
using Refraction.Logic.Input;
using Refraction.Logic.Level;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Tests
{
    /// <summary>弹反返还的正式入口在玩家逻辑上：打回一颗敌弹之后，空中那一份突进次数要能再用</summary>
    public sealed class 空中突进弹反返还Test
    {
        private const float Dt = 0.02f;

        private PlayerConfig _config;
        private PlayerLogic _player;

        [SetUp]
        public void 建立玩家()
        {
            _config = ScriptableObject.CreateInstance<PlayerConfig>();
            _config.blackDashPrepareTime = 0f;
            _config.blackDashTimeUp = 0.04f;
            _config.blackDashRecoverTime = 0f;
            _config.blackDashUpCooldown = 0f;
            _config.blackDashAirCount = 1;
            _config.blackDashReboundRefund = 1;

            _player = new(new 空电机(), _config, new InputBuffer(0.1f, 60), null, null, new 空盒子());

            EventBus<WorldPolarityChanged>.Publish(new(WorldPolarity.Black));
        }

        [TearDown]
        public void 释放玩家()
        {
            _player.Dispose();
            EventBus<WorldPolarityChanged>.Clear();
            EventBus<PlayerPolarityChanged>.Clear();
            EventBus<PlayerHealthChanged>.Clear();
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void 弹反打回敌弹后_空中再突一次()
        {
            走到(0f, false);
            Assert.AreEqual(AttackStateTag.None, _player.AttackTag, "空中不按攻击就是空闲");

            走到(0.01f, true);
            Assert.AreEqual(AttackStateTag.BlackDashUp, _player.AttackTag, "空中第一次斜上突进起手");

            走到(0.05f, false);
            Assert.AreEqual(AttackStateTag.None, _player.AttackTag, "突进整段走完回到空闲");

            走到(0.06f, true);
            Assert.AreEqual(AttackStateTag.None, _player.AttackTag, "次数用光，空中按不出来");

            _player.OnReboundReflected(Vector2.right);

            走到(0.07f, true);
            Assert.AreEqual(AttackStateTag.BlackDashUp, _player.AttackTag, "弹反返还之后空中能再突一次");
        }

        private void 走到(float now, bool attack)
        {
            InputSnapshot input = new(new Vector2(0f, 1f), false, false, false, false, attack, false, false, false, attack);
            WorldInfo world = new(false, false, Vector2.zero, 1, Vector2.zero);

            _player.FixedTick(new LogicContext(input, world, new GameTime(now, Dt)));
        }

        private sealed class 空盒子 : IBlackAttackView
        {
            public void BeginFlat() { }

            public void BeginDash(BlackAttackKind kind) { }

            public void End() { }

            public void SetHitHandler(System.Action onHit) { }
        }

        private sealed class 空电机 : IMoveMotor
        {
            public float BodyHeight => 0.9f;

            public float FeetOffsetY => 0f;

            public int FaceSign => 1;

            public void MoveTowardsX(Vector2 direction, float speed) { }
            public void BrakeTowardsX() { }
            public void AddImpulse(Vector2 deltaVelocity) { }
            public void AddExternalVelocity(Vector2 velocity) { }
            public void SetVelocityX(float speed) { }
            public void SetVelocityY(float speed) { }
            public void SnapVelocity(Vector2 velocity) { }
            public void SetSpeedScaleX(float scale) { }
            public void ScaleVelocityY(float multiplier) { }
            public void SetGravityEnable(bool enable) { }
            public void SetGravity(float gravity) { }
            public void SetBodyScale(float scale) { }
            public void BeginStep(float now, float deltaTime) { }
            public void Commit() { }
            public void FaceTowards(Vector2 dir) { }
            public void ApplyGravity(in LogicContext ctx) { }
            public void SpawnAt(Vector2 position, Vector2 velocity) { }
            public bool CanStandUp(float standHeight, float crouchHeight) => true;
            public void CommitGates(in MoveGates gates) { }
            public void ApplyGates() { }
        }
    }
}
