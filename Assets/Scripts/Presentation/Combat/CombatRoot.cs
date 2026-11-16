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
        private BulletField _bulletField;
        private GameRoot _root;

        private void Start()
        {
            _root = GameRoot.Instance;
            _root.RegisterSceneRoot(this);
        }

        private void OnDestroy()
        {
            if (_bulletField != null) Destroy(_bulletField.gameObject);

            if (_root != null) _root.UnregisterSceneRoot(this);
        }

        public void Attach()
        {
            _root ??= GameRoot.Instance;

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

            GameObject prefab = bulletPrefab != null
                ? bulletPrefab
                : Resources.Load<GameObject>(BulletPath);

            _bulletField = host.AddComponent<BulletField>();
            _bulletField.Init(prefab, playerController.transform, playerController.HitSink);

            _bullets = new BulletLogic(_bulletField, true);

            playerController.BindBulletLauncher(_bullets);
        }
    }
}
