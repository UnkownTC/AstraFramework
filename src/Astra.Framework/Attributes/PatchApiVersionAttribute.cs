using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class PatchApiVersionAttribute(int major, int minor, int build, int revision) : Attribute
{
    public Version ApiVersion { get; } = new(major, minor, build, revision);
}
