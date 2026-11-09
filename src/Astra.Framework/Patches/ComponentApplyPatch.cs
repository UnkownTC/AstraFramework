using Astra.Attributes;

using HarmonyLib;

using LabApi.Features.Wrappers;
using Astra;
using Astra.Enums;
using Astra.Extensions;
using Astra.Roles;
using Astra.Teams;
using Astra.Waves;
using Astra.Items;
using Astra.Effects;
using Astra.Abilities;
using Astra.HumeShield;
using Astra.Patching;
using Astra.Hints;
using Astra.Audio;
using Astra.Countdown;
using Astra.Cancellation;
using Astra.ServerSpecificSettings;
using Astra.Loader;

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

