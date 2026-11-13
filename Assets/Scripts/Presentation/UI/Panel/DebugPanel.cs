using System.Collections.Generic;
using System.Text;
using Refraction.Logic.Events;
using Refraction.Logic.Level;
using Refraction.Presentation.Actor;
using TMPro;
using UnityEngine;

namespace Refraction.Presentation.UI
{
    /// <summary>场景级调试面板：画布与实例都由场景持有，随场景销毁；加载时向 UIMgr 自注册</summary>
    /// <remarks>只订阅事件、不持有玩家对象，因此删掉本面板不会影响任何已有逻辑；文本按 refreshInterval 抽样重建，避免逐帧产生垃圾</remarks>
    public sealed class DebugPanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.Scene;

        public override bool CanBeHideByKey => false;

        public override bool LiveWithScene => true;

        /// <summary>子物体名，必须与预制体里那个 TMP 文本一致，改名后面板只是不刷新</summary>
        private const string TextNodeName = "TxtInfo";

        private const string EnemyTextNodeName = "TxtEnemyInfo";

        [Tooltip("文本刷新间隔（秒）。逐帧重建字符串会持续产生垃圾，按这个间隔抽样一次")]
        [Min(0f)]
        [SerializeField]
        private float refreshInterval = 0.05f;

        private readonly StringBuilder _builder = new(256);

        private readonly StringBuilder _enemyBuilder = new(256);

        private TMP_Text _text;
        private TMP_Text _enemyText;
        private bool _black = true;
        private float _nextRefresh;
        private int _curHealth;
        private int _maxHealth;

        protected override void Awake()
        {
            base.Awake();
            _text = GetComponent<TMP_Text>(TextNodeName);
            _enemyText = GetComponent<TMP_Text>(EnemyTextNodeName);
            ApplyTextColor();
        }

        /// <summary>保留预制体里写的占位文本：第一帧快照到达前也能看出面板活着</summary>
        protected override bool KeepAuthoredTxt(string name) => true;

        protected override void RegisterSelf()
        {
            base.RegisterSelf();
            root.UIMgr.RegisterPanel(this);
        }

        protected override void UnRegisterSelf()
        {
            // 退出播放时 GameRoot 可能先于本面板销毁
            if (root == null) return;

            root.UIMgr.UnregisterPanel(this);
        }

        private void OnEnable()
        {
            EventBus<PlayerDebugSnapshot>.Subscribe(OnSnapshot);
            EventBus<PlayerHealthChanged>.Subscribe(OnHealthChanged);
            EventBus<WorldPolarityChanged>.Subscribe(OnWorldPolarityChanged);
        }

        private void OnDisable()
        {
            EventBus<PlayerDebugSnapshot>.Unsubscribe(OnSnapshot);
            EventBus<PlayerHealthChanged>.Unsubscribe(OnHealthChanged);
            EventBus<WorldPolarityChanged>.Unsubscribe(OnWorldPolarityChanged);
        }

        private void OnWorldPolarityChanged(WorldPolarityChanged evt)
        {
            _black = evt.World == WorldPolarity.Black;
            ApplyTextColor();
        }

        private void ApplyTextColor()
        {
            Color color = _black ? Color.black : Color.white;

            if (_text != null) _text.color = color;
            if (_enemyText != null) _enemyText.color = color;
        }

        private void OnHealthChanged(PlayerHealthChanged evt)
        {
            _curHealth = evt.cur;
            _maxHealth = evt.max;
        }

        private void OnSnapshot(PlayerDebugSnapshot snapshot)
        {
            if (_text == null) return;

            float now = Time.unscaledTime;
            if (now < _nextRefresh) return;

            _nextRefresh = now + refreshInterval;

            _builder.Clear();
            AppendVelocity("帧首速度", snapshot.FrameStartVelocity);
            AppendVelocity("账本速度", snapshot.LedgerVelocity);
            AppendVelocity("受击速度", snapshot.HurtVelocity);

            _builder.Append("触地 ").Append(snapshot.Grounded ? "是" : "否")
                    .Append("\n状态 ").Append(snapshot.Condition.ToString())
                    .Append("\n移动 ").Append(snapshot.Move.ToString())
                    .Append("\n姿态 ").Append(snapshot.Posture.ToString())
                    .Append("\n攻击 ").Append(snapshot.Attack.ToString())
                    .Append("\n血量：").Append(_curHealth).Append('/').Append(_maxHealth)
                    .Append("\n极性：").Append(snapshot.Polarity).Append('/').Append(snapshot.MaxPolarity);

            _text.SetText(_builder);

            AppendEnemies();
        }

        private void AppendEnemies()
        {
            if (_enemyText == null || root == null) return;

            _enemyBuilder.Clear();
            _enemyBuilder.Append("敌人");

            bool any = false;
            IReadOnlyList<ISceneRoot> roots = root.SceneRoots;

            for (int i = 0; i < roots.Count; i++)
            {
                EnemyController enemy = roots[i] as EnemyController;
                if (enemy == null || !enemy.AIEnabled) continue;

                any = true;

                _enemyBuilder.Append("\n").Append(enemy.name)
                             .Append("目标\n").Append(enemy.HasTarget ? "有" : "无")
                             .Append("").Append(enemy.CurrentTag.ToString());
            }

            if (!any) _enemyBuilder.Append(" 无");

            _enemyText.SetText(_enemyBuilder);
        }

        /// <summary>固定两位小数：速度是连续量，整数位跳动看不出一帧内的变化</summary>
        private void AppendVelocity(string label, Vector2 velocity)
        {
            _builder.Append(label).Append(" (")
                    .Append(velocity.x.ToString("F2")).Append(", ")
                    .Append(velocity.y.ToString("F2")).Append(")\n");
        }
    }
}
