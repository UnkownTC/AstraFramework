using System;
using System.Collections.Generic;
using System.Linq;

using Random = UnityEngine.Random;

namespace Astra.Waves;

/// <summary>
/// 支援波次池：按权重轮换支援波次，并在人数门槛不满足时提供降级序列。
/// </summary>
/// <remarks>
/// 只负责「登记 / 权重 / 取样 / 降级」，实际生成仍由调用方拿 <see cref="Pick"/> 或
/// <see cref="GetWithFallback"/> 的结果交给 <see cref="CustomWave.Spawn"/>。
/// </remarks>
public sealed class SupportWavePool
{
    private sealed class Entry
    {
        public CustomWave Wave = null!;
        public int Weight;
    }

    private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<CustomWave> _waves = [];

    /// <summary>波次完成生成后触发；由调用方在生成完成后调用 <see cref="NotifySpawned"/>。</summary>
    public event Action<CustomWave>? WaveSpawned;

    /// <summary>池内全部波次。</summary>
    public IReadOnlyCollection<CustomWave> Waves => _waves;

    /// <summary>登记支援波次；实现 <see cref="IWeightedWave"/> 时权重取其声明值，否则取传入值。</summary>
    /// <param name="wave">支援波次。</param>
    /// <param name="weight">默认权重。</param>
    public void Register(CustomWave wave, int weight)
    {
        if (wave is null || string.IsNullOrWhiteSpace(wave.Id))
            return;

        if (_entries.TryGetValue(wave.Id, out var existing) && !ReferenceEquals(existing.Wave, wave))
            _waves.Remove(existing.Wave);

        _entries[wave.Id] = new Entry
        {
            Wave = wave,
            Weight = wave is IWeightedWave weighted ? weighted.Weight : weight,
        };

        if (!_waves.Contains(wave))
            _waves.Add(wave);
    }

    /// <summary>调整指定波次的权重。</summary>
    /// <param name="waveId">波次 Id。</param>
    /// <param name="weight">新权重。</param>
    /// <returns>Id 存在并更新时返回 <see langword="true"/>。</returns>
    public bool SetWeight(string waveId, int weight)
    {
        if (string.IsNullOrWhiteSpace(waveId) || !_entries.TryGetValue(waveId, out var entry))
            return false;

        entry.Weight = weight;
        return true;
    }

    /// <summary>按权重随机抽取一个满足人数门槛的支援波次。</summary>
    /// <param name="playerCount">当前人数。</param>
    /// <returns>抽中的波次；没有满足门槛的项时为 <see langword="null"/>。</returns>
    public CustomWave? Pick(int playerCount)
    {
        var candidates = _entries.Values.Where(entry => MeetsThreshold(entry.Wave, playerCount)).ToList();
        if (candidates.Count == 0)
            return null;

        var total = candidates.Sum(entry => Math.Max(0, entry.Weight));
        if (total <= 0)
            return candidates[Random.Range(0, candidates.Count)].Wave;

        var roll = Random.Range(0, total);
        foreach (var entry in candidates)
        {
            roll -= Math.Max(0, entry.Weight);
            if (roll < 0)
                return entry.Wave;
        }

        return candidates[candidates.Count - 1].Wave;
    }

    /// <summary>取得满足门槛的支援波次（权重降序）；全部不满足时返回全部候选作为降级序列。</summary>
    /// <param name="playerCount">当前人数。</param>
    /// <returns>非空降级序列；池为空时为空列表。</returns>
    public IReadOnlyList<CustomWave> GetWithFallback(int playerCount)
    {
        var eligible = _entries.Values
            .Where(entry => MeetsThreshold(entry.Wave, playerCount))
            .OrderByDescending(entry => entry.Weight)
            .ThenBy(entry => entry.Wave.Id, StringComparer.OrdinalIgnoreCase)
            .Select(entry => entry.Wave)
            .ToList();

        if (eligible.Count > 0)
            return eligible;

        return _entries.Values
            .OrderByDescending(entry => entry.Weight)
            .ThenBy(entry => entry.Wave.Id, StringComparer.OrdinalIgnoreCase)
            .Select(entry => entry.Wave)
            .ToList();
    }

    /// <summary>在波次生成完成后调用，触发 <see cref="WaveSpawned"/>。</summary>
    /// <param name="wave">已完成生成的波次。</param>
    public void NotifySpawned(CustomWave wave)
    {
        if (wave is not null)
            WaveSpawned?.Invoke(wave);
    }

    private static bool MeetsThreshold(CustomWave wave, int playerCount)
    {
        if (wave is not IWeightedWave weighted)
            return true;

        var meetsMin = weighted.MinPlayers <= 0 || playerCount >= weighted.MinPlayers;
        var meetsMax = weighted.MaxPlayers < 0 || playerCount <= weighted.MaxPlayers;
        return meetsMin && meetsMax;
    }
}
