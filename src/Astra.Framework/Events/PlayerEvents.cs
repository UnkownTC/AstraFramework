using Astra.Events.EventArgs;

using LabApi.Events;

namespace Astra.Events
{
    /// <summary>
    /// 框架扩展的玩家事件集合（射击相关）。
    /// 事件由 IL 织入的补丁触发，用法与 LabAPI 静态事件一致。
    /// </summary>
    public static class PlayerEvents
    {
        /// <summary>玩家开火前触发，可通过 <see cref="ShootingEventArgs.IsAllowed"/> 取消。</summary>
        public static event LabEventHandler<ShootingEventArgs> Shooting;

        /// <summary>子弹命中后触发，可通过 <see cref="ShotEventArgs.CanHurt"/> 等属性调整结果。</summary>
        public static event LabEventHandler<ShotEventArgs> Shot;

        /// <summary>触发 <see cref="Shooting"/> 事件（框架内部调用）。</summary>
        public static void OnShooting(ShootingEventArgs ev)
        {
            Shooting.InvokeEvent(ev);
        }

        /// <summary>触发 <see cref="Shot"/> 事件（框架内部调用）。</summary>
        public static void OnShot(ShotEventArgs ev)
        {
            Shot.InvokeEvent(ev);
        }
    }
}
