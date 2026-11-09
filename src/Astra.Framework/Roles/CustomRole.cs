using System;

using Astra.Teams;

using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.CustomHandlers;
using LabApi.Features.Wrappers;

using PlayerRoles;
using PlayerRoles.FirstPersonControl;

using Respawning.Objectives;

using UnityEngine;

using Logger = LabApi.Features.Console.Logger;

namespace Astra.Roles;
    public abstract class CustomRole : CustomEventsHandler, ICustomContent, IOriginalRole
    {
        /// <summary>套用角色前的血量上限，用于收口时回滚。</summary>
        private float _originalMaxHealth;

        public abstract string Id { get; }
        public abstract string Name { get; }
        public virtual string Description { get; set; }
        /// <summary>
        /// 该实例绑定的玩家。
        /// </summary>
        /// <remarks>
        /// <b>仅玩家专属实例有值，模板实例恒为 <see langword="null"/></b>。
        /// 判断「某玩家是否正持有本角色」请用 <see cref="Check"/>，不要比对本属性——
        /// 事件回调通常运行在模板上，此处为 null。
        /// </remarks>
        public Player? Owner { get; private set; }
        public virtual RoleTypeId RoleType { get; set; } = RoleTypeId.None;
        public virtual RoleSpawnFlags SpawnFlags { get; } = RoleSpawnFlags.All;
        public virtual RoleChangeReason ChangeReason { get; } = RoleChangeReason.Respawn;
        public virtual Vector3 Scale { get; } = Vector3.one;
        public virtual Vector3 Gravity { get; } = FpcGravityController.DefaultGravity;
        public virtual bool ShowCustomInfo { get; set; } = false;
        public virtual float MaxHealth { get; set; } = 100;
        public virtual ItemType[] Items { get; set; } = [];
        public virtual bool ClearInventory { get; set; } = true;
        public bool IsDestroyed { get; private set; } = false;

        /// <summary>是否正在执行 <see cref="AddRole"/>，用于屏蔽初始化期间的角色变更回调。</summary>
        internal bool IsApplying { get; private set; }

        /// <summary>角色配色，由 <see cref="RoleType"/> 推导，只读。</summary>
        public virtual string RoleColor => '#' + ColorUtility.ToHtmlStringRGBA(RoleType.GetRoleColor());

        /// <summary>
        /// 角色所属的自定义阵营；为 <see langword="null"/> 时沿用原版阵营关系。
        /// </summary>
        /// <remarks>
        /// 仅影响敌我判定（伤害是否走友伤系统），不改变原版 <see cref="Team"/> 的显示与计分。
        /// </remarks>
        public virtual CustomTeam? CustomTeam => null;

        /// <summary>角色所属阵营，由 <see cref="RoleType"/> 推导，只读。</summary>
        public virtual Team Team => RoleType.GetTeam();

        /// <summary>判断指定玩家当前是否持有本角色（按 <see cref="Id"/> 比对）。</summary>
        /// <param name="player">待判断的玩家，可为 <see langword="null"/>。</param>
        /// <returns>该玩家正持有同一角色 Id 时为 <see langword="true"/>。</returns>
        /// <remarks>
        /// 语义是「<b>该玩家当前持有的自定义角色与本实例同类</b>」，而不是「本实例属于该玩家」。
        /// 因此模板实例同样可以返回 <see langword="true"/>，可直接在事件回调里作为守卫：
        /// <code>if (!Check(ev.Player)) return;</code>
        /// </remarks>
        public bool Check(Player player)
        {
            if (player is null)
                return false;

            return CustomRoleManager.TryGetCustomRole(player, out var role) &&
                   string.Equals(role.Id, Id, StringComparison.Ordinal);
        }

        internal virtual void AddRole(Player player)
        {
            if (player is null)
                return;

            IsApplying = true;
            try
            {
                Owner = player;
                if (RoleType is not RoleTypeId.None)
                    player.SetRole(RoleType, ChangeReason, SpawnFlags);

                // 先记录原版角色自带的上限，收口时才有依据回滚。
                _originalMaxHealth = player.MaxHealth;

                player.CustomInfo = ShowCustomInfo ? Name : string.Empty;
                player.MaxHealth = MaxHealth;
                player.Health = MaxHealth;
                if (ClearInventory)
                {
                    player.ClearInventory();
                }
                foreach (var item in Items)
                {
                    player.AddItem(item);
                }
                player.Scale = Scale;
                player.Gravity = Gravity;
                OnAdded(player);
            }
            catch (Exception ex)
            {
                // 半初始化状态比直接失败更危险，这里回滚成「已销毁」交给上层清理。
                Logger.Error($"[CustomRole] 角色 {Id} 套用到玩家时失败，已回滚：{ex.Message}");
                Owner = null;
                IsDestroyed = true;
            }
            finally
            {
                IsApplying = false;
            }
        }

        protected virtual void OnAdded(Player player)
        {
        }

        internal virtual void RemoveRole(Player player)
        {
            if (Check(player))
                return;

            if (IsDestroyed)
                return;

            Owner = null;
            IsDestroyed = true;

            if (player is null)
                return;

            player.CustomInfo = string.Empty;
            player.Scale = Vector3.one;
            player.Gravity = FpcGravityController.DefaultGravity;

            // 仅当玩家仍停留在该自定义角色对应的原版角色上时才回滚血量上限，
            // 否则会覆盖新角色自己的血量设置。
            if (RoleType is not RoleTypeId.None && player.Role == RoleType && _originalMaxHealth > 0f)
                player.MaxHealth = _originalMaxHealth;

            OnRemoved(player);
        }

        protected virtual void OnRemoved(Player player)
        {
        }

        /// <summary>
        /// 角色变更前收口。
        /// </summary>
        /// <remarks>
        /// 角色实例是按玩家创建的、不会被注册为事件处理器，因此本方法在玩家实例上不会触发；
        /// 实际清理由 <c>CustomRoleManager.OnPlayerChangedRole</c> 承担。
        /// 保留此实现仅用于「实例被手动注册」的场景。
        /// </remarks>
        public override void OnPlayerChangingRole(PlayerChangingRoleEventArgs ev)
        {
            if (ev?.Player is null)
                return;

            // 初始化期间的角色变更由 AddRole 自行负责，避免自己把自己移除。
            if (IsApplying)
                return;

            if (Check(ev.Player))
            {
                RemoveRole(ev.Player);
            }
        }

        public virtual CustomRole CreateInstance() => (CustomRole)Activator.CreateInstance(GetType());
    }

