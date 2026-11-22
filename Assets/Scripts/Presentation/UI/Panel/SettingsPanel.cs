using System;
using Refraction.Logic.Input;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace Refraction.Presentation.UI
{
    /// <summary>按键设置面板：每行是 BtnKey 预制体实例，点一行后等下一次键盘按下，键位交给 InputKeyMgr 存档并立即回显</summary>
    public sealed class SettingsPanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.Middle;

        public override bool CanBeHideByKey => true;

        public override bool LiveWithScene => false;

        private const string KeyRootName = "KeyRoot";

        private const string ButtonPrefix = "BtnKey";

        private const string LabelName = "Txt";

        private const string KeyTextName = "TxtKey";

        /// <summary>name 拼出实例名 BtnKey??，label 是行内 Txt 的中文名</summary>
        private static readonly (PlayerKeyTag tag, string name, string label)[] Rows =
        {
            (PlayerKeyTag.MoveUp, "Up", "上"),
            (PlayerKeyTag.MoveDown, "Down", "下"),
            (PlayerKeyTag.MoveLeft, "Left", "左"),
            (PlayerKeyTag.MoveRight, "Right", "右"),
            (PlayerKeyTag.Jump, "Jump", "跳跃"),
            (PlayerKeyTag.Dash, "Dash", "冲刺"),
            (PlayerKeyTag.Attack, "Attack", "攻击"),
            (PlayerKeyTag.Crouch, "Crouch", "蹲下"),
            (PlayerKeyTag.Charge, "Charge", "充能"),
            (PlayerKeyTag.Reverse, "Reverse", "反转")
        };

        private readonly TMP_Text[] _texts = new TMP_Text[Rows.Length];

        private IDisposable _capture;
        private int _waitingRow = -1;

        protected override void Awake()
        {
            base.Awake();

            // 行的子文本都叫 Txt / TxtKey，按名进 components 会互相覆盖，只能顺着实例找
            Transform keyRoot = transform.Find(KeyRootName);

            for (int i = 0; i < Rows.Length; i++)
            {
                Transform row = keyRoot != null ? keyRoot.Find(ButtonPrefix + Rows[i].name) : null;
                if (row == null) continue;

                TMP_Text label = FindText(row, LabelName);
                if (label != null) label.text = Rows[i].label;

                _texts[i] = FindText(row, KeyTextName);
            }
        }

        public override void ShowMe()
        {
            Refresh();
        }

        protected override void OnButtonClicked(string name)
        {
            if (name == "BtnClose")
            {
                root.UIMgr.HidePanel<SettingsPanel>();
                root.Game.Resume();
                return;
            }

            for (int i = 0; i < Rows.Length; i++)
            {
                if (name != ButtonPrefix + Rows[i].name) continue;

                BeginCapture(i);
                return;
            }
        }

        private void OnDisable()
        {
            EndCapture();
        }

        private void BeginCapture(int row)
        {
            EndCapture();

            _waitingRow = row;
            _capture = InputSystem.onAnyButtonPress.Call(OnAnyButtonPressed);

            Refresh();
        }

        /// <summary>只吃键盘按下：鼠标点这排按钮的那一下也在 onAnyButtonPress 里，必须滤掉</summary>
        private void OnAnyButtonPressed(InputControl control)
        {
            if (_waitingRow < 0 || root == null || root.InputKeys == null) return;

            if (!(control.device is Keyboard)) return;

            int row = _waitingRow;
            EndCapture();

            root.InputKeys.SetKey(Rows[row].tag, control.name);

            Refresh();
        }

        private void EndCapture()
        {
            _waitingRow = -1;

            _capture?.Dispose();
            _capture = null;
        }

        private void Refresh()
        {
            for (int i = 0; i < _texts.Length; i++)
            {
                if (_texts[i] == null) continue;

                if (i == _waitingRow)
                {
                    _texts[i].text = "...";
                    continue;
                }

                string key = root != null && root.InputKeys != null ? root.InputKeys.GetKey(Rows[i].tag) : null;

                _texts[i].text = string.IsNullOrEmpty(key) ? string.Empty : key.ToUpperInvariant();
            }
        }

        private static TMP_Text FindText(Transform row, string childName)
        {
            Transform child = row.Find(childName);

            return child != null ? child.GetComponent<TMP_Text>() : null;
        }
    }
}
