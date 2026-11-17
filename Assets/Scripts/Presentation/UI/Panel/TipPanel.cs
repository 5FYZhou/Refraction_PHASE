using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Presentation.UI
{
    public sealed class TipPanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.Scene;

        public override bool CanBeHideByKey => false;

        public override bool LiveWithScene => true;
    }
}
