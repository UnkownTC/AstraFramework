using System;

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
