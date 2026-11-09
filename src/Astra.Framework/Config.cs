using Astra.Attributes;
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

namespace Astra;

/// <summary>
/// 晴空框架的配置文件模型（与插件目录下的 config.yml 对应）。
/// </summary>
public class Config
{
    /// <summary>
    /// 忽略补丁声明的 LabApi 主版本差异。
    /// </summary>
    /// <remarks>
    /// 默认 <see langword="false"/>：主版本不一致时拒绝登记该补丁并输出 Error。
    /// 置为 <see langword="true"/> 后改为输出 Warning 并继续登记——仅在已确认兼容性时开启。
    /// </remarks>
    /// <summary>
    /// 调试输出开关。
    /// </summary>
    /// <remarks>
    /// 为 <see langword="true"/> 时输出补丁注册/启停等详细日志；默认 <see langword="false"/> 保持控制台干净。
    /// </remarks>
    public bool IsDebug { get; set; } = false;

    public bool IgnorePatchApiMajorVersionDiff { get; set; } = false;

    /// <summary>
    /// 忽略补丁声明的游戏主版本差异。
    /// </summary>
    /// <remarks>
    /// 默认 <see langword="false"/>：主版本不一致时拒绝登记该补丁并输出 Error。
    /// 置为 <see langword="true"/> 后改为输出 Warning 并继续登记——仅在已确认兼容性时开启。
    /// </remarks>
    public bool IgnorePatchGameMajorVersionDiff { get; set; } = false;
}
