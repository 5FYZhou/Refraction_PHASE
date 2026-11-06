using Refraction.Logic.Actor;
using Refraction.Logic.Events;
using Refraction.Logic.Level;
using Refraction.Presentation.Actor;
using UnityEngine;

namespace Refraction.Presentation.Level
{
    public class Door : MonoBehaviour
    {
        private const string leftName = "LeftPoint";
        private const string rightName = "RightPoint";
        private const string selfName = "SelfPoint";

        [Tooltip("本门的编号，供上一关的门指向。同一场景内唯一。")]
        public int SelfId;

        [Tooltip("目标关卡 id。0 表示按命名约定去下一关；填当前关卡 id 表示同关内传送到 nextDoorId 那扇门，不切场景。")]
        public int nextLevelId;

        [Tooltip("目标场景里的门编号。0 表示不指定，玩家保持场景里的摆放位置。")]
        public int nextDoorId;

        private float _facingLength = 1f;

        private Transform _leftAnchor;
        private Transform _rightAnchor;
        private Transform _selfAnchor;

        private float _enterBlockedUntil;

        public Vector2 Point => _selfAnchor != null ? _selfAnchor.position : transform.position;

        public Vector2 Facing => FromRotationZ(transform.eulerAngles.z);

        public bool HasLeft => _leftAnchor != null && !_leftAnchor.localPosition.Equals(Vector3.zero);
        public bool HasRight => _rightAnchor != null && !_rightAnchor.localPosition.Equals(Vector3.zero);
        public Vector2 LeftLanding => _leftAnchor != null ? _leftAnchor.position : Point;
        public Vector2 RightLanding => _rightAnchor != null ? _rightAnchor.position : Point;

        public DoorEntry ToEntry() => new(Point, Facing, LeftLanding, HasLeft, RightLanding, HasRight);

        private void Awake()
        {
            _leftAnchor = transform.Find(leftName);
            _rightAnchor = transform.Find(rightName);
            _selfAnchor = transform.Find(selfName);
        }

        /// <summary>接下来 seconds 秒内不响应进入：落点就压在门触发器里，不屏蔽会被自己立刻再送走；只延长不缩短</summary>
        public void SuppressEnterFor(float seconds)
        {
            _enterBlockedUntil = Mathf.Max(_enterBlockedUntil, Time.time + seconds);
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision == null) return;
            if (Time.time < _enterBlockedUntil) return;

            // 玩家的碰撞体可能挂在子物体上，往父级找
            PlayerController player = collision.GetComponentInParent<PlayerController>();
            if (player == null) return;

            EventBus<RequestNextDoor>.Publish(
                new RequestNextDoor(nextLevelId, nextDoorId, player.CurrentMovementTag, player.CurrentFace));
        }

        private void OnDrawGizmos()
        {
            Vector2 origin = transform.position;
            Vector2 dir = Facing;

            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
            Gizmos.DrawWireSphere(origin, 0.15f);
            Gizmos.DrawLine(origin, origin + dir * _facingLength);

            Vector2 tip = origin + dir * _facingLength;
            Vector2 side = new Vector2(-dir.y, dir.x) * 0.1f;
            Gizmos.DrawLine(tip, tip - dir * 0.15f + side);
            Gizmos.DrawLine(tip, tip - dir * 0.15f - side);

            DrawLanding(transform.Find(selfName), origin, Color.red);
            DrawLanding(transform.Find(leftName), origin, Color.green);
            DrawLanding(transform.Find(rightName), origin, Color.blue);
        }

        private static void DrawLanding(Transform point, Vector2 origin, Color color)
        {
            if (point == null) return;

            Gizmos.color = color;
            Gizmos.DrawWireSphere(point.position, 0.12f);
            Gizmos.DrawLine(origin, point.position);
        }

        /// <summary>角度转方向：取主轴并归一</summary>
        private static Vector2 FromRotationZ(float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float x = Mathf.Cos(rad);
            float y = Mathf.Sin(rad);

            return Mathf.Abs(x) >= Mathf.Abs(y)
                ? new Vector2(Mathf.Sign(x), 0f)
                : new Vector2(0f, Mathf.Sign(y));
        }
    }
}
