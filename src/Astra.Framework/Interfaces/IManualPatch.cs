using HarmonyLib;

namespace Astra.Interfaces
{
    /// <summary>
    /// 表示一个手动应用 Harmony 补丁的组件。
    /// 与声明式 <c>[HarmonyPatch]</c> 不同，实现该接口可在代码中自行决定如何打补丁。
    /// </summary>
    public interface IManualPatch
    {
        /// <summary>
        /// 应用补丁。
        /// </summary>
        /// <param name="harmony">用于打补丁的 Harmony 实例。</param>
        void ApplyPatch(Harmony harmony);
    }
}
