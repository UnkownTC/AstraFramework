using System;
using System.Collections.Generic;
using System.Linq;


using LabApi.Features.Console;

using UserSettings.ServerSpecific;

using ServerSpecificSettingBase = Astra.Abstractions.ServerSpecificSettingBase;

namespace Astra.Systems.ServerSpecificSettingSystem
{
    /// <summary>
    /// 服务器专属设置管理器单例：负责设置组的登记、注销与统一下发。
    /// </summary>
    public static class ServerSpecificSettingManager
    {
        private static readonly Dictionary<string, ServerSpecificSettingBase> _settingBases = new();

        /// <summary>按类型实例化并登记设置组（框架内部调用）。</summary>
        /// <param name="type">设置组类型。</param>
        internal static void Register(Type type)
        {
            if (Activator.CreateInstance(type) is not ServerSpecificSettingBase settingBase)
                return;

            var id = settingBase.Id;
            _settingBases[id] = settingBase;

            Logger.Info($"[ServerSpecificSettingManager] {id} 已注册");
        }

        /// <summary>注销所有设置组并清空下发的设置（框架内部调用）。</summary>
        internal static void UnregisterAll()
        {
            _settingBases.Clear();
            ServerSpecificSettingsSync.DefinedSettings = [];
            ServerSpecificSettingsSync.SendToAll();
            Logger.Info($"[ServerSpecificSettingManager] 已全部注销");
        }

        /// <summary>按 Id 登记设置组。</summary>
        /// <param name="id">设置组 Id。</param>
        public static void Register(string id)
        {
            if (!_settingBases.TryGetValue(id, out var settingBase))
                return;
            Logger.Info($"[ServerSpecificSettingManager] {id} 已注册");
        }

        /// <summary>按 Id 注销设置组。</summary>
        /// <param name="id">设置组 Id。</param>
        public static void Unregister(string id)
        {
            if (!_settingBases.TryGetValue(id, out var settingBase))
                return;
            _settingBases.Remove(id);
            Logger.Info($"[ServerSpecificSettingManager] {id} 已注销");
        }

        /// <summary>
        /// 将所有设置组按优先级排序、按 SettingId 去重后统一下发给客户端。
        /// </summary>
        public static void SendToAll()
        {
            var orderedBases = _settingBases.Values.OrderBy(s => s.Priority == -1 ? int.MaxValue : s.Priority);
            var definedSettings = ServerSpecificSettingsSync.DefinedSettings;
            var activeSettings = new List<UserSettings.ServerSpecific.ServerSpecificSettingBase>();
            var cacheSettingId = new HashSet<int>();
            foreach (var setting in orderedBases)
            {
                foreach (var activeSetting in setting.ActiveSettings)
                {
                    if (!cacheSettingId.Add(activeSetting.SettingId))
                    {
                        Logger.Warn($"[ServerSpecificSettingManager] 发现重复SettingId: {activeSetting.SettingId} Label: {activeSetting.Label}");
                        continue;
                    }
                    activeSettings.Add(activeSetting);
                }
            }

            if (definedSettings is not null)
            {
                foreach (var definedSetting in definedSettings)
                {
                    if (!cacheSettingId.Add(definedSetting.SettingId))
                    {
                        Logger.Warn($"[ServerSpecificSettingManager] 发现重复SettingId: {definedSetting.SettingId} Label: {definedSetting.Label}");
                        continue;
                    }
                    activeSettings.Add(definedSetting);
                }
            }

            ServerSpecificSettingsSync.DefinedSettings = activeSettings.ToArray();
            ServerSpecificSettingsSync.SendToAll();
        }
    }
}
