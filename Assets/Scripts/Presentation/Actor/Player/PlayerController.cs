using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic;
using Refraction.Logic.Actor;
using Refraction.Logic.Combat;
using Refraction.Logic.Events;
using Refraction.Logic.Input;
using Refraction.Logic.Level;
using Refraction.Presentation.Audio;
using Refraction.Presentation.Combat;
using Refraction.Presentation.Effects;
using Refraction.Presentation.Level;
using Refraction.Presentation.UI;
using UnityEngine;

namespace Refraction.Presentation.Actor
{
    public sealed class PlayerController : MonoBehaviour, ISceneRoot, IPhysicsTicked, IRenderTicked, IHurtView
    {
        private const float BlinkHz = 5f;
        private const float BlinkMinAlpha = 0.3f;
        private const float DashInvulnerableAlpha = 100f / 255f;
        private const int BulletLayer = 11;
        private const float DustMinSpeed = 0.5f;

        public int Order => SceneOrder.Player;

        [SerializeField]
        private float _spawnY = -10f;

        [Tooltip("玩家参数资产。装配时同时注入 PlayerMotor，移动/重力/生命读的是这一份。")]
        [SerializeField]
        private PlayerConfig config;

        [Tooltip("驱动刚体的执行器。逻辑层的移动/跳跃/冲刺指令最终都落到这里。")]
        [SerializeField]
        private MoveMotor moveMotor;


        [Tooltip("存档点锚点：场景里的 Spawn 对象。该存档点勾了自动存档就进范围即存，否则按 W 存。留空表示本关没有存档点。")]
        [SerializeField]
        private Transform spawnT;

        [Tooltip("站立时的子弹发射点：相对玩家根节点的局部偏移，x 取正值、运行时按朝向镜像（x 分量会取绝对值再乘朝向）。")]
        [SerializeField]
        private Vector2 muzzleStand = new(0.6f, 0.2f);

        [Tooltip("下蹲时的子弹发射点：同一套局部坐标。下蹲前摇期间它与 muzzleStand 之间按蹲姿进度线性过渡。")]
        [SerializeField]
        private Vector2 muzzleCrouch = new(0.6f, -0.15f);

        [Tooltip("白攻击的子弹预制体：留空则退回 Resources/Prefabs/Bullet/Bullet。弹反反打复用同一条池。")]
        [SerializeField]
        private GameObject bulletPrefab;

        [Tooltip("黑攻击命中的火花颜色：直接覆盖粒子自带颜色。")]
        [SerializeField]
        private Color attackSparkColor = Color.white;

        [Tooltip("弹反成功的火花颜色：直接覆盖粒子自带颜色。")]
        [SerializeField]
        private Color reboundSparkColor = Color.white;

        [Tooltip("移动扬尘的颜色：直接覆盖粒子自带颜色。")]
        [SerializeField]
        private Color moveDustColor = Color.white;

        [Tooltip("落地烟的颜色：直接覆盖粒子自带颜色。")]
        [SerializeField]
        private Color landColor = Color.white;

        [Tooltip("存档成功的火花颜色：直接覆盖粒子自带颜色。")]
        [SerializeField]
        private Color savePointSparkColor = Color.white;

        [Tooltip("死亡粒子的颜色：直接覆盖粒子自带颜色。")]
        [SerializeField]
        private Color playerDieColor = Color.white;

        [Tooltip("世界反转粒子的颜色：世界黑白翻转时在玩家中点播一次，颜色跟着取反。")]
        [SerializeField]
        private Color worldReverseColor = Color.white;

        private Color _moveDustColor = Color.white;
        private Color _landColor = Color.white;
        private Color _savePointSparkColor = Color.white;
        private Color _playerDieColor = Color.white;
        private Color _worldReverseColor = Color.white;

        private LogicContext _context;

        private GameRoot _root;
        private bool _attached = false;
        private bool _warnedNoGrounded;
        private InputProvider _inputProvider;
        private InputBuffer _buffer;
        private PlayerLogic _logic;
        private IAfterimageSpawner _afterimageSpawner;
        private Spawn _checkpoint;
        private bool _insideCheckpoint;
        private BlackAttackView _blackAttack;
        private Collider2D _hurt;
        private int _hurtExclude;
        private bool _phasing;
        private bool _grounded = true;
        private bool _dead;
        private float _deathElapsed;
        private Collider2D[] _bodyColliders;
        private bool[] _bodyColliderStates;

        private bool _shownBlack;
        private float _saveLockUntil;
        private PlayerAniView _aniView;

        // View 与脚底参考点：都按名字找（Player/View、View/Bottom），不在 Inspector 里连线
        private Transform view;
        private Transform top;
        private Transform bottom;

        private bool _warnedNoView;

        private SpriteRenderer _viewRenderer;
        private Color _viewBaseColor = Color.white;
        private float _blinkElapsed;
        private ChargeBarView _chargeBar;

        private void Awake()
        {
            // 必须放 Awake：GameRoot 可能在 Start 前就调 Attach/Tick，那帧 view 还没解析，会先报一次“找不到 View”
            CacheView();

            _chargeBar = new(transform);
        }
        private void Start()
        {
            _root = GameRoot.Instance;
            _root.RegisterSceneRoot(this);
        }
        private void OnDestroy()
        {
            // 判定盒开打时被摘到场景根上，不先装回来会跟着玩家一起活着（残留在原地、引用已死的逻辑）
            _blackAttack?.End();

            _logic?.Dispose();

            // 用 Start 里抓住的引用：销毁期再问 GameRoot.Instance 可能当场造一个新的出来
            if (_root != null)
            {
                _root.Audio?.SetLoopSfx(AudioId.None, false);
                _root.UnregisterSceneRoot(this);
            }
        }
        private void OnEnable()
        {
            EventBus<RequestPlayerPosChange>.Subscribe(OnRequestPlayerPosChange);
        }
        private void OnDisable()
        {
            EventBus<RequestPlayerPosChange>.Unsubscribe(OnRequestPlayerPosChange);
        }

        /// <summary>场景根装配：接上输入源，再按上一场景那扇门指定的落点摆好；GameRoot 在注册后的下一个驱动帧调用，必须幂等</summary>
        public void Attach()
        {
            if (_attached) return;

            _root ??= GameRoot.Instance;
            if (_root == null || _root.Input == null)
            {
                Debug.LogError("[PlayerController] GameRoot 还没装配好输入源，无法接入。");
                return;
            }

            Require.Assigned(moveMotor, nameof(PlayerController), nameof(moveMotor));
            Require.Assigned(config, nameof(PlayerController), nameof(config));

            moveMotor.BindConfig(config);

            _inputProvider = _root.Input;
            _buffer = new(
                Mathf.Max(config.inputBufferTime, config.dashBufferTime),
                Mathf.RoundToInt(1f / Time.fixedDeltaTime)
                );

            _blackAttack ??= new BlackAttackView(transform, config, () => moveMotor.FaceSign, () => _logic?.OnMeleeHitLanded(), _root.Effects, OnReboundReflected);
            _blackAttack?.SetSparkColors(attackSparkColor, reboundSparkColor);

            // 发射口与开火点在 CombatRoot 装配时绑进来（World 序必然晚于 Player），构造期只先给空
            _logic = new(moveMotor, config, _buffer, null, this, _blackAttack);

            // 跨关血量与极性都要在这里恢复：GameRoot 紧接着会 Announce，晚一步界面就先闪一次默认值
            if (_root.TryGetPlayerHealth(out int health)) _logic.SetHealth(health);
            if (_root.TryGetPlayerPolarity(out int polarity)) _logic.SetPolarity(polarity);

            // 世界极性进关先同步一次：新玩家不听历史事件，不同步就会按白形态出手
            _logic.SetWorld(_root.World);

            if (_afterimageSpawner != null) _logic.BindAfterimageSpawner(_afterimageSpawner);

            var hurtBox = transform.GetComponentInChildren<HurtBox>();
            if (hurtBox != null) hurtBox.SetIAttackable(_logic);

            _hurt = hurtBox != null ? hurtBox.GetComponent<Collider2D>() : null;

            if (_hurt != null) _hurtExclude = _hurt.excludeLayers;

            _blackAttack.BindRebound(_logic, _hurt);

            CacheColliders();

            _checkpoint = spawnT != null ? spawnT.GetComponent<Spawn>() : null;
            if (spawnT != null && _checkpoint == null)
            {
                Debug.LogWarning($"[PlayerController] 存档点 {spawnT.name} 上没挂 Spawn 脚本，按 W 不会存档。");
            }

            _attached = true;

            ApplyInvert(_logic.IsBlack);

            // 跨关回档的落点优先于门；首关两者都没有，保持场景里摆的位置
            if (_root.TryConsumeRespawnPoint(out Vector2 respawn))
            {
                _logic.SpawnTo(respawn);
            }
            else if (_root.TryConsumeEntryPoint(out DoorEntry entry, out MovementStateTag enterTag, out int enterFace))
            {
                SpawnAtEntry(in entry, enterTag, enterFace);
            }
        }

        public MovementStateTag CurrentMovementTag =>
            _attached && _logic != null ? _logic.CurrentTag : MovementStateTag.None;

        public bool IsTransiting => _attached && _logic != null && _logic.IsTransiting;

        public bool MapOpen => _attached && _logic != null && _logic.MapOpen;

        /// -1 朝左，1 朝右；跨场景后新玩家还没有朝向
        public int CurrentFace => _attached && moveMotor != null ? moveMotor.FaceSign : 1;

        public bool IsBlack => _attached && _logic != null && _logic.IsBlack;

        public void Announce() => _logic?.Announce();

        public void AnnouncePolarity() => _logic?.AnnouncePolarity();

        public void BindAfterimageSpawner(IAfterimageSpawner spawner)
        {
            _afterimageSpawner = spawner;
            _logic?.BindAfterimageSpawner(spawner);
        }

        public void BindBulletLauncher(IBulletLauncher launcher)
        {
            _logic?.BindBulletLauncher(launcher);
            _logic?.BindMuzzle(muzzleStand, muzzleCrouch);
        }

        private static Vector2 Mirror(Vector2 offset, int face) =>
            new(Mathf.Abs(offset.x) * (face < 0 ? -1f : 1f), offset.y);

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            float sign = transform.lossyScale.x < 0f ? -1f : 1f;
            Vector3 stand = transform.position + (Vector3)Mirror(muzzleStand, (int)sign);
            Vector3 crouch = transform.position + (Vector3)Mirror(muzzleCrouch, (int)sign);

            Gizmos.color = new Color(0.35f, 1f, 0.45f, 0.9f);
            Gizmos.DrawWireSphere(stand, 0.12f);

            Gizmos.color = new Color(1f, 0.55f, 0.25f, 0.9f);
            Gizmos.DrawWireSphere(crouch, 0.12f);

            Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
            Gizmos.DrawLine(stand, crouch);
        }
#endif

        public Transform View => view;

        public PlayerConfig Config => config;

        public GameObject BulletPrefab => bulletPrefab;

        public IHitLanded HitSink => _logic;

        public bool IsDead => _dead;

        public void FixedTick(float deltaTime)
        {
            if (!_attached) return;

            // 死亡演出期间不推进逻辑、不读输入
            if (_dead) return;

            InputSnapshot snapshot = _inputProvider.ConsumeSnapshot();
            if (Time.fixedTime < _saveLockUntil) snapshot = default;
            // 世界信息由 PlayerMotor 的物理探测给出，本层只负责组装 Context
            WorldInfo worldInfo = moveMotor.SampleWorld();
            GameTime gameTime = new(Time.fixedTime, Time.fixedDeltaTime);
            _context = new(snapshot, worldInfo, gameTime);

            MovementStateTag prevMove = _logic.CurrentTag;
            AttackStateTag prevAttack = _logic.AttackTag;
            ChargePhase prevCharge = _logic.ChargeStage;
            bool prevPhasing = _logic.IsPhasing;

            _logic.FixedTick(_context);
            PlayActionAudio(prevMove, prevAttack, prevCharge, prevPhasing);
            ApplyPhase();

            // 轮询而不是听事件：显示必须跟逻辑同一帧的形态，事件到达顺序由订阅先后决定
            if (_logic.IsBlack != _shownBlack)
            {
                ApplyInvert(_logic.IsBlack);
                _root?.Effects?.PlayWorldReverse(ViewCenter(), _worldReverseColor);
            }

            bool grounded = _context.WorldInfo.Grounded;

            if (grounded && !_grounded) PlayLand();

            _grounded = grounded;

            _blackAttack?.ReboundTick(_root != null && _root.Combat != null ? _root.Combat.Bullets : null);

            EventBus<PlayerDebugSnapshotTaken>.Publish(new(
                _context.WorldInfo.FrameStartVelocity,
                moveMotor.LedgerVelocity,
                _logic.HurtVelocity,
                _context.WorldInfo.Grounded,
                _logic.ConditionTag,
                _logic.CurrentTag,
                _logic.PostureTag,
                _logic.AttackTag,
                _logic.Polarity,
                _logic.MaxPolarity));

            // 掉血到 0：进入死亡演出，等 CombatRoot 走完再复活
            if (!_logic.IsAlive)
            {
                BeginDeath();
                return;
            }

            // 掉出世界：扣坠落伤害并回上次落脚点，扣到没血就走死亡分支
            if (_context.WorldInfo.Pos.y < _spawnY)
            {
                if (!_logic.HasGroundedPos)
                {
                    WarnNoGroundedPoint();
                }
                else
                {
                    _logic.TakeFallDamage();

                    if (!_logic.IsAlive) BeginDeath();
                    else _logic.FallBackToLastGrounded();
                }
            }

            bool insideCheckpoint = _checkpoint != null && _checkpoint.HasPlayer;
            bool enteredCheckpoint = insideCheckpoint && !_insideCheckpoint;
            _insideCheckpoint = insideCheckpoint;

            if (insideCheckpoint && (_context.InputSnapshot.SavePressed || (_checkpoint.AutoSave && enteredCheckpoint)))
            {
                TrySaveCheckpoint();
            }
        }

        private void PlayLand()
        {
            _root.Audio?.EnqueueSfx(AudioId.Land);

            if (bottom == null) return;

            _root.Effects?.PlayLand(bottom.position, _landColor);
        }

        private void PlayActionAudio(MovementStateTag prevMove, AttackStateTag prevAttack, ChargePhase prevCharge, bool prevPhasing)
        {
            AudioManager audio = _root != null ? _root.Audio : null;

            if (audio == null) return;

            if (_logic.ChargeStage == ChargePhase.Charging && prevCharge != ChargePhase.Charging) audio.EnqueueSfx(AudioId.Charge);

            bool phasing = _logic.IsPhasing && !prevPhasing;

            // 蓄满后的这次冲刺会吃掉一枚残影，只有它算「残影冲刺」
            if (phasing) audio.EnqueueSfx(AudioId.AfterimageDash);

            MovementStateTag move = _logic.CurrentTag;

            if (move != prevMove)
            {
                if (move == MovementStateTag.Dash && !phasing) audio.EnqueueSfx(AudioId.Dash);
                else if (move == MovementStateTag.Jump || move == MovementStateTag.AirJump) audio.EnqueueSfx(AudioId.Jump);
            }

            AttackStateTag attack = _logic.AttackTag;

            if (attack == prevAttack || attack == AttackStateTag.None) return;

            bool beginsSwing = prevAttack == AttackStateTag.None || prevAttack == AttackStateTag.WhiteAttackRecover;

            if (beginsSwing) audio.EnqueueSfx(IsBlackDash(attack) ? AudioId.Dash : AudioId.SwordSwing);
        }

        private static bool IsBlackDash(AttackStateTag tag)
            => tag == AttackStateTag.BlackDashUp || tag == AttackStateTag.BlackDashDown
               || tag == AttackStateTag.BlackDashLevel;

        public void OnHurt(int damage)
        {
            if(_logic.CurHealth > 0)
                _root?.Audio?.EnqueueSfx(AudioId.Hurt);
            _root?.Effects?.Play(EffectType.DamageFlash, view, config.flashTime);
        }

        private void OnReboundReflected(Vector2 dir)
        {
            _root?.Audio?.EnqueueSfx(AudioId.WeaponClash);
            _logic?.OnReboundReflected(dir);
        }

        private void ApplyInvert(bool black)
        {
            _shownBlack = black;
            ApplyParticleColors(black);

            if (_root == null || view == null) return;

            _root.Effects?.SetInvert(view, black);
        }

        private void ApplyParticleColors(bool invert)
        {
            _moveDustColor = Tint(moveDustColor, invert);
            _landColor = Tint(landColor, invert);
            _savePointSparkColor = Tint(savePointSparkColor, invert);
            _playerDieColor = Tint(playerDieColor, invert);
            _worldReverseColor = Tint(worldReverseColor, invert);
        }

        private static Color Tint(Color color, bool invert) =>
            invert ? new Color(1f - color.r, 1f - color.g, 1f - color.b, color.a) : color;

        private void BeginDeath()
        {
            _dead = true;
            _deathElapsed = 0f;

            _root.Audio?.EnqueueSfx(AudioId.Death);
            _root.Audio?.SetLoopSfx(AudioId.Move, false);

            // 死亡期间逻辑不推进，先把这一帧的速度清零，否则尸体会按最后的惯性滑出去
            moveMotor.SetMotorEnabled(false);

            _logic.CancelCharge();
            _chargeBar?.Tick(ChargePhase.None, 0f, 0f);

            _blackAttack?.End();
            _blackAttack?.HideRebound();

            _root.Effects?.SetMoveDust(bottom, false, _moveDustColor);
            _root.Effects?.SetChargeDash(ViewCenter(), Vector2.zero, false, 0f);
            _root.Effects?.PlayPlayerDie(ViewCenter(), _playerDieColor);

            SetCollidersEnabled(false);
        }

        private void TickDeathVisual(float deltaTime)
        {
            if (_deathElapsed < config.deathFadeTime) _deathElapsed += deltaTime;

            float t = config.deathFadeTime > 0f ? Mathf.Clamp01(_deathElapsed / config.deathFadeTime) : 1f;
            SetViewAlpha(_viewBaseColor.a * (1f - t));
        }

        public void ReviveFromDeath()
        {
            if (!_dead) return;

            moveMotor.SetMotorEnabled(true);
            _logic.Revive();

            _dead = false;
            _deathElapsed = 0f;
            _blinkElapsed = 0f;

            SetCollidersEnabled(true);
            SetViewAlpha(_viewBaseColor.a);
        }

        public void RespawnAt(Vector2 pivot) => _logic.SpawnTo(pivot);

        public bool RespawnToLastGrounded() => _logic.RespawnToLastGrounded();

        private void SetViewAlpha(float alpha)
        {
            if (_viewRenderer == null) return;

            Color color = _viewRenderer.color;
            if (Mathf.Approximately(color.a, alpha)) return;

            color.a = alpha;
            _viewRenderer.color = color;
        }

        private void SetCollidersEnabled(bool on)
        {
            if (_bodyColliders == null) return;

            for (int i = 0; i < _bodyColliders.Length; i++)
            {
                if (_bodyColliders[i] == null) continue;

                _bodyColliders[i].enabled = on && _bodyColliderStates[i];
            }
        }

        private void CacheColliders()
        {
            _bodyColliders = GetComponentsInChildren<Collider2D>(true);
            _bodyColliderStates = new bool[_bodyColliders.Length];

            for (int i = 0; i < _bodyColliders.Length; i++) _bodyColliderStates[i] = _bodyColliders[i].enabled;
        }

        private void TrySaveCheckpoint()
        {
            if (_checkpoint == null || !_checkpoint.HasPlayer) return;

            Vector2 pivot = _checkpoint.Point;
            pivot.y -= moveMotor.FeetOffsetY;

            if (!_root.SaveCheckpoint(pivot)) return;

            _root.Audio?.EnqueueSfx(AudioId.SaveBell);
            _logic.ResetHealth();
            _logic.Announce();

            Vector2 pos = ViewCenter();
            _root.Effects?.PlaySavePointSpark(pos, _savePointSparkColor);
            _root.Effects?.Play(EffectType.DamageFlash, view, config.saveFlashTime);

            _saveLockUntil = Time.fixedTime + config.saveInputLockTime;

            EventBus<CheckpointActivated>.Publish(default);
        }

        private Vector2 ViewCenter()
        {
            if (top == null || bottom == null) return view != null ? (Vector2)view.position : (Vector2)transform.position;

            return ((Vector2)top.position + (Vector2)bottom.position) * 0.5f;
        }

        public void RenderTick(float deltaTime)
        {
            if (!_attached) return;

            if (_dead)
            {
                TickDeathVisual(deltaTime);
                return;
            }

            _logic.Tick(new());

            bool dust = _grounded && Mathf.Abs(_context.WorldInfo.FrameStartVelocity.x) >= DustMinSpeed;
            _root.Effects?.SetMoveDust(bottom, dust, _moveDustColor);

            Vector2 dashDir = moveMotor.LedgerVelocity;
            if (dashDir.sqrMagnitude < 1e-4f) dashDir = new(moveMotor.FaceSign, 0f);

            _root.Effects?.SetChargeDash(ViewCenter(), dashDir, _logic.IsPhasing, config.dashInvulnerableTime);
            _root.Audio?.SetLoopSfx(AudioId.Move, dust);

            _chargeBar?.Tick(_logic.ChargeStage, _logic.ChargeProgress, deltaTime);
            ApplyBlinkVisual(deltaTime);

            if (_aniView != null)
            {
                _aniView.PlayBlackAttack(_logic.AttackTag);
                _aniView.PlayLocomotion(_logic.CurrentTag, _logic.PostureTag, _logic.MoveX,
                    _logic.IsHurt, _context.WorldInfo.FrameStartVelocity.y);
            }
        }

        private void ApplyBlinkVisual(float deltaTime)
        {
            float alpha = _viewBaseColor.a;

            if (_logic.IsDashInvulnerable)
            {
                _blinkElapsed = 0f;
                alpha = DashInvulnerableAlpha;
            }
            else if (_logic.IsInvulnerable)
            {
                _blinkElapsed += deltaTime;
                if (Mathf.Repeat(_blinkElapsed * BlinkHz, 1f) < 0.5f) alpha = BlinkMinAlpha * _viewBaseColor.a;
            }
            else
            {
                _blinkElapsed = 0f;
            }

            if (_viewRenderer == null) return;

            SetViewAlpha(alpha);
        }

        private void ApplyPhase()
        {
            if (_hurt == null) return;

            bool phase = _logic.IsPhasing;

            if (phase == _phasing) return;

            _phasing = phase;
            _hurt.excludeLayers = phase ? _hurtExclude | (1 << BulletLayer) : _hurtExclude;
        }

        private void WarnNoGroundedPoint()
        {
            if (_warnedNoGrounded) return;

            _warnedNoGrounded = true;
            Debug.LogWarning("[PlayerController] 掉出世界，但还没有任何落脚点记录（出生后一次都没踩过地），无处可回。");
        }

        public void OnRequestPlayerPosChange(RequestPlayerPosChange evt)
        {
            // 还没 Attach 就被点名传送（切场景时事件可能早到），忽略而不是空引用
            if (!_attached) return;

            SpawnAtEntry(in evt.Entry, evt.EnterTag, evt.EnterFace);
        }

        private void SpawnAtEntry(in DoorEntry entry, MovementStateTag enterTag, int enterFace)
        {
            Vector2 pivot = new(entry.Point.x, entry.Point.y - moveMotor.FeetOffsetY);

            _logic.SpawnTo(pivot, false);
            _logic.BeginTransit(in entry, enterTag, enterFace);
        }


        #region View
        /// <summary>层级里约定好的两个名字：Player/View 与 View/Bottom（脚底空物体）；PlayerMotor 找的也是这两个名字，两边必须一致</summary>
        private const string ViewChildName = "View";
        private const string TopChildName = "Top";
        private const string BottomChildName = "Bottom";

        /// <summary>按名字找 View 与脚底参考点：闪白要拿 View 上的 SpriteRenderer，扬尘与落地烟以脚底为发射位置</summary>
        /// <remarks>Bottom 是 View 的直接子节点，必须 view.Find 而不是 transform.Find——后者只扫 Player 的一级子物体</remarks>
        private void CacheView()
        {
            _aniView = GetComponentInChildren<PlayerAniView>(true);

            view = transform.Find(ViewChildName);
            if (view == null)
            {
                WarnNoView();
                return;
            }

            bottom = view.Find(BottomChildName);
            top = view.Find(TopChildName);

            _viewRenderer = view.GetComponent<SpriteRenderer>();
            if (_viewRenderer != null) _viewBaseColor = _viewRenderer.color;
        }

        private void WarnNoView()
        {
            if (_warnedNoView) return;

            _warnedNoView = true;
            Debug.LogWarning($"[PlayerController] 层级里找不到 {ViewChildName} 子物体：受击闪白、扬尘与落地烟都不会生效。");
        }
        #endregion

    }
}
