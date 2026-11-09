using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Astra;
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

namespace Astra.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class PatchApiVersionAttribute(int major, int minor, int build, int revision) : Attribute
{
    public Version ApiVersion { get; } = new(major, minor, build, revision);
}
