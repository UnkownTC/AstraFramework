using System.Collections.Generic;
using System.Linq;

using Astra;
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

namespace Astra.Loader;
    /// <summary>
    /// 基于字符串 Id 的组件管理器，提供 <see cref="IIdentifiable"/> 组件的增删查。
    /// </summary>
    /// <typeparam name="T">实现 <see cref="IIdentifiable"/> 的组件类型。</typeparam>
    public class IdentifiableManager<T> where T : IIdentifiable
    {
        /// <summary>Id -> 组件 的存储字典。</summary>
        protected readonly Dictionary<string, T> Items = new();

        /// <summary>添加组件，Id 不能为空。</summary>
        /// <param name="item">要添加的组件。</param>
        public void Add(T item)
        {
            if (string.IsNullOrEmpty(item.Id))
                throw new System.ArgumentException("Item Id cannot be null or empty.");
            Items[item.Id] = item;
        }

        /// <summary>按 Id 移除组件。</summary>
        /// <param name="id">组件 Id。</param>
        public void Remove(string id) => Items.Remove(id);

        /// <summary>按 Id 获取组件，不存在时返回默认值。</summary>
        /// <param name="id">组件 Id。</param>
        public T Get(string id)
        {
            Items.TryGetValue(id, out var item);
            return item;
        }

        /// <summary>获取所有组件的快照。</summary>
        public IEnumerable<T> GetAll() => Items.Values.ToList();

        /// <summary>判断是否存在指定 Id 的组件。</summary>
        /// <param name="id">组件 Id。</param>
        public bool Contains(string id) => Items.ContainsKey(id);
    }

