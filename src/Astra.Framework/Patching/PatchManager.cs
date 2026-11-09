using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Astra.Attributes;
using Astra.Loader;

using HarmonyLib;

using Logger = LabApi.Features.Console.Logger;

namespace Astra.Patching;
    /// <summary>Astra 托管 Harmony 补丁的登记、启停与统一卸载管理器。</summary>
    internal static class PatchManager
    {
        private const string ManualHarmonyIdPrefix = "com.azure.framework.manual.";
        private static readonly Dictionary<string, PatchEntry> _patches = new Dictionary<string, PatchEntry>();
        private static readonly Dictionary<string, MethodPatchEntry> _methodPatches =
            new Dictionary<string, MethodPatchEntry>(StringComparer.OrdinalIgnoreCase);

        /// <summary>是否输出详细日志（由配置 <c>IsDebug</c> 控制）。</summary>
        private static bool DebugEnabled => AstraPlugin.Configuration.IsDebug;

        /// <summary>所有已登记补丁的只读状态快照。</summary>
        public static IReadOnlyList<PatchStatus> Patches => _patches
            .Select(pair => new PatchStatus(pair.Key, pair.Value.PatchType, pair.Value.IsPatched))
            .OrderBy(status => status.Id, StringComparer.OrdinalIgnoreCase).ToArray();

        internal static void PatchType(Type type, Harmony harmony)
        {
            var id = type.FullName ?? type.Name;
            if (_patches.Keys.Any(key => string.Equals(key, id, StringComparison.OrdinalIgnoreCase)))
            { Logger.Error($"[PatchManager] 补丁 Id {id} 已存在。"); return; }
            if (!VersionGuard.Validate(type,
                    AstraPlugin.Configuration.IgnorePatchApiMajorVersionDiff,
                    AstraPlugin.Configuration.IgnorePatchGameMajorVersionDiff))
            { Logger.Error($"[PatchManager] {id} 版本不兼容，拒绝登记。"); return; }
            var entryHarmony = typeof(IManualPatch).IsAssignableFrom(type) ? new Harmony(ManualHarmonyIdPrefix + id) : harmony;
            var entry = new PatchEntry(entryHarmony, type);
            _patches.Add(id, entry);
            if (DebugEnabled)
                Logger.Info($"[PatchManager] {id} 已登记");
            if (type.GetCustomAttribute<DisableAutoRegisterAttribute>() == null)
                TryApplyEntry(id, entry);
        }

        public static void UnpatchAll()
        {
            foreach (var pair in _patches.ToArray())
                TryUnpatchEntry(pair.Key, pair.Value);
            _patches.Clear();

            foreach (var pair in _methodPatches.ToArray())
            {
                try
                {
                    pair.Value.Harmony.Unpatch(pair.Value.Target, pair.Value.PatchMethod);
                    if (DebugEnabled)
                        Logger.Info($"[PatchManager] 方法补丁 {pair.Key} 已禁用");
                }
                catch (Exception ex)
                {
                    Logger.Error($"[PatchManager] 方法补丁 {pair.Key} 禁用失败：{ex}");
                }
            }

            _methodPatches.Clear();
        }

        /// <summary>
        /// 登记并应用一个方法级补丁（由 <see cref="PatchMethodAttribute"/> 驱动）。
        /// </summary>
        /// <param name="patchMethod">补丁方法，必须是静态方法并标注 Prefix/Postfix/Transpiler/Finalizer 之一。</param>
        /// <param name="harmony">应用补丁的 Harmony 实例。</param>
        /// <returns>成功应用返回 <see langword="true"/>。</returns>
        internal static bool PatchMethod(MethodInfo patchMethod, Harmony harmony)
        {
            var attr = patchMethod.GetCustomAttribute<PatchMethodAttribute>();
            if (attr is null)
                return false;

            var id = $"{patchMethod.DeclaringType?.FullName ?? patchMethod.DeclaringType?.Name}.{patchMethod.Name}";

            if (!patchMethod.IsStatic)
            {
                Logger.Error($"[PatchManager] 方法补丁 {id} 必须是静态方法，已拒绝。");
                return false;
            }

            if (!VersionGuard.Validate(patchMethod,
                    AstraPlugin.Configuration.IgnorePatchApiMajorVersionDiff,
                    AstraPlugin.Configuration.IgnorePatchGameMajorVersionDiff))
                return false;

            if (_methodPatches.ContainsKey(id))
            {
                Logger.Error($"[PatchManager] 方法补丁 {id} 已存在。");
                return false;
            }

            var target = attr.Target;
            if (target is null)
            {
                Logger.Error($"[PatchManager] 方法补丁 {id} 的目标 {attr.TargetType.Name}.{attr.TargetMethodName} 未找到，已拒绝。");
                return false;
            }

            var prefix = patchMethod.GetCustomAttribute<HarmonyPrefix>() is null ? null : new HarmonyMethod(patchMethod);
            var postfix = patchMethod.GetCustomAttribute<HarmonyPostfix>() is null ? null : new HarmonyMethod(patchMethod);
            var transpiler = patchMethod.GetCustomAttribute<HarmonyTranspiler>() is null ? null : new HarmonyMethod(patchMethod);
            var finalizer = patchMethod.GetCustomAttribute<HarmonyFinalizer>() is null ? null : new HarmonyMethod(patchMethod);

            if (prefix is null && postfix is null && transpiler is null && finalizer is null)
            {
                Logger.Error($"[PatchManager] 方法补丁 {id} 未标注 HarmonyPrefix/Postfix/Transpiler/Finalizer，已拒绝。");
                return false;
            }

            try
            {
                harmony.Patch(target, prefix, postfix, transpiler, finalizer);
                _methodPatches[id] = new MethodPatchEntry(harmony, target, patchMethod);
                if (DebugEnabled)
                    Logger.Info($"[PatchManager] 方法补丁 {id} 已启用 → {target.DeclaringType?.Name}.{target.Name}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"[PatchManager] 方法补丁 {id} 启用失败：{ex}");
                return false;
            }
        }

        /// <summary>按完整类型名或唯一短类名查询补丁；匹配不区分大小写。</summary>
        public static bool TryGetPatchStatus(string id, out PatchStatus status)
        {
            if (!TryResolveEntry(id, out var resolvedId, out var entry))
            { status = null; return false; }
            status = new PatchStatus(resolvedId, entry.PatchType, entry.IsPatched);
            return true;
        }

        public static bool TryPatch(string id) => TryResolveEntry(id, out var resolvedId, out var entry) && TryApplyEntry(resolvedId, entry);
        public static bool TryUnpatch(string id) => TryResolveEntry(id, out var resolvedId, out var entry) && TryUnpatchEntry(resolvedId, entry);
        public static void Patch(string typeFullName) => TryPatch(typeFullName);
        public static void Unpatch(string typeFullName) => TryUnpatch(typeFullName);

        private static bool TryResolveEntry(string id, out string resolvedId, out PatchEntry entry)
        {
            resolvedId = null;
            entry = null;
            if (string.IsNullOrWhiteSpace(id))
                return false;
            var matches = _patches.Where(pair => string.Equals(pair.Key, id, StringComparison.OrdinalIgnoreCase) ||
                                                 string.Equals(pair.Value.PatchType.Name, id, StringComparison.OrdinalIgnoreCase))
                .Take(2).ToArray();
            if (matches.Length != 1)
                return false;
            resolvedId = matches[0].Key;
            entry = matches[0].Value;
            return true;
        }

        private static bool TryApplyEntry(string id, PatchEntry entry)
        {
            if (entry.IsPatched)
                return false;
            try
            {
                if (typeof(IManualPatch).IsAssignableFrom(entry.PatchType))
                    ((IManualPatch)Activator.CreateInstance(entry.PatchType)).ApplyPatch(entry.Harmony);
                else
                    entry.Harmony.CreateClassProcessor(entry.PatchType).Patch();
                entry.IsPatched = true;
                if (DebugEnabled)
                    Logger.Info($"[PatchManager] {id} 已启用");
                return true;
            }
            catch (Exception ex) { Logger.Error($"[PatchManager] {id} 启用失败：{ex}"); return false; }
        }

        private static bool TryUnpatchEntry(string id, PatchEntry entry)
        {
            if (!entry.IsPatched)
                return false;
            try
            {
                if (typeof(IManualPatch).IsAssignableFrom(entry.PatchType))
                    entry.Harmony.UnpatchAll(entry.Harmony.Id);
                else
                    entry.Harmony.CreateClassProcessor(entry.PatchType).Unpatch();
                entry.IsPatched = false;
                if (DebugEnabled)
                    Logger.Info($"[PatchManager] {id} 已禁用");
                return true;
            }
            catch (Exception ex) { Logger.Error($"[PatchManager] {id} 禁用失败：{ex}"); return false; }
        }

        private sealed class MethodPatchEntry
        {
            public MethodPatchEntry(Harmony harmony, MethodBase target, MethodInfo patchMethod)
            {
                Harmony = harmony;
                Target = target;
                PatchMethod = patchMethod;
            }

            public Harmony Harmony { get; }

            public MethodBase Target { get; }

            public MethodInfo PatchMethod { get; }
        }

        private sealed class PatchEntry
        {
            public Harmony Harmony { get; }
            public Type PatchType { get; }
            public bool IsPatched { get; set; }
            public PatchEntry(Harmony harmony, Type patchType) { Harmony = harmony; PatchType = patchType; }
        }
    }

    public sealed class PatchStatus
    {
        public string Id { get; }
        public Type PatchType { get; }
        public string TypeFullName => PatchType.FullName ?? PatchType.Name;
        public bool IsEnabled { get; }
        internal PatchStatus(string id, Type patchType, bool isEnabled) { Id = id; PatchType = patchType; IsEnabled = isEnabled; }
    }

