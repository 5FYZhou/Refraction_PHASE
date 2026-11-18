using Cinemachine;
using Refraction.Logic.Events;
using UnityEngine;

namespace Refraction.Presentation.CameraRig
{
    /// <summary>镜头震动：单向震动直接推一格冲量，四向抖动靠每帧轮换方向连发小脉冲</summary>
    /// <remarks>震动只对挂了 CinemachineImpulseListener 的 Virtual Camera 生效，通道必须与那边的 ChannelMask 一致</remarks>
    public sealed class CameraController : MonoBehaviour, ISceneRoot, IRenderTicked
    {
        private static readonly Vector2[] OmniDirections =
        {
            Vector2.right,
            Vector2.up,
            Vector2.left,
            Vector2.down,
        };

        public int Order => SceneOrder.World;

        [Tooltip("镜头震动的冲量源。留空则运行时在本物体上补一个，冲量固定走通道 1。")]
        [SerializeField]
        private CinemachineImpulseSource impulseSource;

        [Tooltip("世界震屏的衰减曲线：横轴是震动进度 0→1，纵轴是这一刻的振幅倍率。只对四向抖动生效。")]
        [SerializeField]
        private AnimationCurve omniDecay = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [Tooltip("世界震屏的抖动频率（次/秒）：越高越碎，20 左右接近手持抖动。")]
        [Min(1f)]
        [SerializeField]
        private float omniFrequency = 20f;

        private GameRoot _root;
        private CinemachineImpulseSource _omniSource;

        private float _omniAmp;
        private float _omniDuration;
        private float _omniRemain;
        private float _omniAcc;
        private int _omniIndex;

        private void Awake() => EnsureSource();

        private void OnEnable() => EventBus<RequestCameraShake>.Subscribe(OnRequestCameraShake);

        private void OnDisable()
        {
            EventBus<RequestCameraShake>.Unsubscribe(OnRequestCameraShake);

            _omniRemain = 0f;
        }

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

        public void RenderTick(float deltaTime)
        {
            if (_omniRemain <= 0f || deltaTime <= 0f) return;

            _omniRemain -= deltaTime;
            _omniAcc += deltaTime;

            float step = 1f / Mathf.Max(1f, omniFrequency);

            while (_omniAcc >= step)
            {
                _omniAcc -= step;
                FireOmniPulse(step);
            }
        }

        private void OnRequestCameraShake(RequestCameraShake evt)
        {
            if (evt.mode == CameraShakeMode.Omni)
            {
                BeginOmni(evt);
                return;
            }

            if (evt.impulse.sqrMagnitude <= 0f) return;

            EnsureSource();
            if (impulseSource == null) return;

            // 时长在 CreateEvent 时才从定义里读，逐次改写即可让每次请求各震各的
            if (impulseSource.m_ImpulseDefinition != null && evt.duration > 0f)
                impulseSource.m_ImpulseDefinition.m_ImpulseDuration = evt.duration;

            impulseSource.GenerateImpulseWithVelocity(new Vector3(evt.impulse.x, evt.impulse.y, 0f));
        }

        private void BeginOmni(RequestCameraShake evt)
        {
            float amp = evt.impulse.magnitude;
            if (amp <= 0f || evt.duration <= 0f) return;

            _omniAmp = amp;
            _omniDuration = evt.duration;
            _omniRemain = evt.duration;
            _omniAcc = 0f;
            _omniIndex = 0;

            FireOmniPulse(1f / Mathf.Max(1f, omniFrequency));
        }

        /// <summary>按当前进度取衰减后的振幅，沿下一条方向推一格：四个方向轮着来才是上下左右都晃</summary>
        private void FireOmniPulse(float step)
        {
            EnsureOmniSource();
            if (_omniSource == null) return;

            float progress = 1f - Mathf.Clamp01(_omniRemain / _omniDuration);

            float decay = omniDecay != null && omniDecay.length > 0 ? omniDecay.Evaluate(progress) : 1f - progress;

            float amp = _omniAmp * decay;
            if (amp <= 0f) return;

            CinemachineImpulseDefinition definition = _omniSource.m_ImpulseDefinition;
            if (definition != null) definition.m_ImpulseDuration = Mathf.Max(step * 2f, 0.02f);

            Vector2 dir = OmniDirections[_omniIndex & 3];
            _omniIndex++;

            _omniSource.GenerateImpulseWithVelocity(new Vector3(dir.x * amp, dir.y * amp, 0f));
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
            definition.m_ImpulseChannel = 1;
        }

        /// <summary>四向抖动单独一个源：脉冲的时长每帧在改，和受击共用一份定义会打乱正在播的那次</summary>
        private void EnsureOmniSource()
        {
            if (_omniSource != null) return;

            _omniSource = gameObject.AddComponent<CinemachineImpulseSource>();

            CinemachineImpulseDefinition definition = _omniSource.m_ImpulseDefinition;
            definition.m_ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            definition.m_ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
            definition.m_ImpulseChannel = 1;
        }
    }
}
