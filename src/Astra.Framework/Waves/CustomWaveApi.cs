using System;
using System.Collections.Generic;
using System.Linq;

using Astra.Lifecycle;

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

    /// <summary>按实例注册波次（外部框架 / 动态定义用）。</summary>
    /// <param name="wave">波次实例。</param>
    /// <returns>注册成功（或同实例重复注册）返回 <see langword="true"/>；未初始化、Id 冲突或阵营依赖缺失返回 <see langword="false"/>。</returns>
    /// <remarks>须在框架自动注册完成后调用，否则会因波次实例化早退而丢失 C# 模板。</remarks>
    public static bool Register(CustomWave wave)
    {
        if (wave is null || !CustomWaveManager.RegisterInstance(wave))
            return false;

        RegistrationTracking.Wave(wave, RegistrationSource.FromType(wave.GetType()));
        return true;
    }

    /// <summary>在指定注册作用域内按实例注册波次。</summary>
    /// <param name="wave">波次实例。</param>
    /// <param name="scope">注册作用域；释放时按作用域来源撤销该波次。</param>
    /// <returns>注册成功（或同实例重复注册）返回 <see langword="true"/>；未初始化、Id 冲突或阵营依赖缺失返回 <see langword="false"/>。</returns>
    public static bool Register(CustomWave wave, RegistrationScope scope)
    {
        if (wave is null)
            return false;
        if (scope is null)
            throw new ArgumentNullException(nameof(scope));

        if (!CustomWaveManager.RegisterInstance(wave))
            return false;

        RegistrationTracking.Wave(wave, scope.Source);
        return true;
    }

    /// <summary>按 Id 注销波次。</summary>
    /// <param name="id">波次 Id。</param>
    /// <returns>确实注销时返回 <see langword="true"/>。</returns>
    public static bool Unregister(string id) => CustomWaveManager.Unregister(id);

    /// <summary>当前生效的角色生成器。</summary>
    public static ICustomWaveRoleSpawner? RoleSpawner => CustomWaveRoleSpawner.Current;

    /// <summary>注册角色生成器（宿主插件在启动时调用一次；会替换已有的生成器链）。</summary>
    /// <param name="spawner">生成器实现。</param>
    public static void SetRoleSpawner(ICustomWaveRoleSpawner spawner) => CustomWaveRoleSpawner.Set(spawner);

    /// <summary>用委托注册角色生成器（会替换已有的生成器链）。</summary>
    /// <param name="trySpawn">套用实现：角色 Id + 玩家 → 是否成功。</param>
    public static void SetRoleSpawner(Func<string, Player, bool> trySpawn) =>
        CustomWaveRoleSpawner.Set(CustomWaveRoleSpawner.FromDelegate(trySpawn));

    /// <summary>追加角色生成器：按注册顺序依次尝试，首个成功即结束。</summary>
    /// <param name="spawner">生成器实现。</param>
    public static void AddRoleSpawner(ICustomWaveRoleSpawner spawner) => CustomWaveRoleSpawner.Add(spawner);

    /// <summary>按 Id 立即执行波次。</summary>
    /// <param name="waveId">波次 Id。</param>
    /// <param name="players">候选玩家。</param>
    /// <returns>成功套用的玩家；Id 不存在时为空集合。</returns>
    public static IReadOnlyList<Player> Spawn(string waveId, IEnumerable<Player> players) =>
        CustomWaveManager.Find(waveId)?.Spawn(players) ?? Array.Empty<Player>();

    /// <summary>立即执行指定波次实例。</summary>
    /// <param name="wave">波次实例。</param>
    /// <param name="players">候选玩家。</param>
    /// <returns>成功套用的玩家；实例为空时为空集合。</returns>
    public static IReadOnlyList<Player> Spawn(CustomWave wave, IEnumerable<Player> players) =>
        wave?.Spawn(players) ?? Array.Empty<Player>();
}
