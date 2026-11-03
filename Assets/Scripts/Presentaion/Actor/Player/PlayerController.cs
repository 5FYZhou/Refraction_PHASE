using Refraction.Data;
using Refraction.Logic;
using Refraction.Logic.Actor;
using Refraction.Logic.Input;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Presentation.Actor
{
    public class PlayerController : MonoBehaviour
    {
        private bool _attached = false;
        private InputProvider _inputProvider;
        private PlayerLogic _logic;
        [SerializeField]
        private MoveMotor moveMotor;
        [SerializeField]
        private PlayerConfig config;


        public void Attach(InputProvider inputProvider)
        {
            _inputProvider = inputProvider;
            _attached = true;
            _logic = new(moveMotor);
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
            WorldInfo worldInfo = new(transform.position, moveMotor.Rb.velocity);
            GameTime gameTime = new(Time.fixedTime, Time.fixedDeltaTime);
            LogicContext context = new(snapshot, worldInfo, gameTime);

            _logic.FixedTick(context);
        }
    }
}
