// -----------------------------------------------------------------------
// <copyright file="Shot.cs" company="ExMod Team">
// Copyright (c) ExMod Team. All rights reserved.
// Licensed under the CC BY-SA 3.0 license.
// </copyright>
// -----------------------------------------------------------------------

using Astra.Attributes;

using HarmonyLib;

using InventorySystem.Items.Firearms.Modules;
using InventorySystem.Items.Firearms.Modules.Misc;

using PlayerRoles;

using PlayerStatsSystem;
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
using PlayerEvents = Astra.Events.PlayerEvents;
using ShotEventArgs = Astra.Events.ShotEventArgs;

namespace Astra.Patches;
    /// <summary>
    /// Patches <see cref="HitscanHitregModuleBase.ServerApplyDestructibleDamage" />.
    /// Adds the <see cref="PlayerEvents.Shot" /> event.
    /// <para>
    /// 2026-09 重构:原实现为 Transpiler(在该方法体第一个 <c>ldloc.2</c> 处注入事件、
    /// <c>CanHurt=false</c> 时将伤害局部变量置 0、方法尾 <see cref="HitscanHitregModuleBase.ServerPlayImpactEffects"/>
    /// 之前注入 <c>CanSpawnImpactEffects</c> 检查)。现改为 <b>Prefix 完整替换</b>:
    /// 以当前游戏版本的反编译体为基础复制原方法逻辑,在原注入点等价位置触发事件。
    /// 行为保持:事件携带归零前的原始伤害;<c>CanHurt=false</c> 时后续
    /// <c>Destructible.Damage</c> 以 0 调用(handler 仍以原始伤害构造,与原注入一致);
    /// <c>CanSpawnImpactEffects=false</c> 时跳过命中特效(不影响 RegisterDamage)。
    /// </para>
    /// </summary>
    [ManagedPatch, HarmonyPatch(typeof(HitscanHitregModuleBase), nameof(HitscanHitregModuleBase.ServerApplyDestructibleDamage))]
    internal static class ShotTarget
    {
        private static bool Prefix(HitscanHitregModuleBase __instance, DestructibleHitPair target, HitscanResult result)
        {
            // —— 原方法体(反编译自当前专用服 Assembly-CSharp)——
            float damage = __instance.DamageAtDistance(target.Hit.distance);
            AttackerDamageHandler handler = __instance.GetHandler(damage);
            IDestructible destructible = target.Destructible;

            // —— 原 Transpiler 注入点:方法体首个 ldloc.2(destructible 首次读取处)——
            ShotEventArgs ev = new(__instance, target.Hit, __instance.Firearm, destructible, damage);
            PlayerEvents.OnShot(ev);

            if (!ev.CanHurt)
                damage = 0f;
            // ————————————————————————————————————————

            HitboxIdentity hitboxIdentity = destructible as HitboxIdentity;
            if (hitboxIdentity != null && !hitboxIdentity.TargetHub.IsAlive())
            {
                result.RegisterDamage(destructible, damage, handler);
                return false;
            }

            if (!destructible.Damage(damage, handler, target.Hit.point))
            {
                return false;
            }

            result.RegisterDamage(destructible, damage, handler);

            // —— 原 Transpiler 注入点:方法尾(this.ServerPlayImpactEffects 之前)——
            if (ev.CanSpawnImpactEffects)
            {
                __instance.ServerPlayImpactEffects(target.Raycast, damage > 0f);
            }

            return false; // 已完整复制原方法逻辑,跳过原始方法
        }
    }

