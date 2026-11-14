using UnityEngine;

namespace Refraction.Presentation.Actor
{
    /// <summary>探测触发器消息的接收方：控制器实现它，转发器只认这个接口</summary>
    public interface IDetectSink
    {
        void OnDetectEnter(Collider2D other);

        void OnDetectExit(Collider2D other);
    }

    /// <summary>给子物体上的触发器挂转发器：找不到只警告，控制器其余逻辑照常</summary>
    public static class DetectZoneBinder
    {
        public static void Bind(Transform root, string childName, string ownerName, IDetectSink sink)
        {
            Transform detect = root.Find(childName);
            if (detect == null)
            {
                Debug.LogWarning($"[{ownerName}] 找不到 {childName} 子物体：这只敌人看不见玩家，只会巡逻。");
                return;
            }

            Collider2D zone = detect.GetComponent<Collider2D>();
            if (zone == null || !zone.isTrigger)
            {
                Debug.LogWarning($"[{ownerName}] {childName} 上没有触发器碰撞体（Is Trigger 未勾选）：这只敌人看不见玩家。");
                return;
            }

            EnemyDetectZone forwarder = detect.GetComponent<EnemyDetectZone>();
            if (forwarder == null) forwarder = detect.gameObject.AddComponent<EnemyDetectZone>();

            forwarder.Init(sink);
        }
    }
}
