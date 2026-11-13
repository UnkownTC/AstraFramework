namespace Astra.Enums;

/// <summary>
/// 提示（Hint）在屏幕上的显示位置。
/// </summary>
/// <remarks>
/// <para>
/// 作为 <c>HintApi.SendTo</c> / <c>HintApi.SendDynamic</c> 等方法的 <c>position</c> 参数使用，
/// 静态提示与动态提示共用同一套取值；框架默认位置是 <see cref="CenterTop"/>。
/// </para>
/// <para>
/// 具体坐标由 <c>HintPositionMapping</c> 映射到 HintServiceMeow 的屏幕坐标系（Y 越大越靠下）：
/// <see cref="Top"/> = 20、<see cref="CenterTop"/> = 220、<see cref="Left" /> / <see cref="Right"/> = 530、
/// <see cref="Center"/> = 650、<see cref="CenterBottom"/> = 860、<see cref="Bottom"/> = 1060；
/// 左/右对齐的位置在动态提示中会按文本宽度自动计算 X 偏移。
/// </para>
/// <para>
/// <see cref="Custom"/> 当前没有内置坐标，映射时会回退为居中（650）；需要自由定位时请自行计算坐标并直接使用
/// HintServiceMeow 的 API。
/// </para>
/// <para>
/// 用法示例：
/// <code>
/// HintApi.SendTo(player, "已到达检查点", position: HintPosition.CenterTop);
/// HintApi.SendDynamic(player, () =&gt; $"剩余 {seconds} 秒", position: HintPosition.Bottom);
/// </code>
/// </para>
/// </remarks>
public enum HintPosition : byte
{
    /// <summary>屏幕中央（垂直居中区域）；适合醒目的单条通知。</summary>
    Center,

    /// <summary>屏幕左侧、垂直居中区域；适合侧边状态栏。</summary>
    Left,

    /// <summary>屏幕右侧、垂直居中区域；适合侧边状态栏。</summary>
    Right,

    /// <summary>中上部；框架默认位置，适合标题类提示。</summary>
    CenterTop,

    /// <summary>中上偏左；适合左上区域的状态信息。</summary>
    CenterTopLeft,

    /// <summary>中上偏右；适合右上区域的状态信息。</summary>
    CenterTopRight,

    /// <summary>中下部；适合副标题或补充说明。</summary>
    CenterBottom,

    /// <summary>中下偏左。</summary>
    CenterBottomLeft,

    /// <summary>中下偏右。</summary>
    CenterBottomRight,

    /// <summary>顶部（贴屏幕上沿）；适合常驻状态栏。</summary>
    Top,

    /// <summary>顶部偏左。</summary>
    TopLeft,

    /// <summary>顶部偏右。</summary>
    TopRight,

    /// <summary>底部（贴屏幕下沿）；适合操作提示与倒计时。</summary>
    Bottom,

    /// <summary>底部偏左。</summary>
    BottomLeft,

    /// <summary>底部偏右。</summary>
    BottomRight,

    /// <summary>
    /// 自定义位置占位值；当前映射回退到 <see cref="Center"/> 的坐标（650），不会自动生效。
    /// </summary>
    Custom
}
