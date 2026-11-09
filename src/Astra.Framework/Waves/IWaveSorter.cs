using Astra;
using Astra.Attributes;
using Astra.Enums;
using Astra.Extensions;
using Astra.Roles;
using Astra.Teams;
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

namespace Astra.Waves;

/// <summary>
/// 波次排序器：决定候选波次的优先级顺序。
/// </summary>
/// <remarks>
/// 框架只约定「比较方向」，不关心比较依据是权重、威胁等级还是别的业务概念，
/// 从而让通用刷新逻辑与具体游戏的威胁等级解耦。
/// </remarks>
/// <typeparam name="T">被排序的波次类型。</typeparam>
public interface IWaveSorter<in T>
{
    /// <summary>比较两个波次的优先级。</summary>
    /// <param name="left">左侧波次。</param>
    /// <param name="right">右侧波次。</param>
    /// <returns>负数表示 <paramref name="left"/> 优先；正数表示 <paramref name="right"/> 优先；0 表示同等。</returns>
    int Compare(T left, T right);
}
