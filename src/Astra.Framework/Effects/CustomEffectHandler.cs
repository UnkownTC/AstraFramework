using Astra.Attributes;

using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.CustomHandlers;
using Astra;
using Astra.Enums;
using Astra.Extensions;
using Astra.Roles;
using Astra.Teams;
using Astra.Waves;
using Astra.Items;
using Astra.Abilities;
using Astra.HumeShield;
using Astra.Patching;
using Astra.Hints;
using Astra.Audio;
using Astra.Countdown;
using Astra.Cancellation;
using Astra.ServerSpecificSettings;
using Astra.Loader;

namespace Astra.Effects;

/// <summary>
/// 玩家离开时清理其身上的自定义效果，避免宿主对象销毁后效果实例残留。
/// </summary>
[ManagedEvent("Astra - CustomEffectHandler")]
public class CustomEffectHandler : CustomEventsHandler
{
    /// <inheritdoc />
    public override void OnPlayerLeft(PlayerLeftEventArgs ev)
    {
        if (ev.Player.GameObject.TryGetComponent<CustomEffectController>(out var controller))
            controller?.Cleanup();
    }
}
