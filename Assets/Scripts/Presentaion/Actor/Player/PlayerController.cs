using Refraction.Data;
using Refraction.Logic;
using Refraction.Logic.Actor;
using Refraction.Logic.Input;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Presentation.Actor
{
    public sealed class PlayerController : MonoBehaviour
    {
        private bool _attached = false;
        private InputProvider _inputProvider;
        private InputBuffer _buffer;
        private PlayerLogic _logic;
        [SerializeField]
        private MoveMotor moveMotor;
        [SerializeField]
        private PlayerConfig config;


        public void Attach(InputProvider inputProvider)
        {
            if(moveMotor  == null)
            {
                return;
            }
            if(config == null)
            {
                return;
            }
            _inputProvider = inputProvider;
            _buffer = new (
                Mathf.Max(config.inputBufferTime, config.dashBufferTime),
                Mathf.RoundToInt(1f / Time.fixedDeltaTime)
                );
            _logic = new(moveMotor, config, _buffer);
            _attached = true;
        }

        private void Start()
        {
            if(!_attached)
            {
                Debug.LogError("未装配，无法运行");
            }
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
        }
    }
}
