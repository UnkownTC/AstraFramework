using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Astra.Abstractions;

namespace Astra.Interfaces;

public interface IAbilityRole
{
    CustomAbility[] Abilities { get; }
}
