using System;

using Astra.Enums;
using Astra.Systems.HintSystem;

using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Models.Arguments;
using HintServiceMeow.Core.Models.Hints;

using LabApi.Features.Wrappers;

namespace Astra.Apis
{
    /// <summary>
    /// 对外暴露的静态提示 API，内部委托给 HintManager 单例。
    /// </summary>
    public static class HintApi
    {
        /// <summary>
        /// 向指定玩家发送普通静态提示。
        /// </summary>
        public static void SendTo(Player player, string message, float duration = 3f,
            HintPosition position = HintPosition.CenterTop, int fontSize = 20, string messageId = null)
        {
            HintManager.SendTo(player, message, duration, position, fontSize, messageId);
        }

        /// <summary>
        /// 向指定玩家发送动态提示。
        /// </summary>
        public static void SendDynamic(Player player, string message, float duration = 3f,
            HintPosition position = HintPosition.CenterTop, int fontSize = 20, string messageId = null)
        {
            HintManager.SendDynamic(player, message, duration, position, fontSize, messageId);
        }
        /// <summary>
        /// 向指定玩家发送动态提示。
        /// </summary>
        public static void SendDynamic(Player player, Func<string> textGenerator, float duration = 3f,
            HintPosition position = HintPosition.CenterTop, int fontSize = 20, string messageId = null)
        {
            HintManager.SendDynamic(player, textGenerator, duration, position, fontSize, messageId);
        }
        /// <summary>
        /// 向指定玩家发送动态提示。
        /// </summary>
        public static void SendDynamic(Player player, Func<AutoContentUpdateArg, string> textGenerator, float duration = 3f,
            HintPosition position = HintPosition.CenterTop, int fontSize = 20, string messageId = null)
        {
            HintManager.SendDynamic(player, textGenerator, duration, position, fontSize, messageId);
        }

        /// <summary>
        /// 向指定玩家发送自动文本提示（支持动态更新文本）。
        /// </summary>
        public static void SendTo(Player player, Func<string> textGenerator, float duration = 3f,
            HintPosition position = HintPosition.CenterTop, int fontSize = 20,
            string messageId = null, HintSyncSpeed syncSpeed = HintSyncSpeed.Normal)
        {
            HintManager.SendTo(player, textGenerator, duration, position, fontSize, messageId, syncSpeed);
        }
        /// <summary>
        /// 向指定玩家发送自动文本提示（支持动态更新文本）。
        /// </summary>
        public static void SendTo(Player player, Func<AutoContentUpdateArg, string> textGenerator, float duration = 3f,
            HintPosition position = HintPosition.CenterTop, int fontSize = 20,
            string messageId = null, HintSyncSpeed syncSpeed = HintSyncSpeed.Normal)
        {
            HintManager.SendTo(player, textGenerator, duration, position, fontSize, messageId, syncSpeed);
        }

        /// <summary>
        /// 向指定玩家直接发送Hint类。
        /// </summary>
        public static void SendAbstractHint(Player player, AbstractHint hint, float duration)
        {
            HintManager.SendAbstractHint(player, hint, duration);
        }

        /// <summary>
        /// 移除指定玩家身上、由本框架发送的、带指定标识的提示。
        /// </summary>
        /// <param name="player">目标玩家。</param>
        /// <param name="messageId">发送时使用的提示标识（<c>messageId</c>）。</param>
        public static void RemoveHint(Player player, string messageId)
        {
            HintManager.RemoveHint(player, messageId);
        }
    }
}
