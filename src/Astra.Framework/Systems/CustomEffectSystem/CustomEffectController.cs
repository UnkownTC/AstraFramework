using System;
using System.Collections.Generic;

using Astra.Abstractions;

using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;
using LabApi.Features.Wrappers;


using UnityEngine;

namespace Astra.Systems.CustomEffectSystem
{
    /// <summary>
    /// 承载玩家级"纯托管"自定义效果（<see cref="CustomEffect"/> 家族）的组件。
    /// 效果不进入 <c>PlayerEffectsController.AllEffects</c>，因此不改变原版按索引对齐的强度同步契约。
    /// </summary>
    public class CustomEffectController : MonoBehaviour
    {
        /// <summary>效果宿主玩家；未 <see cref="Bind"/> 前为 <see langword="null"/>。</summary>
        public Player? Owner { get; private set; }

        private readonly List<CustomEffect> _effects = [];

        /// <summary>
        /// 绑定宿主玩家并重建默认效果集合。由 <c>ComponentApplyPatch</c> 在 <c>ReferenceHub.Awake</c> 之后调用。
        /// </summary>
        /// <param name="player">宿主玩家。</param>
        public void Bind(Player player)
        {
            if (player is null)
                return;

            // 重复绑定（例如重新挂载）时先走一遍销毁钩子，避免旧效果持有的事件/资源泄漏。
            if (_effects.Count > 0)
            {
                foreach (var effect in _effects)
                    effect?.OnDestroy();

                _effects.Clear();
            }

            Owner = player;
        }

        /// <summary>向控制器追加一个效果实例（已存在则忽略）。</summary>
        /// <param name="effect">效果实例。</param>
        public void AddEffect(CustomEffect effect)
        {
            if (effect is null || _effects.Contains(effect))
                return;

            _effects.Add(effect);
        }

        /// <summary>追加效果实例并返回它，便于链式使用。</summary>
        /// <typeparam name="T">效果类型。</typeparam>
        /// <param name="effect">效果实例。</param>
        /// <returns>传入的实例。</returns>
        public T AddEffect<T>(T effect) where T : CustomEffect
        {
            AddEffect((CustomEffect)effect);
            return effect;
        }

        /// <summary>按类型取得效果实例（供统一包装类使用）。</summary>
        /// <param name="effectType">效果类型。</param>
        /// <returns>效果实例；不存在时为 <see langword="null"/>。</returns>
        public CustomEffect? GetEffect(Type effectType)
        {
            if (effectType is null)
                return null;

            for (var i = 0; i < _effects.Count; i++)
            {
                if (_effects[i].GetType() == effectType)
                    return _effects[i];
            }

            return null;
        }

        /// <summary>按类型停用效果实例，实例保留以便复用。</summary>
        /// <param name="effectType">效果类型。</param>
        public void DisableEffect(Type effectType) => GetEffect(effectType)?.DisableEffect();

        /// <summary>取得指定类型的效果实例，不存在时用工厂创建并挂载。</summary>
        /// <typeparam name="T">效果类型。</typeparam>
        /// <param name="factory">创建工厂，仅在不存在时调用。</param>
        /// <returns>效果实例；宿主未绑定时为 <see langword="null"/>。</returns>
        public T? GetOrAddEffect<T>(Func<Player, T> factory) where T : CustomEffect
        {
            if (TryGetEffect<T>(out var existing))
                return existing;

            if (Owner is null || factory is null)
                return null;

            return AddEffect(factory(Owner));
        }

        /// <summary>取得指定类型的首个效果实例。</summary>
        /// <typeparam name="T">效果类型。</typeparam>
        /// <returns>存在的效果实例；不存在时为 <see langword="null"/>。</returns>
        public T? GetEffect<T>() where T : CustomEffect
        {
            for (var i = 0; i < _effects.Count; i++)
            {
                if (_effects[i] is T match)
                    return match;
            }

            return null;
        }

        /// <summary>尝试取得指定类型的首个效果实例。</summary>
        /// <typeparam name="T">效果类型。</typeparam>
        /// <param name="effect">取到的效果实例。</param>
        /// <returns>是否取到。</returns>
        public bool TryGetEffect<T>(out T? effect) where T : CustomEffect
        {
            effect = GetEffect<T>();
            return effect is not null;
        }

        /// <summary>修改指定类型效果的强度与时长。</summary>
        /// <typeparam name="T">效果类型。</typeparam>
        /// <param name="intensity">新强度。</param>
        /// <param name="duration">时长（秒）。</param>
        /// <param name="addDuration">为 <see langword="true"/> 时在剩余时间上追加。</param>
        /// <returns>被修改的效果实例；不存在时为 <see langword="null"/>。</returns>
        public T? ChangeState<T>(byte intensity, float duration = 0f, bool addDuration = false) where T : CustomEffect
        {
            var effect = GetEffect<T>();
            effect?.SetState(intensity, duration, addDuration);
            return effect;
        }

        /// <summary>关闭全部效果（含视觉载体清理）。</summary>
        public void DisableAllEffects()
        {
            foreach (var effect in _effects)
                effect?.DisableEffect();
        }

        private void Update()
        {
            foreach (var effect in _effects)
                effect?.Update();
        }

        private void Awake()
        {
            PlayerEvents.Death += OnPlayerDeath;
            PlayerEvents.ChangedRole += OnPlayerChangedRole;
        }

        private void OnDisable()
        {
            PlayerEvents.Death -= OnPlayerDeath;
            PlayerEvents.ChangedRole -= OnPlayerChangedRole;
            DisableAllEffects();
        }

        /// <summary>释放全部效果并解除事件订阅（可重复调用）。</summary>
        public void Cleanup()
        {
            PlayerEvents.Death -= OnPlayerDeath;
            PlayerEvents.ChangedRole -= OnPlayerChangedRole;

            foreach (var effect in _effects)
                effect?.OnDestroy();

            _effects.Clear();
            Owner = null;
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        private void OnPlayerDeath(PlayerDeathEventArgs ev)
        {
            if (ev.Player != Owner)
                return;

            foreach (var effect in _effects)
                effect?.OnOwnerDeath();
        }

        private void OnPlayerChangedRole(PlayerChangedRoleEventArgs ev)
        {
            if (ev.Player != Owner)
                return;

            foreach (var effect in _effects)
                effect?.OnOwnerRoleChanged();
        }
    }
}
