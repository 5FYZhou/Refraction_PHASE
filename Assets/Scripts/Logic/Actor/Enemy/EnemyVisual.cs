namespace Refraction.Logic.Actor
{
    /// <summary>表现层换色用的展示态：巡逻 / 追击 / 返回 + 攻击三段；没有对应态的敌人不会产出它</summary>
    public enum EnemyVisual
    {
        Patrol,
        Chase,
        Return,
        Windup,
        Strike,
        Recover,
    }
}
