using Astra.Attributes;
using Astra.Effects;

using HarmonyLib;

using LabApi.Features.Wrappers;

namespace Astra.Patches;
    /// <summary>
    /// 为玩家挂载 <see cref="CustomEffectController"/>：在 <c>ReferenceHub.Awake</c> 之后追加组件并绑定宿主。
    /// </summary>
    [ManagedPatch, HarmonyPatch(typeof(ReferenceHub), nameof(ReferenceHub.Awake))]
    internal static class ComponentApplyPatch
    {
        /// <summary>
        /// 后置补丁：仅对真实玩家挂载一次控制器（非玩家 hub 不挂载）。
        /// </summary>
        /// <param name="__instance">被唤醒的 <see cref="ReferenceHub"/>。</param>
        [HarmonyPostfix]
        public static void Postfix(ReferenceHub __instance)
        {
            var player = Player.Get(__instance);
            if (player is null)
                return;

            if (__instance.gameObject.TryGetComponent<CustomEffectController>(out _))
                return;

            __instance.gameObject.AddComponent<CustomEffectController>().Bind(player);
        }
    }

