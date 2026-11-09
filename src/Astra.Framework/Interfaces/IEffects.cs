using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Astra.Abstractions;
using Astra.Effects;
using Astra.Systems.CustomEffectSystem;

using CustomPlayerEffects;

namespace Astra.Interfaces;

public interface IEffects
{
    EffectEntry[] Effects { get; }
}
