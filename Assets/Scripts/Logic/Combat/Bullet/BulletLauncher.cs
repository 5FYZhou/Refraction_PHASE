namespace Refraction.Logic.Combat
{
    /// <summary>一个发射者的开火口：给发射意图补上"谁在打、算哪边"，再交给全局 BulletLogic</summary>
    public sealed class BulletLauncher : IBulletLauncher
    {
        private readonly BulletLogic _system;

        private BulletShooter _shooter;

        public BulletLauncher(BulletLogic system, in BulletShooter shooter)
        {
            _system = system;
            _shooter = shooter;
        }

        public int OwnerId => _shooter.OwnerId;

        /// <summary>换这一类子弹：同一个发射者只有类目变，池与归属不动</summary>
        public void Rebind(int bulletIndex) =>
            _shooter = new BulletShooter(_shooter.OwnerId, bulletIndex, _shooter.Faction);

        public void Launch(in BulletSpawn spawn) => _system?.Launch(in _shooter, in spawn);

        /// <summary>清掉这个发射者还在飞的子弹：复位与回收时用，不碰别人的</summary>
        public void Clear() => _system?.ClearOwner(_shooter.OwnerId);
    }
}
