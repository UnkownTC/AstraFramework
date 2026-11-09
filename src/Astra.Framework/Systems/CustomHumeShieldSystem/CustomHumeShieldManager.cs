using System;
using System.Collections.Generic;

using Astra.Interfaces;

using CustomPlayerEffects;

using LabApi.Features.Wrappers;

using Mirror;

using PlayerStatsSystem;

using UnityEngine;

using Object = UnityEngine.Object;

namespace Astra.Systems.CustomHumeShieldSystem;

/// <summary>
/// 自定义角色的休谟护盾驱动器。
/// </summary>
/// <remarks>
/// <para>
/// 行为复刻原版 <c>PlayerRoles.PlayableScps.HumeShield.DynamicHumeShieldController</c>：
/// 上限按血量比例曲线计算、受伤后进入冷却、冷却结束按 <c>速率 × deltaTime</c> 逐帧恢复、
/// 支持外部 blocker 阻断恢复、跌破 0 时触发破盾事件。
/// </para>
/// <para>
/// 与原生护盾的关系：只读写 <c>HumeShieldStat</c> 的 <c>MaxValue</c> / <c>CurValue</c>，
/// 网络同步、护盾条、观战显示全部沿用原版实现。原生 <c>HumeShieldStat.Update()</c> 仍会运行，
/// 但人类阵营取到的恢复速率为 0，不会与本控制器争夺写入。
/// </para>
/// <para>
/// 注意：<c>MaxValue</c> 一旦被写入即置位 <c>_maxValueOverride</c>，这是刻意为之 ——
/// 让原生循环以本控制器的上限为准，而不是去问 <c>IHumeShieldProvider</c>。
/// </para>
/// </remarks>
internal sealed class CustomHumeShieldManager : MonoBehaviour
{
    /// <summary>阻断源失效判定；缓存为静态委托，避免每帧因方法组转换产生分配。</summary>
    private static readonly Predicate<ICustomHumeShieldBlocker> BlockerStalePredicate = IsBlockerStale;

    private readonly HashSet<ICustomHumeShieldBlocker> _blockers = new();

    private int _blockersCount;
    private HumeShieldStat _shield;
    private HealthStat _health;
    private double _nextRegenTime;
    private bool _subscribed;

    /// <summary>护盾参数来源。</summary>
    public ICustomHumeShieldStat HumeShieldStat { get; private set; }

    /// <summary>护盾所属玩家。</summary>
    public Player Owner { get; private set; }

    /// <summary>当前护盾上限（随血量比例变化）。</summary>
    public float HsMax => EvaluateMax();

    /// <summary>当前是否被阻断恢复。</summary>
    public bool IsBlocked
    {
        get
        {
            // 无阻断源时完全跳过清理，避免空集合上的逐帧遍历。
            if (_blockers.Count == 0)
            {
                _blockersCount = 0;
                return false;
            }

            _blockersCount -= _blockers.RemoveWhere(BlockerStalePredicate);
            return _blockersCount > 0;
        }
    }

    /// <summary>
    /// 为玩家挂载护盾。已有实例会先收口再重建，避免重复组件。
    /// </summary>
    /// <param name="player">目标玩家。</param>
    /// <param name="humeShield">护盾参数。</param>
    public static void AddHumeShield(Player player, ICustomHumeShieldStat humeShield)
    {
        if (player is null || humeShield is null)
            return;

        RemoveHumeShield(player);

        var manager = player.GameObject.AddComponent<CustomHumeShieldManager>();
        manager.Owner = player;
        manager.HumeShieldStat = humeShield;
        manager.Initialize();
    }

    /// <summary>
    /// 收口玩家身上的护盾：清零并销毁驱动器。
    /// </summary>
    /// <param name="player">目标玩家。</param>
    public static void RemoveHumeShield(Player player)
    {
        if (player is null)
            return;

        var manager = player.GameObject.GetComponent<CustomHumeShieldManager>();
        if (manager is null)
            return;

        manager.Cleanup();
        Object.Destroy(manager);
    }

    /// <summary>解除恢复冷却，立即开始恢复（对齐原版 <c>ResumeRegen</c>）。</summary>
    public void ResumeRegen() => _nextRegenTime = 0.0;

    /// <summary>注册一个恢复阻断源。</summary>
    /// <param name="blocker">阻断源。</param>
    public void AddBlocker(ICustomHumeShieldBlocker blocker)
    {
        if (blocker is null)
            return;

        if (_blockers.Add(blocker))
            _blockersCount++;
    }

    /// <summary>移除恢复阻断源。</summary>
    /// <param name="blocker">阻断源。</param>
    public void RemoveBlocker(ICustomHumeShieldBlocker blocker)
    {
        if (blocker is null)
            return;

        if (_blockers.Remove(blocker))
            _blockersCount--;
    }

    /// <summary>初始化：绑定统计模块、订阅受伤、铺满护盾。</summary>
    private void Initialize()
    {
        var hud = Owner.ReferenceHub;
        _shield = hud.playerStats.GetModule<HumeShieldStat>();
        _health = hud.playerStats.GetModule<HealthStat>();
        hud.playerStats.OnThisPlayerDamaged += OnDamaged;
        _subscribed = true;

        _nextRegenTime = 0.0;
        _blockersCount = 0;
        _blockers.Clear();

        ApplyMax(EvaluateMax(), force: true);
        _shield.CurValue = HsMax;
    }

    /// <summary>收口：退订、清零护盾上限与当前值。</summary>
    private void Cleanup()
    {
        if (_subscribed && Owner is not null)
        {
            var hud = Owner.ReferenceHub;
            if (hud is not null)
                hud.playerStats.OnThisPlayerDamaged -= OnDamaged;
            _subscribed = false;
        }

        if (Owner is null)
            return;

        try
        {
            Owner.HumeShield = 0f;

            // 关键：必须写「负值」解除原版覆盖锁。
            // HumeShieldStat.MaxValue 的 setter 会把 _maxValueOverride 置为 (value >= 0)：
            // 写 0 会让覆盖锁保持（true），把新角色的原生护盾上限与再生锁死为 0
            // （例如切换为 SCP 后护盾永久 0 / 无法再生）。
            // 原版 ClassChanged 解除覆盖用的正是负值（float.MinValue）。
            Owner.ReferenceHub.playerStats.GetModule<HumeShieldStat>().MaxValue = float.MinValue;
        }
        catch (Exception)
        {
            // 玩家对象已随回合销毁时无需再清零。
        }
    }

    private void Update()
    {
        if (Owner is null || HumeShieldStat is null || HumeShieldStat.IsDestroyed ||
            _shield is null || _health is null)
        {
            Destroy(this);
            return;
        }

        if (!NetworkServer.active)
            return;

        // 每帧只求一次上限：ApplyMax 与后续插值复用同一个值，避免曲线被重复 evaluate。
        var max = EvaluateMax();
        ApplyMax(max, force: false);

        var regen = HsRegeneration;
        if (regen == 0f)
            return;

        var current = _shield.CurValue;
        var delta = regen * Time.deltaTime;

        if (regen > 0f)
        {
            if (current < max)
                _shield.CurValue = Mathf.MoveTowards(current, max, delta);
        }
        else if (current > 0f)
        {
            _shield.CurValue = Mathf.Max(0f, current + delta);
        }
    }

    /// <summary>每秒恢复量；冷却中或被阻断时为 0。</summary>
    private float HsRegeneration
    {
        get
        {
            // 冷却未结束时不触碰阻断源集合，省掉一次无意义的清理遍历。
            if (_nextRegenTime >= NetworkTime.time)
                return 0f;

            return IsBlocked ? 0f : HumeShieldStat.HumeShieldRegenRate;
        }
    }

    /// <summary>受伤后进入冷却；带「无视冷却」类效果时跳过。</summary>
    /// <param name="handler">伤害信息。</param>
    private void OnDamaged(DamageHandlerBase handler)
    {
        if (!NetworkServer.active || HumeShieldStat is null)
            return;

        if (AnyActiveRegenCooldownBypass())
            return;

        _nextRegenTime = NetworkTime.time + HumeShieldStat.HumeShieldRegenDelay;
    }

    /// <summary>是否存在无视护盾恢复冷却的生效效果（如原版异常再生）。</summary>
    private bool AnyActiveRegenCooldownBypass()
    {
        var effects = Owner.ReferenceHub.playerEffectsController.AllEffects;
        for (var i = 0; i < effects.Length; i++)
        {
            var effect = effects[i];
            // 先做类型判断：绝大多数效果不是 IHumeRegenModifier，可省掉一次属性访问。
            if (effect is IHumeRegenModifier { HumeRegenCooldownEnabled: false } modifier &&
                modifier is StatusEffectBase { IsEnabled: true })
                return true;
        }

        return false;
    }

    /// <summary>计算当前护盾上限；曲线非空时以曲线为准。</summary>
    private float EvaluateMax()
    {
        var config = HumeShieldStat;
        if (config is null)
            return 0f;

        var curve = config.HumeShieldCurve;
        if (curve is null)
            return Mathf.Max(0f, config.MaxHumeShield);

        var normalized = _health is not null && _health.MaxValue > 0f
            ? Mathf.Clamp01(_health.CurValue / _health.MaxValue)
            : 0f;

        return Mathf.Max(0f, curve.Evaluate(normalized));
    }

    /// <summary>同步护盾上限到原生统计模块。</summary>
    /// <param name="force">是否强制写入。</param>
    private void ApplyMax(float target, bool force)
    {
        if (force || !Mathf.Approximately(_shield.MaxValue, target))
            _shield.MaxValue = target;
    }

    /// <summary>判断阻断源是否已失效（销毁或已解除阻断）。</summary>
    private static bool IsBlockerStale(ICustomHumeShieldBlocker blocker)
    {
        if (blocker is null)
            return true;

        // Unity 对象被销毁后接口引用仍非 null，必须走 Unity 的空判断。
        if (blocker is Object unityObject && unityObject == null)
            return true;

        return !blocker.HumeShieldBlocked;
    }

    private void OnDestroy()
    {
        if (_subscribed && Owner is not null)
        {
            var hud = Owner.ReferenceHub;
            if (hud is not null)
                hud.playerStats.OnThisPlayerDamaged -= OnDamaged;
            _subscribed = false;
        }
    }
}
