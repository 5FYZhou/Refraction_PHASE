using Refraction.Logic.Actor;

namespace Refraction.Logic.Level
{
    /// <summary>跨场景通道：由常驻根实现，LevelManager 切场景前把落点交出去——本关对象随旧场景销毁，加载完再问就晚了</summary>
    public interface ISceneTransfer
    {
        void RequestLevelLoad(int doorId, MovementStateTag enterTag, int enterFace);
    }
}
