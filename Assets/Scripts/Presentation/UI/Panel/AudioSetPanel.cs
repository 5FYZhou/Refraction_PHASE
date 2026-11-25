using UnityEngine;
using UnityEngine.UI;

namespace Refraction.Presentation.UI
{
    /// <summary>音频设置面板：两条滑条直接改 AudioManager 的音乐与音效音量，滑动当帧生效</summary>
    public sealed class AudioSetPanel : SettingSubPanel
    {
        private const string MusicSliderName = "SldMusic";

        private const string SfxSliderName = "SldSfx";

        protected override void Awake()
        {
            base.Awake();

            if (root == null || root.Audio == null) return;

            Slider music = GetComponent<Slider>(MusicSliderName);
            if (music != null) music.SetValueWithoutNotify(root.Audio.BgmVolume);

            Slider sfx = GetComponent<Slider>(SfxSliderName);
            if (sfx != null) sfx.SetValueWithoutNotify(root.Audio.SfxVolume);
        }

        protected override void CloseSelf()
        {
            root.UIMgr.HidePanel<AudioSetPanel>();
        }

        protected override void OnSliderValueChange(string sliderName, float value)
        {
            if (root == null || root.Audio == null) return;

            switch (sliderName)
            {
                case MusicSliderName:
                    root.Audio.SetBgmVolume(value);
                    break;
                case SfxSliderName:
                    root.Audio.SetSfxVolume(value);
                    break;
            }
        }
    }
}
