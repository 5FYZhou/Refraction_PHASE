namespace Refraction.Logic.Input
{
    /// <summary>可改键的动作；Move 四条落在 InputSys 的 2DVector 分段上，其余各自一条按钮绑定</summary>
    public enum PlayerKeyTag
    {
        MoveUp = 0,
        MoveDown,
        MoveLeft,
        MoveRight,
        Jump,
        Dash,
        Attack,
        Crouch,
        Charge,
        Reverse
    }
}
