using Refraction.Logic.Events;
using Refraction.Presentation.Actor;
using UnityEngine;

namespace Refraction.Presentation.Level
{
    public class Door : MonoBehaviour
    {
        [Tooltip("本门的编号，供上一关的门指向。同一场景内唯一。")]
        public int SelfId;

        [Tooltip("目标关卡 id。0 表示按命名约定去下一关；填当前关卡 id 表示同关内传送到 nextDoorId 那扇门，不切场景。")]
        public int nextLevelId;

        [Tooltip("目标场景里的门编号。0 表示不指定，玩家保持场景里的摆放位置。")]
        public int nextDoorId;

        [Tooltip("玩家从这扇门进来时的落点。留空就用门自身的位置。")]
        [SerializeField]
        private Transform spawnAnchor;

        private float _enterBlockedUntil;

        public Vector2 SpawnPoint => spawnAnchor != null ? spawnAnchor.position : transform.position;

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
            if (collision.GetComponentInParent<PlayerController>() == null) return;

            EventBus<RequestNextDoor>.Publish(new RequestNextDoor(nextLevelId, nextDoorId));
        }
    }
}
