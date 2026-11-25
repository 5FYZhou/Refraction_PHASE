using System.Collections.Generic;
using Refraction.Logic.Combat;
using UnityEngine;

namespace Refraction.Presentation.Combat
{
    /// <summary>全局活跃子弹场：登记在飞的实例、注销失效的，并按固定帧推进整套弹道</summary>
    /// <remarks>不持池也不造实例，那些归 CombatRoot。容器必须是根对象：转身靠翻角色根节点 scale.x，挂在角色下面会把子弹镜像到另一侧</remarks>
    public sealed class BulletField : MonoBehaviour
    {
        private readonly List<Bullet> _live = new();

        private BulletLogic _system;

        public int LiveCount => _live.Count;

        public void Init(BulletLogic system)
        {
            transform.SetParent(null, true);

            _system = system;
        }

        public Bullet LiveAt(int index) => _live[index];

        public void Register(Bullet bullet)
        {
            if (bullet != null && !_live.Contains(bullet)) _live.Add(bullet);
        }

        public void Unregister(Bullet bullet) => _live.Remove(bullet);

        public void Tick(float deltaTime) => _system?.Tick(deltaTime);

        private void OnDestroy() => _live.Clear();
    }
}
