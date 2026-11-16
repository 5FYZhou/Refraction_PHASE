namespace Refraction.Logic.Combat
{
    /// <summary>弹反范围的表现端：逻辑每帧给方向与左右，表现层只负责点灯与镜像</summary>
    /// <remarks>side 用 -1 / 1 而不是 bool，表现层直接拿它和当前朝向比；同向重复调用必须幂等，逻辑层每帧都会调</remarks>
    public interface IReboundView
    {
        void ShowRebound(BlackAttackKind kind, int side);

        void HideRebound();
    }
}
