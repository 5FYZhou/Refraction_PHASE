using Cinemachine;
using NUnit.Framework;
using Refraction.Presentation.CameraRig;
using UnityEngine;

namespace Refraction.Tests
{
    public sealed class 镜头震动Test
    {
        [Test]
        public void 自动补的冲量源_必须是均匀模式且走通道1()
        {
            GameObject host = new("CameraController");

            try
            {
                CameraController controller = host.AddComponent<CameraController>();
                controller.Attach();

                CinemachineImpulseSource source = host.GetComponent<CinemachineImpulseSource>();

                Assert.IsNotNull(source, "没在 Inspector 里连冲量源时要自己补一个，否则发不出震动");

                CinemachineImpulseDefinition definition = source.m_ImpulseDefinition;

                Assert.AreEqual(CinemachineImpulseDefinition.ImpulseTypes.Uniform, definition.m_ImpulseType,
                    "默认定义是 Legacy 且没有 RawSignal，不改成 Uniform 就一点震动都不会有");

                Assert.AreEqual(1, definition.m_ImpulseChannel,
                    "通道要对上 Virtual Camera 预制体上 ImpulseListener 的 ChannelMask，否则监听不到");

                Assert.Greater(definition.m_ImpulseDuration, 0f, "时长必须大于 0，否则冲量事件直接被丢掉");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void 重复装配_只补一个冲量源()
        {
            GameObject host = new("CameraController");

            try
            {
                CameraController controller = host.AddComponent<CameraController>();
                controller.Attach();
                controller.Attach();

                Assert.AreEqual(1, host.GetComponents<CinemachineImpulseSource>().Length,
                    "Attach 必须幂等：重复装配不能再叠一个冲量源");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
