using Refraction.Foundation;
using Refraction.Logic.Input;
using Refraction.Presentation.Actor;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Presentation
{
    public class GameRoot : Singleton<GameRoot>
    {
        [SerializeField]
        private PlayerController playerController;
        private InputProvider _inputProvider;

        protected override void Awake()
        {
            base.Awake();
            Assemble();
        }

        private void OnEnable()
        {
            _inputProvider.Init();
        }

        private void OnDisable()
        {
            _inputProvider.Dispose();
        }

        /// <summary>
        /// 组装
        /// </summary>
        private void Assemble()
        {
            _inputProvider = new();
            playerController.Attach(_inputProvider);
        }

        private void Update()
        {
            _inputProvider.Tick();
        }

        private void FixedUpdate()
        {
            playerController.FixedTick();
        }
    }
}
