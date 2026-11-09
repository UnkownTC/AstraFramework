using HarmonyLib;
using Astra;
using Astra.Attributes;
using Astra.Enums;
using Astra.Extensions;
using Astra.Roles;
using Astra.Teams;
using Astra.Waves;
using Astra.Items;
using Astra.Effects;
using Astra.Abilities;
using Astra.HumeShield;
using Astra.Hints;
using Astra.Audio;
using Astra.Countdown;
using Astra.Cancellation;
using Astra.ServerSpecificSettings;
using Astra.Loader;

namespace Astra.Patching;
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

