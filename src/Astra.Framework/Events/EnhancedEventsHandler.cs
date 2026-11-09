using Astra.Attributes;

using LabApi.Events.CustomHandlers;

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

