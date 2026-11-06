

namespace Refraction.Logic
{
    public interface IFixedTickable
    {
        void FixedTick(LogicContext cxt);
    }

    public interface ITickable
    {
        void Tick(LogicContext cxt);
    }
}
