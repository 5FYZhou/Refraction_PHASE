using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    public class ConditionGroup : StateGroup<ConditionStateTag, LogicContext>
    {
        protected override ConditionStateTag EmptyTag => throw new System.NotImplementedException();

        protected override ConditionStateTag Fallback(in LogicContext ctx)
        {
            throw new System.NotImplementedException();
        }
    }
}
