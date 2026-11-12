using System.Collections.Generic;

namespace Refraction.Logic.Service
{
    public sealed class PauseService : IService
    {
        private readonly IGameTime gameTime;
        private readonly List<object> _freezeOwners = new(3);

        private float pausedTime;

        public bool IsPaused => _freezeOwners.Count > 0;

        public float PausedTime => pausedTime;

        public PauseService(IGameTime gameTime)
        {
            this.gameTime = gameTime;
        }

        public void Init()
        {
            _freezeOwners.Clear();
            pausedTime = 0f;

            gameTime.SetTimeScale(1f);
        }

        public void Dispose()
        {
            _freezeOwners.Clear();

            gameTime.SetTimeScale(1f);
        }

        // 暂停时 dt 已缩放到 0
        public void Tick(float deltaTime, float unscaledDeltaTime)
        {
            if (IsPaused)
            {
                pausedTime += unscaledDeltaTime;
            }
        }

        // 冻结来源成对登记；重复或缺失都是 no-op，count>0 即冻结
        public void PushFreeze(object owner)
        {
            if (owner == null || _freezeOwners.Contains(owner)) return;

            _freezeOwners.Add(owner);

            if (_freezeOwners.Count == 1)
            {
                gameTime.SetTimeScale(0f);
            }
        }

        public void PopFreeze(object owner)
        {
            if (owner == null || !_freezeOwners.Remove(owner)) return;

            if (_freezeOwners.Count == 0)
            {
                gameTime.SetTimeScale(1f);
            }
        }
    }
}
