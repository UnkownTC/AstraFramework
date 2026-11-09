using System;
using System.Linq;
using System.Reflection;

using HarmonyLib;

namespace Astra.Attributes;

/// <summary>
/// 标记一个**静态方法**为框架托管的方法级 Harmony 补丁。
/// </summary>
/// <remarks>
/// <para>
/// 与类级 <see cref="ManagedPatchAttribute"/> 的区别：类级要求整类用 <see cref="HarmonyPatchAttribute"/>
/// 声明目标；本特性直接标注在单个 Prefix/Postfix/Transpiler/Finalizer 方法上，
/// 由特性自身携带目标信息，无需外层类再写 <see cref="HarmonyPatchAttribute"/>。
/// </para>
/// <para>
/// 构造重载与 <see cref="HarmonyPatchAttribute"/> 对齐，仅收录能在方法级标注中直接解析出目标的形式；
/// 不含目标类型信息的重载（仅 <c>methodName</c>、仅 <see cref="MethodType"/>、仅参数类型）在类级特性里
/// 依赖后续 <c>HarmonyPatch</c> 合并推断，方法级标注无法等价推断，故不提供。
/// </para>
/// <para>
/// 目标在特性构造时即解析一次（与 <see cref="EventMethodAttribute"/> 解析事件的方式一致），
/// 解析失败不会抛异常，而是留空由注册阶段报错。
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class PatchMethodAttribute : Attribute
{
    /// <summary>构造目标成员描述。</summary>
    /// <param name="targetType">被补丁的类型。</param>
    /// <param name="targetMethodName">目标成员名。</param>
    /// <param name="methodType">目标成员种类。</param>
    /// <param name="parameterTypes">目标方法参数类型。</param>
    private PatchMethodAttribute(Type targetType, string? targetMethodName, MethodType methodType,
        Type[] parameterTypes)
    {
        TargetType = targetType;
        TargetMethodName = targetMethodName;
        MethodType = methodType;
        ParameterTypes = parameterTypes;
        Target = ResolveTarget();
    }

    /// <summary>按类型与成员名声明目标（无参数）。</summary>
    /// <param name="targetType">被补丁的类型。</param>
    /// <param name="targetMethodName">目标成员名。</param>
    public PatchMethodAttribute(Type targetType, string targetMethodName)
        : this(targetType, targetMethodName, MethodType.Normal, Type.EmptyTypes)
    {
    }

    /// <summary>按类型、成员名与参数类型声明目标。</summary>
    /// <param name="targetType">被补丁的类型。</param>
    /// <param name="targetMethodName">目标成员名。</param>
    /// <param name="parameterTypes">目标方法参数类型；为空时按名字匹配唯一重载。</param>
    public PatchMethodAttribute(Type targetType, string targetMethodName, params Type[] parameterTypes)
        : this(targetType, targetMethodName, MethodType.Normal, parameterTypes ?? Type.EmptyTypes)
    {
    }

    /// <summary>按类型、成员名与成员种类声明目标。</summary>
    /// <param name="targetType">被补丁的类型。</param>
    /// <param name="targetMethodName">目标成员名（属性名）。</param>
    /// <param name="methodType">目标成员种类。</param>
    public PatchMethodAttribute(Type targetType, string targetMethodName, MethodType methodType)
        : this(targetType, targetMethodName, methodType, Type.EmptyTypes)
    {
    }

    /// <summary>按类型与成员种类声明目标。</summary>
    /// <param name="targetType">被补丁的类型。</param>
    /// <param name="methodType">目标成员种类。</param>
    /// <param name="parameterTypes">目标方法参数类型。</param>
    public PatchMethodAttribute(Type targetType, MethodType methodType, params Type[] parameterTypes)
        : this(targetType, null, methodType, parameterTypes ?? Type.EmptyTypes)
    {
    }

    /// <summary>按类型声明构造函数目标。</summary>
    /// <param name="targetType">被补丁的类型。</param>
    /// <param name="parameterTypes">构造函数参数类型。</param>
    public PatchMethodAttribute(Type targetType, params Type[] parameterTypes)
        : this(targetType, null, MethodType.Constructor, parameterTypes ?? Type.EmptyTypes)
    {
    }

    /// <summary>按类型声明无参构造函数目标。</summary>
    /// <param name="targetType">被补丁的类型。</param>
    public PatchMethodAttribute(Type targetType)
        : this(targetType, null, MethodType.Constructor, Type.EmptyTypes)
    {
    }

    /// <summary>按类型全名声明目标。</summary>
    /// <param name="typeName">被补丁类型的全名。</param>
    /// <param name="targetMethodName">目标成员名。</param>
    /// <param name="methodType">目标成员种类。</param>
    /// <exception cref="ArgumentException">找不到指定类型。</exception>
    public PatchMethodAttribute(string typeName, string targetMethodName,
        MethodType methodType = MethodType.Normal)
        : this(FindType(typeName) ?? throw new ArgumentException($"找不到类型 {typeName}", nameof(typeName)),
            targetMethodName, methodType, Type.EmptyTypes)
    {
    }

    /// <summary>被补丁的类型。</summary>
    public Type TargetType { get; }

    /// <summary>目标成员名；构造函数类目标为 <see langword="null"/>。</summary>
    public string? TargetMethodName { get; }

    /// <summary>目标成员种类。</summary>
    public MethodType MethodType { get; }

    /// <summary>目标方法参数类型。</summary>
    public Type[] ParameterTypes { get; }

    /// <summary>解析出的目标方法；解析失败时为 <see langword="null"/>。</summary>
    internal MethodBase? Target { get; }

    /// <summary>按全名在已加载程序集中查找类型。</summary>
    /// <param name="typeName">类型全名。</param>
    /// <returns>找到的类型；找不到时为 <see langword="null"/>。</returns>
    private static Type? FindType(string typeName) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(typeName, false))
        .FirstOrDefault(type => type is not null);

    /// <summary>解析目标方法本体。</summary>
    /// <returns>目标方法；找不到时为 <see langword="null"/>。</returns>
    private MethodBase? ResolveTarget()
    {
        try
        {
            return MethodType switch
            {
                MethodType.Getter => AccessTools.DeclaredProperty(TargetType, TargetMethodName)?.GetGetMethod(true),
                MethodType.Setter => AccessTools.DeclaredProperty(TargetType, TargetMethodName)?.GetSetMethod(true),
                MethodType.Constructor => ParameterTypes.Length == 0
                    ? AccessTools.DeclaredConstructor(TargetType)
                    : AccessTools.DeclaredConstructor(TargetType, ParameterTypes),
                MethodType.StaticConstructor => AccessTools.DeclaredConstructor(TargetType, Type.EmptyTypes, true),
                _ => ParameterTypes.Length == 0
                    ? AccessTools.DeclaredMethod(TargetType, TargetMethodName)
                      ?? AccessTools.Method(TargetType, TargetMethodName)
                    : AccessTools.DeclaredMethod(TargetType, TargetMethodName, ParameterTypes)
                      ?? AccessTools.Method(TargetType, TargetMethodName, ParameterTypes),
            };
        }
        catch
        {
            return null;
        }
    }
}
