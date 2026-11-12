using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Combat;
using Refraction.Logic.Level;
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
        private LevelManager _levelMgr;
        private GameRoot _root;

        private void Start()
        {
            _root = GameRoot.Instance;
            _root.RegisterSceneRoot(this);
        }

        private void OnDestroy()
        {
            _levelMgr?.Dispose();
            _levelMgr = null;

            if (_root != null) _root.UnregisterSceneRoot(this);
        }

        public void Attach()
        {
            _root ??= GameRoot.Instance;

            AttachLevel();

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

        private void AttachLevel()
        {
            if (_levelMgr != null) return;

            if (_root == null)
            {
                Debug.LogError("[CombatRoot] 没拿到 GameRoot，门推进与世界反转在本关都不会生效。");
                return;
            }

            _levelMgr = new LevelManager(_root.LevelId, _root.DoorReader);
            _levelMgr.Init();

            MonoBehaviour[] all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] is IReverse reverse) _levelMgr.RegisterReverse(reverse);
            }
        }

        public void FixedTick(float deltaTime)
        {
            if (_afterimages == null) return;

            _afterimages.Tick(Time.fixedTime);

            if (_field != null) _field.Sync();
        }
    }
}
