using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Combat;
using Refraction.Presentation.Actor;
using UnityEngine;

namespace Refraction.Presentation.Combat
{
    public class CombatRoot : MonoBehaviour, ISceneRoot, IPhysicsTicked
    {
        public int Order => SceneOrder.World;

        private const string AfterimagePath = "Prefabs/AfterImage";

        [SerializeField]
        private PlayerController playerController;

        [SerializeField]
        private GameObject afterimagePrefab;

        private AfterimageLogic _afterimages;
        private AfterimageField _field;
        private GameRoot _root;

        private void Start()
        {
            _root = GameRoot.Instance;
            _root.RegisterSceneRoot(this);
        }

        private void OnDestroy()
        {
            if (_root != null) _root.UnregisterSceneRoot(this);
        }

        public void Attach()
        {
            Require.Assigned(playerController, nameof(CombatRoot), nameof(playerController));

            if (_afterimages != null) return;

            PlayerConfig config = playerController.Config;

            _afterimages = new AfterimageLogic(config);
            playerController.BindAfterimageSpawner(_afterimages);

            if (playerController.View == null) return;

            GameObject host = new(nameof(AfterimageField));
            host.transform.SetParent(transform, false);

            GameObject prefab = afterimagePrefab != null
                ? afterimagePrefab
                : Resources.Load<GameObject>(AfterimagePath);

            _field = host.AddComponent<AfterimageField>();
            _field.Init(_afterimages, config, playerController.View, playerController.transform, prefab);
        }

        public void FixedTick(float deltaTime)
        {
            if (_afterimages == null) return;

            _afterimages.Tick(Time.fixedTime);

            if (_field != null) _field.Sync();
        }
    }
}
