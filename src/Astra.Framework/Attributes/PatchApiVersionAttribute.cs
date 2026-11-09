using System;

namespace Astra.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class PatchApiVersionAttribute(int major, int minor, int build, int revision) : Attribute
{
    public Version ApiVersion { get; } = new(major, minor, build, revision);
}
