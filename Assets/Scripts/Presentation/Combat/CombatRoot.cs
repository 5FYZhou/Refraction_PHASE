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
        private const string BulletPath = "Prefabs/Bullet";

        [SerializeField]
        private PlayerController playerController;

        [SerializeField]
        private GameObject afterimagePrefab;

        [SerializeField]
        private GameObject bulletPrefab;

        private AfterimageLogic _afterimages;
        private AfterimageField _field;
        private BulletLogic _bullets;
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

            AttachBullets();

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

            _levelMgr = new LevelManager(_root.LevelId, _root.DoorReader, _root.World);
            _levelMgr.Init();

            MonoBehaviour[] all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] is IReverse reverse) _levelMgr.RegisterReverse(reverse);
            }
        }

        public void FixedTick(float deltaTime)
        {
            if (_afterimages != null)
            {
                _afterimages.Tick(Time.fixedTime);

                if (_field != null) _field.Sync();
            }

            _bullets?.Tick(deltaTime);
        }

        private void AttachBullets()
        {
            if (_bullets != null) return;

            GameObject host = new(nameof(BulletField));
            host.transform.SetParent(transform, false);

            GameObject prefab = bulletPrefab != null
                ? bulletPrefab
                : Resources.Load<GameObject>(BulletPath);

            BulletField field = host.AddComponent<BulletField>();
            field.Init(prefab, playerController.transform, playerController.HitSink);

            _bullets = new BulletLogic(field);

            playerController.BindBulletLauncher(_bullets);
        }
    }
}
