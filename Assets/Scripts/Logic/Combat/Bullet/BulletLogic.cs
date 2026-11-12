using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Logic.Combat
{
    /// <summary>子弹系统：开火唯一出口是 Launch，之后每发的状态、路径与出射方向全在这里推</summary>
    /// <remarks>只管逻辑：视图经 IBulletViewFactory 造，姿势经 IBulletView 下发，撞击与否由表现层把触发器回调报回来</remarks>
    public sealed class BulletLogic : IBulletLauncher
    {
        private const float ArriveEps = 1e-4f;
        private const float DirEps = 1e-6f;

        private sealed class Shot
        {
            public IBulletView View;
            public Damage Damage;
            public AfterimageLogic Logic;
            public Vector2 Pos;
            public Vector2 Dir;
            public Vector2 EnterDir = Vector2.right;
            public float Speed;
            public float TransferSpeed;
            public BulletState State;
            public int Id;
            public int Step;
            public int Node;
            public int SegFrom = -1;
            public int SegTo = -1;
            public bool Transported;
            public bool Dead;
            public readonly List<TransferNode> Plan = new();
        }

        private readonly List<Shot> _live = new();
        private readonly IBulletViewFactory _factory;

        private int _nextId;

        public BulletLogic(IBulletViewFactory factory)
        {
            _factory = factory;
        }

        public int Count => _live.Count;

        /// <summary>玩家逻辑层开火的唯一入口：先记账再要视图，视图在 Bind 时会回读伤害</summary>
        public void Launch(in BulletSpawn spawn)
        {
            if (_factory == null) return;

            Shot shot = new()
            {
                Id = _nextId++,
                Pos = spawn.Origin,
                Dir = spawn.Direction,
                Speed = spawn.Speed,
                TransferSpeed = spawn.TransferSpeed,
                State = BulletState.Flying,
                Damage = new Damage(spawn.Damage, DamageSource.Bullet, spawn.Direction, spawn.KnockbackSpeed)
            };

            _live.Add(shot);

            shot.View = _factory.Create(this, shot.Id);
            if (shot.View == null)
            {
                _live.Remove(shot);
                return;
            }

            shot.View.Apply(Pose(shot));
        }

        /// <summary>每物理帧推一次：飞行中的只跟读位置，传输中的逐段走，走完或目标失效就地出射</summary>
        public void Tick(float deltaTime)
        {
            for (int i = 0; i < _live.Count; i++)
            {
                Shot shot = _live[i];
                if (shot.Dead) continue;

                if (shot.State == BulletState.Flying) shot.Pos = shot.View.Position;
                else Advance(shot, deltaTime);

                if (!shot.Dead) shot.View.Apply(Pose(shot));
            }
        }

        /// <summary>子弹撞进残影：当场规划整条路径并锁住，只有一格路径时返回 false（不能自成一跳）</summary>
        public bool TryEnterTransport(int id, AfterimageLogic logic, int entryIndex)
        {
            Shot shot = Find(id);

            if (shot == null || shot.Dead) return false;
            if (shot.State != BulletState.Flying) return false;
            if (shot.Transported) return false;
            if (logic == null) return false;
            if (logic.BuildTransfer(entryIndex, shot.Plan) < 2) return false;

            shot.Logic = logic;
            shot.State = BulletState.Transporting;
            shot.Node = 0;
            shot.Step = 1;

            if (shot.Dir.sqrMagnitude > DirEps) shot.EnterDir = shot.Dir;

            shot.Pos = shot.Plan[0].Pos;

            BeginSegment(shot, shot.Plan[0], shot.Plan[1]);

            shot.View.Apply(Pose(shot));

            return true;
        }

        public Damage DamageOf(int id)
        {
            Shot shot = Find(id);

            return shot != null ? shot.Damage : default;
        }

        /// <summary>收一发：撞到 bound、地面，或命中敌人结算完时调这里，本层摘账再让视图回池</summary>
        public void Kill(int id)
        {
            Shot shot = Find(id);
            if (shot == null) return;

            shot.Dead = true;
            _live.Remove(shot);

            shot.View?.Kill();
        }

        /// <summary>视图被外部销毁（换场景、编辑器删对象）时清账，不能在账上留一发没有身体的子弹</summary>
        public void Forget(int id)
        {
            Shot shot = Find(id);
            if (shot == null) return;

            shot.Dead = true;
            _live.Remove(shot);
        }

        private void Advance(Shot shot, float deltaTime)
        {
            if (shot.Step >= shot.Plan.Count)
            {
                Leave(shot);
                return;
            }

            TransferNode target = shot.Plan[shot.Step];

            if (!shot.Logic.IsAlive(target.Index))
            {
                Leave(shot);
                return;
            }

            float stride = shot.TransferSpeed > 0f ? shot.TransferSpeed * deltaTime : float.PositiveInfinity;
            Vector2 next = Vector2.MoveTowards(shot.Pos, target.Pos, stride);

            Vector2 heading = next - shot.Pos;
            if (heading.sqrMagnitude > DirEps) shot.Dir = heading.normalized;

            shot.Pos = next;

            if ((next - target.Pos).sqrMagnitude > ArriveEps) return;

            shot.SegFrom = -1;
            shot.SegTo = -1;

            shot.Node = shot.Step++;

            if (shot.Step < shot.Plan.Count) BeginSegment(shot, shot.Plan[shot.Node], shot.Plan[shot.Step]);
        }

        /// <summary>出射：水平按残影朝向，垂直沿用进入方向；出射一次就永久作废，防止回飞后再被接管</summary>
        private void Leave(Shot shot)
        {
            int facing = shot.Plan.Count > shot.Node ? shot.Plan[shot.Node].Facing : 1;

            Vector2 dir = new(facing < 0 ? -1f : 1f, shot.EnterDir.y);
            if (dir.sqrMagnitude < DirEps) dir = new(facing < 0 ? -1f : 1f, 0f);

            shot.Dir = dir.normalized;
            shot.State = BulletState.Flying;
            shot.Transported = true;
            shot.SegFrom = -1;
            shot.SegTo = -1;
            shot.Step = 0;
            shot.Node = 0;

            shot.Plan.Clear();
        }

        private static void BeginSegment(Shot shot, in TransferNode from, in TransferNode to)
        {
            shot.SegFrom = from.Index;
            shot.SegTo = to.Index;
        }

        private static BulletPose Pose(Shot shot) =>
            new BulletPose(shot.Pos, shot.Dir, shot.Speed, shot.State, shot.SegFrom, shot.SegTo);

        private Shot Find(int id)
        {
            for (int i = 0; i < _live.Count; i++)
            {
                if (_live[i].Id == id) return _live[i];
            }

            return null;
        }
    }
}
