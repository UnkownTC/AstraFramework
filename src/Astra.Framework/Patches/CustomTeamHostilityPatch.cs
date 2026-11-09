using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using Astra.Attributes;
using Astra;
using Astra.Teams;

using PlayerRoles;
using PlayerRoles.PlayableScps.Scp096;

using Footprinting;

using PlayerStatsSystem;

using HarmonyLib;

using InventorySystem.Items.ThrowableProjectiles;
using Astra.Enums;
using Astra.Extensions;
using Astra.Roles;
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
/// 用自定义阵营关系接管原版「敌我判定」。
/// </summary>
/// <remarks>
/// <para>
/// 只替换 <see cref="HitboxIdentity.IsEnemy(ReferenceHub, ReferenceHub)"/> 的判定结果，
/// <b>不插入自建 DamageHandler</b>：
/// </para>
/// <list type="bullet">
/// <item>判定为敌对 → 原版按「非友军」结算，伤害不打折、不触发友伤逻辑。</item>
/// <item>判定为友好 → 原版按「友军」结算，完全走原版友伤系统（含服务器友伤开关与倍率）。</item>
/// <item>双方都没有自定义阵营 → 直接返回，交回原版。</item>
/// </list>
/// <para>
/// 未接管 <see cref="HitboxIdentity.IsEnemy(RoleTypeId, RoleTypeId)"/>：仅凭 <!-- 见备注 -->
/// 原版角色无法区分同一 <see cref="RoleTypeId"/> 下的不同自定义阵营，强行接管会误判。
/// </para>
/// </remarks>
[ManagedPatch]
[HarmonyPatch(typeof(HitboxIdentity), nameof(HitboxIdentity.IsEnemy), typeof(ReferenceHub), typeof(ReferenceHub))]
internal static class CustomTeamHostilityPatch
{
    /// <summary>前缀：有自定义阵营时接管判定结果。</summary>
    /// <param name="attacker">攻击方。</param>
    /// <param name="victim">受击方。</param>
    /// <param name="__result">原方法返回值。</param>
    /// <returns>返回 <see langword="false"/> 表示跳过原方法。</returns>
    [HarmonyPrefix]
    internal static bool Prefix(ReferenceHub attacker, ReferenceHub victim, ref bool __result)
    {
        if (!CustomTeamResolver.TryResolveHostility(attacker, victim, out var hostile))
            return true;

        __result = hostile;
        return false;
    }
}

/// <summary>
/// 让原版「友伤检测（自动处罚）」尊重自定义阵营关系。
/// </summary>
/// <remarks>
/// 原版 <c>FriendlyFireHandler</c>（internal 类型，按名字反射定位）用 <c>GetFaction()</c> 判断是否友伤，
/// 与自定义阵营无关：
/// 不同自定义阵营但同原版派系的交火会被误判为团队击杀并累计处罚。
/// 这里只在「自定义阵营判定为敌对」时跳过检测；其余情况保持原版。
/// </remarks>
[ManagedPatch]
[HarmonyPatch("FriendlyFireHandler", "IsFriendlyFire")]
internal static class CustomTeamFriendlyFireDetectorPatch
{
    /// <summary>前缀：自定义阵营判定为敌对时，不认定为友伤。</summary>
    /// <param name="damagedPlayer">受击玩家。</param>
    /// <param name="handler">伤害处理器。</param>
    /// <param name="__result">原方法返回值。</param>
    /// <returns>返回 <see langword="false"/> 表示跳过原方法。</returns>
    [HarmonyPrefix]
    internal static bool Prefix(ReferenceHub damagedPlayer, DamageHandlerBase handler, ref bool __result)
    {
        if (handler is not AttackerDamageHandler attackerHandler)
            return true;

        var attackerHub = attackerHandler.Attacker.Hub;
        if (!CustomTeamResolver.TryResolveHostility(attackerHub, damagedPlayer, out var hostile) || !hostile)
            return true;

        __result = false;
        return false;
    }
}

/// <summary>
/// SCP-079 扫描器：与 SCP 明确友好的自定义阵营不进入 <c>TrackedPlayers</c>。
/// </summary>
/// <remarks>
/// 原版 <c>Scp079ScannerTracker.AddTarget</c> 只看角色是否为 <c>HumanRole</c>，不看阵营；
/// 这里在加入追踪列表前拦截，友军不会被标记、不会被 <c>Scanned</c> 效果暴露。
/// </remarks>
[ManagedPatch]
[HarmonyPatch("Scp079ScannerTracker", "AddTarget")]
internal static class CustomTeamScp079TrackingPatch
{
    /// <summary>前缀：与 SCP 明确友好时不加入追踪列表。</summary>
    /// <param name="hub">待追踪玩家。</param>
    /// <returns>返回 <see langword="false"/> 表示跳过原方法。</returns>
    [HarmonyPrefix]
    internal static bool Prefix(ReferenceHub hub) =>
        !CustomTeamResolver.IsExplicitFriendlyWithScp(hub, RoleTypeId.Scp079);
}

/// <summary>
/// SCP-079 扫描器：计算「区域敌人数量」时排除与 SCP 明确友好的阵营。
/// </summary>
/// <remarks>
/// 原版 <c>Scp079ScannerTrackedPlayer.CheckEnemy</c> 按 <c>GetFaction()</c> 判断，
/// 友军会被计入敌人数量、影响扫描半径计算。
/// </remarks>
[ManagedPatch]
[HarmonyPatch("Scp079ScannerTrackedPlayer", "CheckEnemy")]
internal static class CustomTeamScp079CampingPatch
{
    /// <summary>前缀：与 SCP 明确友好时不计入敌人。</summary>
    /// <param name="hub">候选玩家。</param>
    /// <param name="__result">原方法返回值。</param>
    /// <returns>返回 <see langword="false"/> 表示跳过原方法。</returns>
    [HarmonyPrefix]
    internal static bool Prefix(ReferenceHub hub, ref bool __result)
    {
        if (!CustomTeamResolver.IsExplicitFriendlyWithScp(hub, RoleTypeId.Scp079))
            return true;

        __result = false;
        return false;
    }
}

/// <summary>
/// 伤害倍率：让 <see cref="AttackerDamageHandler.ProcessDamage"/> 的友伤折扣跟随自定义阵营。
/// </summary>
/// <remarks>
/// <para>
/// 原版按 <c>IsEnemy(RoleTypeId, RoleTypeId)</c> 决定是否乘 <c>_ffMultiplier</c> 并置
/// <see cref="AttackerDamageHandler.IsFriendlyFire"/>——该重载拿不到玩家实例，无法区分同一角色下的不同阵营。
/// </para>
/// <para>
/// 这里不改原逻辑，而是记录进入时的伤害值，在方法末尾修正：
/// </para>
/// <list type="bullet">
/// <item>自定义阵营判定**敌对** → 还原为未折扣伤害，并清除友伤标记（原版 FF 倍率不再生效）。</item>
/// <item>自定义阵营判定**友好** → 按原版倍率折算并置友伤标记（走原版友伤数值）。</item>
/// </list>
/// </remarks>
[ManagedPatch]
[HarmonyPatch(typeof(AttackerDamageHandler), "ProcessDamage")]
internal static class CustomTeamDamageMultiplierPatch
{
    private static readonly PropertyInfo FriendlyFireProperty =
        typeof(AttackerDamageHandler).GetProperty(
            nameof(AttackerDamageHandler.IsFriendlyFire),
            BindingFlags.Instance | BindingFlags.Public)!;

    private static readonly FieldInfo MultiplierField =
        typeof(AttackerDamageHandler).GetField("_ffMultiplier", BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly Action<AttackerDamageHandler, bool> FriendlyFireSetter =
        (Action<AttackerDamageHandler, bool>)Delegate.CreateDelegate(
            typeof(Action<AttackerDamageHandler, bool>),
            FriendlyFireProperty.GetSetMethod(true)!);

    /// <summary>记录进入时的原始伤害。</summary>
    /// <param name="__instance">伤害处理器。</param>
    /// <param name="__state">原始伤害值。</param>
    [HarmonyPrefix]
    internal static void Prefix(AttackerDamageHandler __instance, out float __state) =>
        __state = __instance.Damage;

    /// <summary>按自定义阵营修正伤害与友伤标记。</summary>
    /// <param name="__instance">伤害处理器。</param>
    /// <param name="ply">受击玩家。</param>
    /// <param name="__state">原始伤害值。</param>
    [HarmonyPostfix]
    internal static void Postfix(AttackerDamageHandler __instance, ReferenceHub ply, float __state)
    {
        if (!CustomTeamResolver.TryResolveHostility(__instance.Attacker.Hub, ply, out var hostile))
            return;

        if (hostile)
        {
            if (!__instance.IsFriendlyFire)
                return;

            // 原版按友军处理过 → 还原全额伤害并清除友伤标记
            __instance.Damage = __state;
            FriendlyFireSetter(__instance, false);
            return;
        }

        if (__instance.IsFriendlyFire)
            return;

        // 原版按敌军处理过，但自定义判定为友军 → 套用原版友伤倍率
        var multiplier = MultiplierField?.GetValue(null) is float value ? value : 1f;
        __instance.Damage = __state * multiplier;
        FriendlyFireSetter(__instance, true);
    }
}

/// <summary>
/// 爆炸伤害：自定义阵营判定为友好时完全免除爆炸伤害。
/// </summary>
/// <remarks>
/// <c>ExplosionGrenade</c> 的候选过滤用 <c>IsDamageable(RoleTypeId, RoleTypeId)</c>，拿不到玩家实例；
/// 但真正的伤害会进入 <see cref="ExplosionDamageHandler.ApplyDamage"/>，这里有受击者 hub，
/// 因此在此处一票否决即可（比逐个改候选过滤更稳）。
/// </remarks>
[ManagedPatch]
[HarmonyPatch(typeof(ExplosionDamageHandler), nameof(ExplosionDamageHandler.ApplyDamage))]
internal static class CustomTeamExplosionFriendlyPatch
{
    /// <summary>前缀：友军不吃爆炸伤害。</summary>
    /// <param name="__instance">爆炸伤害处理器。</param>
    /// <param name="ply">受击玩家。</param>
    /// <param name="__result">原方法返回值。</param>
    /// <returns>返回 <see langword="false"/> 表示跳过原方法。</returns>
    [HarmonyPrefix]
    internal static bool Prefix(ExplosionDamageHandler __instance, ReferenceHub ply,
        ref DamageHandlerBase.HandlerOutput __result)
    {
        if (!CustomTeamResolver.TryResolveHostility(__instance.Attacker.Hub, ply, out var hostile) || hostile)
            return true;

        __result = DamageHandlerBase.HandlerOutput.Nothing;
        return false;
    }
}

/// <summary>
/// SCP-096 近战：与 SCP 明确友好的阵营不吃 096 的挥击伤害。
/// </summary>
/// <remarks>
/// 原版 <c>Scp096HitHandler</c> 用 <c>IsEnemy(Team.SCPs, target.GetTeam())</c> 过滤目标——
/// 只看原版阵营，自定义友军会被拍死。这里在真正的伤害入口
/// <c>DealDamage(IDestructible, float)</c> 上拦截，目标若是友军直接返回未造成伤害。
/// </remarks>
[ManagedPatch]
[HarmonyPatch(typeof(Scp096HitHandler), "DealDamage", typeof(IDestructible), typeof(float))]
internal static class CustomTeamScp096HitPatch
{
    /// <summary>前缀：友军不吃 096 挥击。</summary>
    /// <param name="target">受击目标。</param>
    /// <param name="__result">原方法返回值。</param>
    /// <returns>返回 <see langword="false"/> 表示跳过原方法。</returns>
    [HarmonyPrefix]
    internal static bool Prefix(IDestructible target, ref bool __result)
    {
        if (target is not HitboxIdentity hitbox)
            return true;

        var hub = hitbox.TargetHub;
        if (!CustomTeamResolver.IsExplicitFriendlyWithScp(hub, RoleTypeId.Scp096))
            return true;

        __result = false;
        return false;
    }
}

/// <summary>
/// 闪光弹：自定义阵营判定为友好时，不闪光、不耳鸣、不计入命中数。
/// </summary>
/// <remarks>
/// <c>FlashbangGrenade.ProcessPlayer(ReferenceHub)</c> 是逐玩家入口，受击者 hub 与投掷者
/// (<c>PreviousOwner</c>，<c>ItemPickupBase</c> 上的 public 字段) 都可得，因此直接接管判定。
/// 原版这里用 <c>IsDamageable(RoleTypeId, RoleTypeId)</c> 过滤，拿不到玩家实例。
/// </remarks>
[ManagedPatch]
[HarmonyPatch(typeof(FlashbangGrenade), "ProcessPlayer")]
internal static class CustomTeamFlashbangFriendlyPatch
{
    /// <summary>前缀：友军不吃闪光与耳鸣。</summary>
    /// <param name="__instance">闪光弹实例。</param>
    /// <param name="hub">受影响的玩家。</param>
    /// <returns>返回 <see langword="false"/> 表示跳过原方法。</returns>
    [HarmonyPrefix]
    internal static bool Prefix(FlashbangGrenade __instance, ReferenceHub hub)
    {
        var ownerHub = __instance.PreviousOwner.Hub;   // Footprint 是结构体，判 Hub 是否为空
        if (ownerHub is null)
            return true;

        if (!CustomTeamResolver.TryResolveHostility(ownerHub, hub, out var hostile) || hostile)
            return true;

        return false;
    }
}

/// <summary>
/// 爆炸附加效果（灼烧 / 耳鸣 / 脑震荡）：友军不吃。
/// </summary>
/// <remarks>
/// <para>
/// <c>ExplosionGrenade.Explode</c> 是静态方法，效果通过私有的静态泛型
/// <c>TriggerEffect&lt;T&gt;(ReferenceHub, float, float)</c> 施加；该泛型方法只拿得到受击者，
/// 拿不到投掷者，无法单独判定。
/// </para>
/// <para>
/// 因此用「爆炸期间记录投掷者」的方式补上下文：<see cref="ExplodeOwnerPatch"/> 在
/// <c>Explode</c> 前后压栈/出栈，本补丁在效果施加前读取栈顶投掷者做判定。
/// 用栈而非单字段，是为兼容连锁爆炸的嵌套调用。
/// </para>
/// </remarks>
[ManagedPatch]
[HarmonyPatch]
internal static class CustomTeamExplosionEffectPatch
{
    /// <summary>
    /// 开放泛型方法（<c>TriggerEffect&lt;T&gt;</c>）无法被 Harmony 直接 patch
    /// （MonoMod 不支持导入泛型定义），这里返回三个具象实例。
    /// </summary>
    /// <returns>灼烧 / 耳鸣 / 脑震荡三个实例化方法。</returns>
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var open = AccessTools.Method(typeof(ExplosionGrenade), "TriggerEffect");
        yield return open!.MakeGenericMethod(typeof(CustomPlayerEffects.Burned));
        yield return open.MakeGenericMethod(typeof(CustomPlayerEffects.Deafened));
        yield return open.MakeGenericMethod(typeof(CustomPlayerEffects.Concussed));
    }
    /// <summary>前缀：友军不吃爆炸附加效果。</summary>
    /// <param name="hub">受影响的玩家。</param>
    /// <returns>返回 <see langword="false"/> 表示跳过原方法。</returns>
    [HarmonyPrefix]
    internal static bool Prefix(ReferenceHub hub)
    {
        var owner = ExplodeOwnerScope.Current;
        if (owner is null)
            return true;

        if (!CustomTeamResolver.TryResolveHostility(owner, hub, out var hostile) || hostile)
            return true;

        return false;
    }
}

/// <summary>在 <see cref="ExplosionGrenade.Explode"/> 执行期间记录投掷者，供效果补丁查询。</summary>
[ManagedPatch]
[HarmonyPatch(typeof(ExplosionGrenade), nameof(ExplosionGrenade.Explode))]
internal static class ExplodeOwnerPatch
{
    /// <summary>进入爆炸：压入投掷者。</summary>
    /// <param name="attacker">投掷者足迹。</param>
    [HarmonyPrefix]
    internal static void Prefix(Footprint attacker) => ExplodeOwnerScope.Push(attacker.Hub);

    /// <summary>离开爆炸：弹出投掷者。</summary>
    [HarmonyPostfix]
    internal static void Postfix() => ExplodeOwnerScope.Pop();
}

/// <summary>爆炸投掷者作用域（栈式，兼容嵌套爆炸）。</summary>
internal static class ExplodeOwnerScope
{
    private static readonly Stack<ReferenceHub?> Owners = new();

    /// <summary>当前正在结算的爆炸投掷者；不在爆炸中时为 <see langword="null"/>。</summary>
    internal static ReferenceHub? Current => Owners.Count > 0 ? Owners.Peek() : null;

    /// <summary>压入投掷者。</summary>
    /// <param name="hub">投掷者 hub。</param>
    internal static void Push(ReferenceHub? hub) => Owners.Push(hub);

    /// <summary>弹出投掷者。</summary>
    internal static void Pop()
    {
        if (Owners.Count > 0)
            Owners.Pop();
    }
}

/// <summary>
/// SCP-096 挥击门禁转译：把 <c>ProcessHits</c> 里的
/// <c>IsEnemy(Team.SCPs, TargetHub.GetTeam())</c>（拿不到玩家实例）替换为
/// <c>IsEnemy096HitGate(Team, HitboxIdentity)</c>（带受击者 hub 的完整自定义阵营判定）。
/// </summary>
/// <remarks>
/// 这是为了修复「096 能追着自定义阵营玩家跑、却拍不到」的割裂：
/// 目标获取走 <c>IsEnemy(hub, hub)</c>（已被接管），而挥击门禁走 Team 重载（未接管）。
/// 转译按「ldc(Team.SCPs) + 加载 hitbox + get_TargetHub + GetTeam + call IsEnemy」的
/// 固定序列匹配，替换后栈形态等价（Team, hitbox）。
/// </remarks>
[ManagedPatch]
[HarmonyPatch(typeof(Scp096HitHandler), "ProcessHits")]
internal static class CustomTeamScp096GateTranspiler
{
    private static readonly MethodInfo IsEnemyTeamOverload = AccessTools.Method(
        typeof(HitboxIdentity), nameof(HitboxIdentity.IsEnemy), new[] { typeof(Team), typeof(Team) });

    private static readonly MethodInfo TargetHubGetter = AccessTools.PropertyGetter(
        typeof(HitboxIdentity), nameof(HitboxIdentity.TargetHub));

    private static readonly MethodInfo GetTeamMethod = AccessTools.Method(
        typeof(PlayerRolesUtils), nameof(PlayerRolesUtils.GetTeam), new[] { typeof(ReferenceHub) });

    private static readonly MethodInfo GateResolver = AccessTools.Method(
        typeof(CustomTeamResolver), nameof(CustomTeamResolver.IsEnemy096HitGate));

    /// <summary>转译：替换门禁调用。</summary>
    /// <param name="instructions">原方法 IL。</param>
    /// <returns>转译后的 IL。</returns>
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        for (var i = 0; i < codes.Count; i++)
        {
            if (!codes[i].Calls(IsEnemyTeamOverload))
                continue;

            // 向前定位：IsEnemy 的前一应是 GetTeam，其前应是 get_TargetHub
            var hubIndex = -1;

            for (var j = i - 1; j >= Math.Max(0, i - 4); j--)
            {
                if (codes[j].Calls(GetTeamMethod) && j > 0 && codes[j - 1].Calls(TargetHubGetter))
                {
                    hubIndex = j - 1;
                    break;
                }
            }

            if (hubIndex < 0)
                continue;   // 形态不符，保持原样（不误伤）

            // [ldloc hitbox][get_TargetHub][GetTeam][IsEnemy] → [ldloc hitbox][call 解析器]
            var labels = codes[hubIndex + 1].labels;
            labels.AddRange(codes[hubIndex + 2].labels);

            codes[hubIndex + 1] = new CodeInstruction(OpCodes.Nop);
            codes[hubIndex + 2] = new CodeInstruction(OpCodes.Nop);
            codes[i] = new CodeInstruction(OpCodes.Call, GateResolver).WithLabels(labels);
            break;
        }

        return codes;
    }
}
