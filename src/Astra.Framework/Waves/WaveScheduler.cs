using System;
using System.Collections.Generic;
using System.Linq;

using LabApi.Features.Wrappers;

namespace Astra.Waves;

/// <summary>
/// 加权波次调度器：只决定「顺序 + 是否跳过」，实际生成仍由 <see cref="CustomWave.Spawn"/> 执行。
/// </summary>
/// <remarks>
/// <see cref="Load"/> 会覆盖式装载并按 <see cref="WeightedWaveAdapter"/> 排序；
/// <see cref="Advance"/> 逐个推进，人数不满足门槛的波次被跳过（游标前进，不阻断后续波次）。
/// </remarks>
public sealed class WaveScheduler
{
    private readonly Func<int>? _playerCountProvider;
    private readonly List<CustomWave> _queue = [];
    private int _index;

    /// <summary>构造调度器。</summary>
    /// <param name="playerCountProvider">人数提供器；缺省取 <c>Player.ReadyList.Count()</c>。</param>
    public WaveScheduler(Func<int>? playerCountProvider = null)
    {
        _playerCountProvider = playerCountProvider;
    }

    /// <summary>当前波次；<see cref="Advance"/> 成功推进后更新，队列耗尽时为 <see langword="null"/>。</summary>
    public CustomWave? Current { get; private set; }

    /// <summary>尚未推进的波次数量。</summary>
    public int Remaining => Math.Max(0, _queue.Count - _index);

    /// <summary>内置支援波次池。</summary>
    public SupportWavePool Support { get; } = new();

    /// <summary>从支援池抽取一个波次，并记录为当前波次。</summary>
    /// <param name="playerCount">当前人数。</param>
    /// <returns>抽中的波次；没有满足门槛的项时为 <see langword="null"/>。</returns>
    public CustomWave? PickSupportWave(int playerCount)
    {
        var wave = Support.Pick(playerCount);
        if (wave is not null)
            Current = wave;

        return wave;
    }

    /// <summary>覆盖式装载波次，按权重排序并重置游标。</summary>
    /// <param name="waves">候选波次。</param>
    /// <returns>装载后的队列长度。</returns>
    public int Load(IEnumerable<CustomWave> waves)
    {
        _queue.Clear();
        _index = 0;
        Current = null;

        if (waves is not null)
        {
            foreach (var wave in WeightedWaveAdapter.Instance.Sort(waves))
            {
                if (wave is not null)
                    _queue.Add(wave);
            }
        }

        return _queue.Count;
    }

    /// <summary>推进到下一个满足人数门槛的波次。</summary>
    /// <param name="playerCount">人数；缺省使用构造时的人数提供器。</param>
    /// <returns>满足门槛的波次；队列耗尽时为 <see langword="null"/>。</returns>
    public CustomWave? Advance(int? playerCount = null)
    {
        var count = playerCount ?? GetPlayerCount();

        while (_index < _queue.Count)
        {
            var wave = _queue[_index++];
            if (!MeetsThreshold(wave, count))
                continue;

            Current = wave;
            return wave;
        }

        Current = null;
        return null;
    }

    /// <summary>清空队列与当前波次。</summary>
    public void Reset()
    {
        _queue.Clear();
        _index = 0;
        Current = null;
    }

    private int GetPlayerCount() => _playerCountProvider?.Invoke() ?? Player.ReadyList.Count();

    private static bool MeetsThreshold(CustomWave wave, int playerCount)
    {
        if (wave is not IWeightedWave weighted)
            return true;

        var meetsMin = weighted.MinPlayers <= 0 || playerCount >= weighted.MinPlayers;
        var meetsMax = weighted.MaxPlayers < 0 || playerCount <= weighted.MaxPlayers;
        return meetsMin && meetsMax;
    }
}
