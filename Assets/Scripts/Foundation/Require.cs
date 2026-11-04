using UnityEngine;

namespace Refraction.Foundation
{
    /// <summary>
    /// 装配校验：序列化引用为空就当场抛错。
    /// 空引用拖到运行几帧后才炸时，堆栈已经指不回是哪个字段没连了。
    /// </summary>
    public static class Require
    {
        /// <summary>
        /// 要求引用已装配，返回原值方便链式使用。
        /// 泛型约束到 <see cref="Object"/> 是有意的：这样 == null 走的是 Unity 的重载，
        /// 被销毁的对象（伪 null）也能被识别出来。
        /// </summary>
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
