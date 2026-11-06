using UnityEngine;

namespace Refraction.Foundation
{
    /// <summary>装配校验：引用为空当场抛错，空引用拖到几帧后炸时堆栈已指不回是哪个字段没连</summary>
    public static class Require
    {
        /// <summary>要求引用已装配，返回原值便于链式调用</summary>
        /// <remarks>约束到 Object 是有意的：== null 走 Unity 重载，才能识别已销毁对象的伪 null</remarks>
        public static T Assigned<T>(T value, string owner, string field) where T : Object
        {
            if (value == null)
            {
                throw new MissingReferenceException($"[{owner}] 装配缺失：{field} 未在 Inspector 中赋值。");
            }

            return value;
        }
    }
}
