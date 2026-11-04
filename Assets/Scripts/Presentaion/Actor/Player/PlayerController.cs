using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic;
using Refraction.Logic.Actor;
using Refraction.Logic.Input;
using UnityEngine;

namespace Refraction.Presentation.Actor
{
    public sealed class PlayerController : MonoBehaviour
    {
        private const float _spawnY = -10f;

        private bool _attached = false;
        private InputProvider _inputProvider;
        private InputBuffer _buffer;
        private PlayerLogic _logic;

        [Tooltip("驱动刚体的执行器。逻辑层的移动/跳跃/冲刺指令最终都落到这里。")]
        [SerializeField]
        private MoveMotor moveMotor;

        [Tooltip("玩家参数资产。应与 MoveMotor 上引用的是同一份配置。")]
        [SerializeField]
        private PlayerConfig config;

        [Tooltip("重生点。掉到 _spawnY 以下后被送回这里。")]
        [SerializeField]
        private Transform _spawnT;

        public void Attach(InputProvider inputProvider)
        {
            Require.Assigned(moveMotor, nameof(PlayerController), nameof(moveMotor));
            Require.Assigned(config, nameof(PlayerController), nameof(config));

            _inputProvider = inputProvider;
            _buffer = new (
                Mathf.Max(config.inputBufferTime, config.dashBufferTime),
                Mathf.RoundToInt(1f / Time.fixedDeltaTime)
                );
            _logic = new(moveMotor, config, _buffer);
            _attached = true;
        }

        public void FixedTick()
        {
            if (!_attached) return;

            InputSnapshot snapshot = _inputProvider.ConsumeSnapshot();
            // 世界信息由 MoveMotor 的物理探测给出，本层只负责组装 Context
            WorldInfo worldInfo = moveMotor.SampleWorld();
            GameTime gameTime = new(Time.fixedTime, Time.fixedDeltaTime);
            LogicContext context = new(snapshot, worldInfo, gameTime);

            _buffer.Push(in snapshot, Time.fixedTime);
            _logic.FixedTick(context);
            if (context.WorldInfo.Pos.y < _spawnY)
            {
                _logic.SpawnTo(_spawnT ? _spawnT.position : new Vector2(0, 0));
            }
        }
    }
}
