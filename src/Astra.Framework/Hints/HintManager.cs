using System;
using System.Collections.Generic;

using Astra.Enums;

using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Models.Arguments;
using HintServiceMeow.Core.Models.Hints;
using HintServiceMeow.Core.Utilities;
using HintServiceMeow.Core.Utilities.Parser;

using JetBrains.Annotations;

using LabApi.Features.Wrappers;

using MEC;
using Astra;
using Astra.Attributes;
using Astra.Extensions;
using Astra.Roles;
using Astra.Teams;
using Astra.Waves;
using Astra.Items;
using Astra.Effects;
using Astra.Abilities;
using Astra.HumeShield;
using Astra.Patching;
using Astra.Audio;
using Astra.Countdown;
using Astra.Cancellation;
using Astra.ServerSpecificSettings;
using Astra.Loader;

namespace Astra.Hints;
    /// <summary>
    /// 统一管理游戏中各种提示（Hint）的发送与生命周期。
    /// </summary>
    public static class HintManager
    {
        /// <summary>
        /// 发送普通静态提示。
        /// </summary>
        public static void SendTo(Player player, string message, float duration = 3f,
            HintPosition position = HintPosition.CenterTop, int fontSize = 20, string messageId = null)
        {
            var (alignment, y) = HintPositionMapping.GetForHint(position);
            var hint = new HintServiceMeow.Core.Models.Hints.Hint
            {
                Id = messageId,
                Text = message,
                FontSize = fontSize,
                Alignment = alignment,
                YCoordinate = y
            };
            SendAbstractHint(player, hint, duration);
        }

        /// <summary>
        /// 发送动态提示（DynamicHint）。
        /// </summary>
        public static void SendDynamic(Player player, string message, float duration = 3f,
            HintPosition position = HintPosition.CenterTop, int fontSize = 20, [CanBeNull] string messageId = null)
        {
            var hint = new DynamicHint
            {
                Id = messageId,
                Text = message,
                FontSize = fontSize,
            };
            PositionDynamicHint(player, hint, position);
            SendAbstractHint(player, hint, duration);
        }

        /// <summary>
        /// 发送动态提示（DynamicHint）。
        /// </summary>
        public static void SendDynamic(Player player, Func<string> textGenerator, float duration = 3f,
            HintPosition position = HintPosition.CenterTop, int fontSize = 20, [CanBeNull] string messageId = null)
        {
            SendDynamic(player, _ => textGenerator(), duration, position, fontSize, messageId);
        }

        /// <summary>
        /// 发送动态提示（DynamicHint）。
        /// </summary>
        public static void SendDynamic(Player player, Func<AutoContentUpdateArg, string> textGenerator, float duration = 3f,
            HintPosition position = HintPosition.CenterTop, int fontSize = 20, [CanBeNull] string messageId = null)
        {
            var hint = new DynamicHint
            {
                Id = messageId,
                AutoText = ev => textGenerator(ev),
                FontSize = fontSize,
            };
            PositionDynamicHint(player, hint, position);
            SendAbstractHint(player, hint, duration);
        }

        /// <summary>
        /// 发送自动文本提示（AutoText），可根据委托动态更新文本。
        /// </summary>
        public static void SendTo(Player player, Func<string> textGenerator, float duration = 3f,
            HintPosition position = HintPosition.CenterTop, int fontSize = 20,
            string messageId = null, HintSyncSpeed syncSpeed = HintSyncSpeed.Normal)
        {
            var (alignment, y) = HintPositionMapping.GetForHint(position);
            var hint = new HintServiceMeow.Core.Models.Hints.Hint
            {
                Id = messageId,
                AutoText = _ => textGenerator(),
                FontSize = fontSize,
                Alignment = alignment,
                YCoordinate = y,
                SyncSpeed = syncSpeed
            };
            SendAbstractHint(player, hint, duration);
        }

        /// <summary>
        /// 发送自动文本提示（AutoText），可根据委托动态更新文本。
        /// </summary>
        public static void SendTo(Player player, Func<AutoContentUpdateArg, string> textGenerator, float duration = 3f,
            HintPosition position = HintPosition.CenterTop, int fontSize = 20,
            string messageId = null, HintSyncSpeed syncSpeed = HintSyncSpeed.Normal)
        {
            var (alignment, y) = HintPositionMapping.GetForHint(position);
            var hint = new HintServiceMeow.Core.Models.Hints.Hint
            {
                Id = messageId,
                AutoText = ev => textGenerator(ev),
                FontSize = fontSize,
                Alignment = alignment,
                YCoordinate = y,
                SyncSpeed = syncSpeed
            };
            SendAbstractHint(player, hint, duration);
        }

        /// <summary>
        /// 核心发送逻辑：移除重复提示，添加新提示，并在 duration 后自动移除。
        /// </summary>
        public static void SendAbstractHint(Player player, AbstractHint hint, float duration)
        {
            var display = PlayerDisplay.Get(player);

            // 移除重复提示：优先按 Id 去重，若 Id 为空则按文本去重
            // 仅在确实存在重复项时才分配临时列表，避免每次调用都产生 List 与闭包分配。
            List<AbstractHint> duplicates = null;
            foreach (var existing in display.GetHints())
            {
                if (!ShouldReplace(hint, existing))
                    continue;
                (duplicates ??= new List<AbstractHint>()).Add(existing);
            }

            if (duplicates is not null)
            {
                foreach (var dup in duplicates)
                    display.RemoveHint(dup);
            }

            display.AddHint(hint);

            // 正数 duration 才自动移除，<=0 表示永久显示
            if (duration > 0)
            {
                Timing.CallDelayed(duration, () => display.RemoveHint(hint));
            }
        }

        /// <summary>
        /// 移除指定玩家身上由本程序集发送的、带指定标识的提示。
        /// </summary>
        /// <remarks>
        /// 必须留在本类（而非调用方程序集）中实现：HintServiceMeow 按
        /// <c>Assembly.GetCallingAssembly()</c> 给提示分组，只有与 <see cref="SendAbstractHint"/>
        /// 同程序集调用，移除才会命中同一个分组。
        /// </remarks>
        /// <param name="player">目标玩家。</param>
        /// <param name="messageId">发送时使用的提示标识。</param>
        public static void RemoveHint(Player player, string messageId)
        {
            if (player is null || string.IsNullOrEmpty(messageId))
                return;

            PlayerDisplay.Get(player).RemoveHint(messageId);
        }

        /// <summary>
        /// 按位置与实际文本宽度计算动态提示的 TargetX / TargetY。
        /// </summary>
        private static void PositionDynamicHint(Player player, DynamicHint hint, HintPosition position)
        {
            var hintParser = PlayerDisplay.Get(player).HintParser as HintParser;
            var textWidth = hintParser!.coordinateTool.GetTextWidth(hint);
            var (x, y) = HintPositionMapping.GetForDynamicHint(position, 0, textWidth);
            hint.TargetX = x;
            hint.TargetY = y;
        }

        /// <summary>
        /// 判断 newHint 是否应替换 existingHint。
        /// 规则：若新提示有 Id，则替换所有相同 Id 的提示；否则替换所有相同文本的提示。
        /// </summary>
        /// <remarks>
        /// 按文本去重时必须排除双方文本为空/为 <see langword="null"/> 的情形：
        /// HintServiceMeow 中 <c>AutoText</c>（<c>AutoContent</c>）型提示的
        /// <see cref="AbstractHint.Text"/> <b>恒为 null</b>（只有 <c>StringContent</c> 才返回文本），
        /// 若直接比较，则所有「无 Id + AutoText」的提示会因 <c>null == null</c> 互相判重并被误删。
        /// 典型场景：定位技能的无 Id 提示会把同分组内全部 AutoText 型 UI 一起清掉。
        /// </remarks>
        private static bool ShouldReplace(AbstractHint newHint, AbstractHint existingHint)
        {
            if (!string.IsNullOrEmpty(newHint.Id))
            {
                return newHint.Id == existingHint.Id;
            }

            // 无 Id 时按文本去重，但必须要求双方文本均非空，否则 AutoText 型提示会互相误删。
            return !string.IsNullOrEmpty(newHint.Text)
                && newHint.Text == existingHint.Text;
        }
    }

