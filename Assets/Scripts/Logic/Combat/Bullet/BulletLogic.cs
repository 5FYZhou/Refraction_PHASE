using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Logic.Combat
{
    /// <summary>子弹系统：全场景只有一份，发射先记成请求，姿势与路径在 Tick 里推</summary>
    /// <remarks>不造实例、不管池：请求由 CombatRoot 取走并落地，视图由它交回来。阵营只写进 Damage</remarks>
    public sealed class BulletLogic
    {
        private const float ArriveEps = 1e-4f;
        private const float DirEps = 1e-6f;

        private sealed class Shot
        {
            public IBulletView View;
            public AfterimageLogic Logic;
            public Vector2 Pos;
            public Vector2 Dir;
            public Vector2 EnterDir = Vector2.right;
            public float Speed;
            public float TransferSpeed;
            public BulletState State;
            public int Id;
            public int OwnerId;
            public int Step;
            public int Node;
            public int SegFrom = -1;
            public int SegTo = -1;
            public bool Transported;
            public bool Dead;
            public readonly List<TransferNode> Plan = new();
        }

        private readonly List<Shot> _live = new();
        private readonly List<BulletRequest> _pending = new();

        private int _nextId;
        private int _head;

        public int Count => _live.Count;

        /// <summary>逻辑层开火的唯一入口：先记账再挂请求，实例由 CombatRoot 在同一帧取走</summary>
        public void Launch(in BulletShooter shooter, in BulletSpawn spawn)
        {
            Shot shot = new()
            {
                Id = _nextId++,
                OwnerId = shooter.OwnerId,
                Pos = spawn.Origin,
                Dir = spawn.Direction,
                Speed = spawn.Speed,
                TransferSpeed = spawn.TransferSpeed,
                State = BulletState.Flying,
            };

            _live.Add(shot);

            _pending.Add(new BulletRequest(
                shot.Id,
                in shooter,
                Damage.FromBullet(shooter.Faction, spawn.Damage, spawn.Direction, spawn.KnockbackSpeed),
                spawn.Origin,
                spawn.Direction,
                spawn.Speed,
                spawn.TransferSpeed));
        }

        /// <summary>取一条还没落地的请求：CombatRoot 每帧排空，取完即清表，不留旧请求</summary>
        public bool TryTakeRequest(out BulletRequest request)
        {
            if (_head >= _pending.Count)
            {
                request = default;
                return false;
            }

            request = _pending[_head++];

            if (_head < _pending.Count) return true;

            _pending.Clear();
            _head = 0;

            return true;
        }

        /// <summary>视图落地后交回来：挂上并立刻摆到出膛姿势，别等下一次 Tick</summary>
        public void AttachView(int id, IBulletView view)
        {
            Shot shot = Find(id);

            if (shot == null) return;

            shot.View = view;
            shot.View.Apply(Pose(shot));
        }

        /// <summary>弹反改写弹道：视图那边已经反向，这里跟着改，否则下一次 Apply 会把朝向摆回去</summary>
        public void Redirect(int id, Vector2 dir, float speed, float transferSpeed)
        {
            Shot shot = Find(id);

            if (shot == null) return;

            shot.Dir = dir;
            shot.Speed = speed;
            shot.TransferSpeed = transferSpeed;
        }

        /// <summary>每物理帧推一次：飞行中的只跟读位置，传输中的逐段走，走完或目标失效就地出射</summary>
        public void Tick(float deltaTime)
        {
            for (int i = 0; i < _live.Count; i++)
            {
                Shot shot = _live[i];
                if (shot.Dead) continue;

                // 请求还没被 CombatRoot 落地，这一发还没有身体
                if (shot.View == null) continue;

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

        /// <summary>收一发：撞到 bound、地面，或命中结算完时调这里，本层摘账再让视图回池</summary>
        public void Kill(int id)
        {
            Shot shot = Find(id);
            if (shot == null) return;

            shot.Dead = true;
            _live.Remove(shot);

            shot.View?.Kill();
        }

        /// <summary>只清某个发射者的子弹：一只敌人复位不该顺手抹掉别人的弹</summary>
        public void ClearOwner(int ownerId)
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                if (_live[i].OwnerId != ownerId) continue;

                Kill(_live[i].Id);
            }
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

        /// <summary>出射：顺着残影飞进来的按残影朝向出，逆着飞进来的反向出；出射一次就永久作废，防止回飞后再被接管</summary>
        private void Leave(Shot shot)
        {
            int facing = shot.Plan.Count > shot.Node ? shot.Plan[shot.Node].Facing : 1;

            // 判据取入口那枚：子弹顶着残影飞进来时出射也要顶着走，否则等于被弹回原路
            if (shot.Plan.Count > 0 && shot.EnterDir.x * shot.Plan[0].Facing < 0f) facing = -facing;

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
