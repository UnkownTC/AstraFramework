// -----------------------------------------------------------------------
// <copyright file="Shooting.cs" company="ExMod Team">
// Copyright (c) ExMod Team. All rights reserved.
// Licensed under the CC BY-SA 3.0 license.
// </copyright>
// -----------------------------------------------------------------------

using System;

using Astra.Attributes;

using HarmonyLib;

using InventorySystem.Items.Firearms;
using InventorySystem.Items.Firearms.Modules.Misc;

using PlayerEvents = Astra.Events.PlayerEvents;
using ShootingEventArgs = Astra.Events.ShootingEventArgs;

namespace Astra.Patches;
    /// <summary>
    /// Patches <see cref="ShotBacktrackData.ProcessShot" />.
    /// Adds the <see cref="PlayerEvents.Shooting" /> event.
    /// <para>
    /// 2026-09 重构:原实现为 Transpiler(在两处 <c>processingMethod</c> 调用点注入事件,
    /// <c>IsAllowed=false</c> 时跳过调用)。现改为 <b>Prefix 包装委托</b>:
    /// Prefix 将 <c>processingMethod</c> 替换为"先触发事件、被允许才调用原委托"的包装。
    /// 事件仍然在 <c>WaypointBacktracker</c>/<c>FpcBacktracker</c> 回溯上下文内部触发
    /// (处理程序读取到的射手/目标位置为回溯后位置,与原 Transpiler 完全一致);
    /// <c>IsAllowed=false</c> 时跳过原始处理,回溯上下文照常建立并释放。
    /// </para>
    /// </summary>
    [ManagedPatch, HarmonyPatch(typeof(ShotBacktrackData), nameof(ShotBacktrackData.ProcessShot))]
    internal static class Shooting
    {
        private static bool Prefix(ShotBacktrackData __instance, Firearm firearm, ref Action<ReferenceHub> processingMethod)
        {
            ShotBacktrackData instance = __instance;
            Action<ReferenceHub> original = processingMethod;

            processingMethod = hub =>
            {
                // 原 Transpiler 注入点:processingMethod 调用处(回溯上下文内)
                ShootingEventArgs ev = new(firearm, ref instance);
                PlayerEvents.OnShooting(ev);

                if (ev.IsAllowed)
                {
                    original(hub);
                }
            };

            return true; // 让 ProcessShot 正常执行,事件在其回溯上下文内的调用点触发
        }
    }

