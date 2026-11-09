using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Astra.Attributes;
using Astra.Interfaces;

using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.CustomHandlers;

namespace Astra.Systems.CustomHumeShieldSystem;

[ManagedEvent("Astra - CustomHumeShieldStat")]
public class CustomHumeShieldEventHandler : CustomEventsHandler
{
    public override void OnPlayerChangingRole(PlayerChangingRoleEventArgs ev)
    {
        // 在角色实际切换【前】收口护盾：
        // 此时清掉覆盖锁（MaxValue 负值），新角色的原生护盾上限/再生才能在角色初始化后正常接管。
        // 若放在 ChangedRole（切换后），会晚于新角色初始化，存在把原生值覆盖为 0 的时序风险。
        CustomHumeShieldManager.RemoveHumeShield(ev.Player);
    }
}
