using Refraction.Logic.Combat;
using UnityEngine;

namespace Refraction.Presentation.Combat
{
    /// <summary>残影的身份牌：子弹撞到残影的 Collider 后靠它拿到残影序号、朝向与残影系统</summary>
    /// <remarks>挂在残影根节点上，随槽位一起复用；槽位回收后索引会过期，所以子弹每次都要回残影系统验活</remarks>
    public sealed class AfterimageAnchor : MonoBehaviour
    {
        public int Index { get; private set; } = -1;

        public int Facing { get; private set; } = 1;

        public AfterimageLogic Logic { get; private set; }

        public Transform PlayerRoot { get; private set; }

        public void Bind(AfterimageLogic logic, int index, int facing, Transform playerRoot)
        {
            Logic = logic;
            Index = index;
            Facing = facing;
            PlayerRoot = playerRoot;
        }
    }
}
