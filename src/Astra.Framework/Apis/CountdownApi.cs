using System;

using Astra.Systems.CountdownSystem;

namespace Astra.Apis
{
    /// <summary>
    /// 倒计时系统对外静态 API，内部委托给 <see cref="CountdownManager"/> 单例。
    /// </summary>
    public static class CountdownApi
    {
        /// <summary>
        /// 启动一个倒计时。
        /// </summary>
        /// <param name="delay">延迟秒数。</param>
        /// <param name="action">到期时执行的回调。</param>
        /// <param name="tickAction">倒计时期间每秒执行一次的 tick 回调，可为空。</param>
        /// <param name="repeat">返回 true 则到期后按原 delay 重新入队。</param>
        /// <returns>计时器唯一标识，可交给 <see cref="CountdownManager.RemoveTimer"/> 取消。</returns>
        public static int Start(float delay, Action action, Action tickAction = null, Func<bool> repeat = null)
        {
            return CountdownManager.Instance.AddTimer(delay, action, tickAction, repeat);
        }

        /// <summary>
        /// 取消一个尚未到期的倒计时；已到期或被取消的标识调用无副作用。
        /// 供需要在效果中途提前收口的调用方使用（如锚点 / 迷雾区被提前销毁）。
        /// </summary>
        /// <param name="uuid"><see cref="Start"/> 返回的计时器标识。</param>
        public static void Cancel(int uuid)
        {
            CountdownManager.Instance.RemoveTimer(uuid);
        }
    }
}
