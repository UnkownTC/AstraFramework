using System.Runtime.InteropServices;

using Astra.Attributes;
using Astra.Events;

using LabApi.Events.CustomHandlers;
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

namespace Astra.Events;
    /// <summary>
    /// 增强事件管理器：继承 <see cref="CustomEventsHandler"/> 并额外提供框架扩展的射击事件虚方法。
    /// 继承该类并重写相应方法即可自动接收射击事件。
    /// </summary>
    public abstract class EnhancedEventsHandler : CustomEventsHandler
    {
        public EnhancedEventsHandler()
        {
            EventMethodManager.Register(this);
        }

        /// <summary>玩家开火前触发。</summary>
        [EventMethod(typeof(PlayerEvents), nameof(PlayerEvents.Shooting))]
        public virtual void OnExiledShooting(ShootingEventArgs ev)
        {
        }

        /// <summary>子弹命中后触发。</summary>
        [EventMethod(typeof(PlayerEvents), nameof(PlayerEvents.Shot))]
        public virtual void OnExiledShot(ShotEventArgs ev)
        {
        }
    }

