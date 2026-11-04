using Refraction.Foundation;
using Refraction.Logic.Input;
using Refraction.Logic.Level;
using Refraction.Presentation.Actor;
using Refraction.Presentation.Level;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Presentation
{
    public class GameRoot : MonoBehaviour
    {
        private bool _initialized = false;

        [SerializeField]
        private GroundIniter groundIniter;
        [SerializeField]
        private LevelReader levelReader;
        [SerializeField]
        private PlayerController playerController;
        private InputProvider _inputProvider;
        private LevelManager _levelMgr;

        protected  void Awake()
        {
            //base.Awake();
            Assemble();
        }

        private void OnEnable()
        {
            _inputProvider.Init();
            _levelMgr.Init();
        }

        private void OnDisable()
        {
            _inputProvider.Dispose();
            _levelMgr.Dispose();
        }

        /// <summary>
        /// 组装
        /// </summary>
        private void Assemble()
        {
            groundIniter.Init();
            _inputProvider = new();
            playerController.Attach(_inputProvider);
            _levelMgr = new LevelManager(levelReader.Id);
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
