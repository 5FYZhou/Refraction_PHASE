namespace Refraction.Foundation
{
    /// <summary>Resources 里全部资源路径的唯一出口：一律相对 Resources、不带扩展名，挪目录只改这张表</summary>
    /// <remarks>带 Assets/ 前缀的几条是 AssetDatabase 口径（Editor 测试用），喂给 Resources.Load 一定返回 null</remarks>
    public static class ResPath
    {
        public const string AudioConfig = "Data/Settings/AudioConfig";
        public const string UIStyleConfig = "Data/Settings/UIStyleConfig";
        public const string BlackAttackVisualConfig = "Data/Player/BlackAttackVisualConfig";
        public const string AfterimageVisualConfig = "Data/Player/AfterimageVisualConfig";
        public const string EnemyColliderConfig = "Data/Enemy/EnemyColliderConfig";

        public const string BulletPrefab = "Prefabs/Bullet/Bullet";
        public const string AfterimagePrefab = "Prefabs/AfterImage";
        public const string PlayerHeadPrefab = "Prefabs/PlayerHead";

        public const string HitSpark = "Effects/HitSpark";
        public const string AttackSpark = "Effects/AttackSpark";
        public const string ReboundSpark = "Effects/ReboundSpark";
        public const string Land = "Effects/Land";
        public const string MoveDust = "Effects/MoveDust";
        public const string SavePointSpark = "Effects/SavePointSpark";
        public const string ChargeIn = "Effects/ChargeIn";
        public const string ChargeDash = "Effects/ChargeDash";
        public const string PlayerDie = "Effects/PlayerDie";

        public const string UICamera = "ui/UICamera";
        public const string UICanvas = "ui/Canvas";
        public const string UIEventSystem = "ui/EventSystem";
        public const string UIPanelFolder = "ui/Panel/";

        public const string SoundFolder = "Sound/";

        public const string AssetsPlayerPrefab = "Assets/Resources/Prefabs/Player.prefab";
        public const string AssetsEnemyPrefab = "Assets/Resources/Prefabs/Enemys/Enemy.prefab";
    }
}
