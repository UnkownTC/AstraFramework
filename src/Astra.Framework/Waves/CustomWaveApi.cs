using System;
using System.Collections.Generic;
using System.Linq;

using LabApi.Features.Wrappers;

namespace Astra.Waves;

/// <summary>自定义波次对外 API：角色生成器注册与波次执行。</summary>
public static class CustomWaveApi
{
    /// <summary>当前回合的全部波次实例。</summary>
    public static IReadOnlyDictionary<string, CustomWave> Waves => CustomWaveManager.All;

    /// <summary>按 Id 查找当前回合的波次实例。</summary>
    /// <param name="id">波次 Id。</param>
    /// <returns>波次实例；不存在为 <see langword="null"/>。</returns>
    public static CustomWave? Find(string id) => CustomWaveManager.Find(id);

    /// <summary>按类型查找波次实例。</summary>
    /// <typeparam name="T">波次类型。</typeparam>
    /// <returns>波次实例；未注册或类型不匹配为 <see langword="null"/>。</returns>
    /// <remarks>用类型代替 Id 字符串，避免拼写错误导致的静默失效。</remarks>
    public static T? Find<T>() where T : CustomWave =>
        Waves.Values.OfType<T>().FirstOrDefault();

    /// <summary>按类型查找波次实例。</summary>
    /// <typeparam name="T">波次类型。</typeparam>
    /// <param name="wave">取到的实例。</param>
    /// <returns>存在时返回 <see langword="true"/>。</returns>
    public static bool TryFind<T>(out T wave) where T : CustomWave
    {
        wave = Find<T>()!;
        return wave is not null;
    }

    /// <summary>按模板实例化波次（幂等，框架启动时自动调用，须晚于阵营实例化）。</summary>
    public static void Initialize() => CustomWaveManager.Initialize();

    /// <summary>登记波次模板（框架自动扫描时调用，通常无需手动）。</summary>
    /// <param name="type">波次类型。</param>
    public static void RegisterTemplate(Type type) => CustomWaveManager.RegisterTemplate(type);

    /// <summary>当前生效的角色生成器。</summary>
    public static ICustomWaveRoleSpawner? RoleSpawner => CustomWaveRoleSpawner.Current;

    /// <summary>注册角色生成器（宿主插件在启动时调用一次）。</summary>
    /// <param name="spawner">生成器实现。</param>
    public static void SetRoleSpawner(ICustomWaveRoleSpawner spawner) => CustomWaveRoleSpawner.Current = spawner;

    /// <summary>用委托注册角色生成器。</summary>
    /// <param name="trySpawn">套用实现：角色 Id + 玩家 → 是否成功。</param>
    public static void SetRoleSpawner(Func<string, Player, bool> trySpawn) =>
        CustomWaveRoleSpawner.Current = CustomWaveRoleSpawner.FromDelegate(trySpawn);

    /// <summary>执行波次：按波次表把玩家刷成对应角色。</summary>
    /// <param name="wave">波次实例。</param>
    /// <param name="players">候选玩家。</param>
    /// <returns>成功套用的玩家。</returns>
    public static IReadOnlyList<Player> Spawn(CustomWave wave, IEnumerable<Player> players) =>
        wave.Spawn(players);
}
