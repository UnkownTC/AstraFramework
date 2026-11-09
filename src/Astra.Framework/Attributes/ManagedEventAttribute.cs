using System;
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

namespace Astra.Attributes;
    /// <summary>
    /// 将事件处理器类纳入框架自动注册，并以指定的 Id 作为该处理器的标识。
    /// 主要用于未实现 <see cref="IIdentifiable"/> 的处理器类；
    /// 若类同时实现了 <see cref="IIdentifiable"/>，则以接口返回的 Id 优先。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ManagedEventAttribute : Attribute
    {
        /// <summary>
        /// 初始化 <see cref="ManagedEventAttribute"/> 并指定事件处理器 Id。
        /// </summary>
        /// <param name="id">事件处理器的唯一标识。</param>
        public ManagedEventAttribute(string id)
        {
            Id = id;
        }

        /// <summary>
        /// 获取事件处理器的唯一标识。
        /// </summary>
        public string Id { get; }
    }

