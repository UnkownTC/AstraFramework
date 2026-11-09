using System;

namespace Astra.Attributes
{
    /// <summary>
    /// 标记一个类为框架托管的 Harmony 补丁。
    /// 需配合 <see cref="HarmonyLib.HarmonyPatch"/>（声明式）或 <see cref="Interfaces.IManualPatch"/>（手动式）使用，
    /// 由框架在自动注册阶段统一应用，并在框架禁用时统一卸载。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class ManagedPatchAttribute : Attribute
    {
    }
}
