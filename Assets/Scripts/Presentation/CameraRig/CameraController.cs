using Cinemachine;
using Refraction.Logic.Events;
using UnityEngine;

namespace Refraction.Presentation.CameraRig
{
    /// <summary>镜头震动：订阅 CameraShakeRequested，把强度原样交给 Cinemachine 冲量</summary>
    /// <remarks>震动只对挂了 CinemachineImpulseListener 的 Virtual Camera 生效，通道必须与那边的 ChannelMask 一致</remarks>
    public sealed class CameraController : MonoBehaviour, ISceneRoot
    {
        public int Order => SceneOrder.World;

        [Tooltip("镜头震动的冲量源。留空则运行时在本物体上补一个，冲量固定走通道 1。")]
        [SerializeField]
        private CinemachineImpulseSource impulseSource;

        [Tooltip("自动补冲量源时用的震动时长（秒）：强度仍由事件里的 force 决定，这里只管震多久。")]
        [Min(0.01f)]
        [SerializeField]
        private float impulseDuration = 0.2f;

        private GameRoot _root;

        private void Awake() => EnsureSource();

        private void OnEnable() => EventBus<CameraShakeRequested>.Subscribe(OnShakeRequested);

        private void OnDisable() => EventBus<CameraShakeRequested>.Unsubscribe(OnShakeRequested);

        private void Start()
        {
            _root = GameRoot.Instance;
            if (_root != null) _root.RegisterSceneRoot(this);
        }

        private void OnDestroy()
        {
            if (_root != null) _root.UnregisterSceneRoot(this);
        }

        public void Attach() => EnsureSource();

        private void OnShakeRequested(CameraShakeRequested evt)
        {
            if (evt.force <= 0f) return;

            EnsureSource();
            if (impulseSource != null) impulseSource.GenerateImpulse(evt.force);
        }

        /// <summary>冲量源的默认定义是 Legacy 且没有 RawSignal，直接补组件发不出任何震动</summary>
        private void EnsureSource()
        {
            if (impulseSource != null) return;

            impulseSource = GetComponent<CinemachineImpulseSource>();
            if (impulseSource != null) return;

            impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();

            CinemachineImpulseDefinition definition = impulseSource.m_ImpulseDefinition;
            definition.m_ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            definition.m_ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Recoil;
            definition.m_ImpulseDuration = impulseDuration;
            definition.m_ImpulseChannel = 1;
        }
    }
}
