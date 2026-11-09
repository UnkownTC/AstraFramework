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
public class PatchGameVersionAttribute(int major, int minor, int revision, string versionSuffix = "") : Attribute
{
    public string GameVersion { get; } = $"{major}.{minor}.{revision}{versionSuffix}";
    public int MajorVersion { get; } = major;
    public int MinorVersion { get; } = minor;
    public int RevisionVersion { get; } = revision;
    public string VersionSuffix { get; } = versionSuffix;
}
