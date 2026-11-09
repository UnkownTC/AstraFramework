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
    /// 表示一个具有全局唯一字符串标识（Id）的组件。
    /// 实现该接口的类型可被框架通过 Id 精确注册、注销与查找。
    /// </summary>
    public interface IIdentifiable
    {
        /// <summary>
        /// 获取该组件的全局唯一标识。
        /// </summary>
        string Id { get; }
    }

