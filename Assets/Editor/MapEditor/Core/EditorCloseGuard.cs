using System;
using UnityEditor;

namespace Refraction.MapEditor
{
    public sealed class EditorCloseGuard
    {
        private readonly string _title;
        private readonly Func<bool> _hasChanges;
        private readonly Action _save;
        private bool _handled;

        public EditorCloseGuard(string title, Func<bool> hasChanges, Action save)
        {
            _title = title;
            _hasChanges = hasChanges;
            _save = save;
        }

        public void ConfirmOnDestroy()
        {
            if (_handled || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!_hasChanges()) return;
            _handled = true;
            Ask();
        }

        public bool ConfirmOnQuit()
        {
            if (_handled || !_hasChanges()) return true;
            _handled = true;
            Ask();
            return true;
        }

        private void Ask()
        {
            if (EditorUtility.DisplayDialog(_title, "有未保存的更改，是否保存？", "保存", "不保存")) _save();
        }
    }
}
