using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Combat;
using Refraction.Logic.Events;
using Refraction.Presentation.Actor;
using UnityEngine;

namespace Refraction.Presentation.Combat
{
    public class CombatRoot : MonoBehaviour, ISceneRoot, IPhysicsTicked, IRenderTicked
    {
        public int Order => SceneOrder.World;

        [SerializeField]
        private PlayerController playerController;

        [SerializeField]
        private GameObject afterimagePrefab;

        private AfterimageLogic _afterimages;
        private AfterimageField _field;
        private BulletLogic _bullets;
        private BulletField _bulletField;
        private EnemyMgr _enemies;
        private GameRoot _root;
        private bool _refreshPending;
        private bool _subscribed;

        private void Start()
        {
            _root = GameRoot.Instance;
            _root.RegisterSceneRoot(this);
        }

        private void OnDestroy()
        {
            if (_bulletField != null) Destroy(_bulletField.gameObject);

            if (_subscribed)
            {
                EventBus<CheckpointActivated>.Unsubscribe(OnCheckpointActivated);
                EventBus<PlayerRespawned>.Unsubscribe(OnPlayerRespawned);
            }

            _enemies?.Dispose();
            _enemies = null;

            if (_root != null)
            {
                _root.UnbindCombatRoot(this);
                _root.UnregisterSceneRoot(this);
            }
        }

        public EnemyMgr Enemies => _enemies;

        public void Attach()
        {
            _root ??= GameRoot.Instance;

            Require.Assigned(playerController, nameof(CombatRoot), nameof(playerController));

            _enemies ??= new EnemyMgr();
            _root.BindCombatRoot(this);

            if (!_subscribed)
            {
                EventBus<CheckpointActivated>.Subscribe(OnCheckpointActivated);
                EventBus<PlayerRespawned>.Subscribe(OnPlayerRespawned);
                _subscribed = true;
            }

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
                : Resources.Load<GameObject>(ResPath.AfterimagePrefab);

            _field = host.AddComponent<AfterimageField>();
            _field.Init(_afterimages, config, playerController.View, playerController.transform, prefab);
        }

        public void FixedTick(float deltaTime)
        {
            if (_refreshPending)
            {
                _refreshPending = false;
                _enemies?.ResetAll();
            }

            if (_afterimages != null)
            {
                _afterimages.Tick(Time.fixedTime);

                if (_field != null) _field.Sync();
            }

            _bullets?.Tick(deltaTime);

            _enemies?.FixedTick(deltaTime);
        }

        public void RenderTick(float deltaTime) => _enemies?.RenderTick(deltaTime);

        private void OnCheckpointActivated(CheckpointActivated evt) => _refreshPending = true;

        private void OnPlayerRespawned(PlayerRespawned evt) => _refreshPending = true;

        private void AttachBullets()
        {
            if (_bullets != null) return;

            GameObject host = new(nameof(BulletField));

            GameObject prefab = playerController.BulletPrefab != null
                ? playerController.BulletPrefab
                : Resources.Load<GameObject>(ResPath.BulletPrefab);

            _bulletField = host.AddComponent<BulletField>();
            _bulletField.Init(prefab, playerController.transform, playerController.HitSink);

            _bullets = new BulletLogic(_bulletField, true);

            playerController.BindBulletLauncher(_bullets);
        }
    }
}
