using System.Collections.Generic;
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
        private const int BulletPoolSize = 32;
        private const int BulletPrewarm = 4;

        public int Order => SceneOrder.World;

        [SerializeField]
        private PlayerController playerController;

        [SerializeField]
        private GameObject afterimagePrefab;

        private readonly List<BulletOwner> _owners = new();
        private readonly Dictionary<GameObject, BulletPool> _pools = new();

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
            DisposePools();

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

        public BulletField Bullets => _bulletField;

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

            _bulletField?.Tick(deltaTime);

            _enemies?.FixedTick(deltaTime);

            SpawnBullets();
        }

        public void RenderTick(float deltaTime) => _enemies?.RenderTick(deltaTime);

        public BulletLauncher CreateLauncher(GameObject prefab, Transform owner, BulletFaction faction,
            IHitLanded hitSink = null)
        {
            if (prefab == null || _bullets == null) return null;

            int id = _owners.Count;

            _owners.Add(new BulletOwner(prefab, owner, hitSink));

            return new BulletLauncher(_bullets, new BulletShooter(id, 0, faction));
        }

        public static BulletLauncher Acquire(GameObject prefab, Transform owner, BulletFaction faction,
            IHitLanded hitSink = null)
        {
            CombatRoot combat = GameRoot.Instance != null ? GameRoot.Instance.Combat : null;

            return combat != null ? combat.CreateLauncher(prefab, owner, faction, hitSink) : null;
        }

        private void OnCheckpointActivated(CheckpointActivated evt) => _refreshPending = true;

        private void OnPlayerRespawned(PlayerRespawned evt) => _refreshPending = true;

        private void AttachBullets()
        {
            if (_bullets != null) return;

            GameObject host = new(nameof(BulletField));

            _bulletField = host.AddComponent<BulletField>();
            _bullets = new BulletLogic();
            _bulletField.Init(_bullets);

            GameObject prefab = playerController.BulletPrefab != null
                ? playerController.BulletPrefab
                : Resources.Load<GameObject>(ResPath.BulletPrefab);

            BulletLauncher launcher = CreateLauncher(prefab, playerController.transform, BulletFaction.Player,
                playerController.HitSink);

            playerController.BindBulletLauncher(launcher);
        }

        private void SpawnBullets()
        {
            if (_bullets == null || _bulletField == null) return;

            while (_bullets.TryTakeRequest(out BulletRequest request))
            {
                BulletOwner owner = OwnerOf(request.Shooter.OwnerId);
                BulletPool pool = owner.Prefab != null ? PoolOf(owner.Prefab) : null;
                Bullet bullet = pool != null ? pool.Take(_bulletField, _bullets, in request, owner.Root, owner.HitSink) : null;

                if (bullet == null)
                {
                    _bullets.Kill(request.Id);
                    continue;
                }

                _bullets.AttachView(request.Id, bullet);
            }
        }

        private BulletOwner OwnerOf(int id) => id >= 0 && id < _owners.Count ? _owners[id] : default;

        private BulletPool PoolOf(GameObject prefab)
        {
            if (_pools.TryGetValue(prefab, out BulletPool pool)) return pool;

            pool = new BulletPool(prefab, _bulletField.transform);
            _pools.Add(prefab, pool);

            return pool;
        }

        private void DisposePools()
        {
            foreach (BulletPool pool in _pools.Values) pool.Dispose();

            _pools.Clear();
            _owners.Clear();
        }

        private readonly struct BulletOwner
        {
            public readonly GameObject Prefab;

            public readonly Transform Root;

            public readonly IHitLanded HitSink;

            public BulletOwner(GameObject prefab, Transform root, IHitLanded hitSink)
            {
                Prefab = prefab;
                Root = root;
                HitSink = hitSink;
            }
        }
    }
}
