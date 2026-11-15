using Refraction.Logic.Actor;
using Refraction.Logic.Combat;
using UnityEngine;

namespace Refraction.Presentation.Actor
{
    /// <summary>敌人视线：DetectCollider 进出决定 Visible，玩家位置每帧现取，所以出视线之后位置仍然有效</summary>
    public sealed class EnemySight : IDetectSink
    {
        private readonly bool _forgetOnExit;

        private PlayerController _player;
        private Transform _playerTransform;
        private Collider2D _body;

        /// <summary>forgetOnExit：出门连玩家引用一起丢（地面敌人的旧判法）；false 只关视线，后摇与追击还要位置</summary>
        public EnemySight(bool forgetOnExit)
        {
            _forgetOnExit = forgetOnExit;
        }

        public bool Visible { get; private set; }

        public bool HasTarget => _player != null;

        public EnemyTargetInfo Sense()
        {
            if (_player == null) return default;

            Vector2 pos = _playerTransform.position;

            if (_body == null) return new EnemyTargetInfo(true, Visible, pos);

            Bounds body = _body.bounds;

            return new EnemyTargetInfo(true, Visible, pos, body.center, body.extents);
        }

        public void OnDetectEnter(Collider2D other)
        {
            if (other == null) return;

            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;

            _player = player;
            _playerTransform = player.transform;
            _body = PlayerBody(player);
            Visible = true;
        }

        public void OnDetectExit(Collider2D other)
        {
            if (_player == null || other == null) return;
            if (!other.transform.IsChildOf(_player.transform)) return;

            Visible = false;

            if (!_forgetOnExit) return;

            _player = null;
            _playerTransform = null;
            _body = null;
        }

        /// <summary>撞人判定要的是受击盒：玩家枢轴在脚下，拿枢轴算距离会把贴身判成没撞上</summary>
        private static Collider2D PlayerBody(PlayerController player)
        {
            HurtBox hurtBox = player.GetComponentInChildren<HurtBox>();

            return hurtBox != null ? hurtBox.GetComponent<Collider2D>() : null;
        }
    }
}
