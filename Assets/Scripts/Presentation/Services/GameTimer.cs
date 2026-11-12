using Refraction.Logic.Service;
using UnityEngine;

namespace Refraction.Presentation.Adapters
{
    public sealed class GameTimer : IGameTime
    {
        public float UnscaledDeltaTime => Time.unscaledDeltaTime;

        public float TimeScale => Time.timeScale;

        public void SetTimeScale(float scale)
        {
            Time.timeScale = scale;
        }
    }
}
