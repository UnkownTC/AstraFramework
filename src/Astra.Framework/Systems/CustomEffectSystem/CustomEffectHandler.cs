using Astra.Attributes;

using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.CustomHandlers;

namespace Astra.Systems.CustomEffectSystem;

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
