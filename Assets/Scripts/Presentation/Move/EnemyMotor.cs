using UnityEngine;

namespace Refraction.Presentation
{
    /// <summary>敌方移动执行器：目前只继承刚体与速度账本，自身的地面/墙面探测还没接，WorldInfo 一律按未踩地、未贴墙给出</summary>
    public sealed class EnemyMotor : MoveMotor
    {
        public override float BodyHeight => 0f;

        public override float FeetOffsetY => 0f;

        public override void SetBodyScale(float scale) { }

        public override bool CanStandUp(float standHeight, float crouchHeight) => true;

        protected override bool SampleGround() => false;

        protected override bool SampleWall(int side) => false;
    }
}
