using System;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Astra.Analyzers;

/// <summary>框架分析器的诊断描述符集合。</summary>
internal static class Diagnostics
{
    private const string Category = "Astra";

    internal static readonly DiagnosticDescriptor EventMethodMustOverride = new(
        id: "AZF1001",
        title: "[EventMethod] 实例方法必须重写基类虚方法",
        messageFormat: "方法 '{0}' 标注了 [EventMethod]，但既不是 override 也不是 virtual 声明点，框架不会注册它",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:
        "实例方法由 EnhancedEventsHandler 构造函数扫描注册，只认 true override（new 隐藏无效）；" +
        "静态方法走类型扫描，不受此限制。");

    internal static readonly DiagnosticDescriptor EventMethodSignature = new(
        id: "AZF1002",
        title: "[EventMethod] 方法签名与目标事件不匹配",
        messageFormat: "方法 '{0}' 的签名与事件 {1} 的委托签名不一致，运行期会创建委托失败",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "EventMethodManager 用 Delegate.CreateDelegate 绑定方法，参数个数与类型必须与事件委托兼容。");

    internal static readonly DiagnosticDescriptor EventMethodAccessibility = new(
        id: "AZF1003",
        title: "[EventMethod] 方法必须是 public",
        messageFormat: "方法 '{0}' 标注了 [EventMethod]，但不是 public，框架不会注册它",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "注册流程通过反射扫描 public 方法，非 public 方法会被静默跳过。");

    internal static readonly DiagnosticDescriptor EventMethodTargetMissing = new(
        id: "AZF1004",
        title: "[EventMethod] 目标事件在编译期找不到",
        messageFormat: "事件 '{0}.{1}' 在编译期找不到（标注于方法 '{2}'）",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "事件名拼写错误或目标类型选错时，运行期会静默创建空订阅。");

    internal static readonly DiagnosticDescriptor PatchMethodMustBeStatic = new(
        id: "AZF2001",
        title: "[ManagedPatchMethod] 补丁方法必须是静态方法",
        messageFormat: "补丁方法 '{0}' 必须是 static，框架会拒绝注册",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Harmony 补丁方法必须为静态方法，PatchManager 会在注册阶段校验并拒绝实例方法。");

    internal static readonly DiagnosticDescriptor PatchMethodMissingHarmonyKind = new(
        id: "AZF2002",
        title: "[ManagedPatchMethod] 缺少 Harmony 补丁类型标注",
        messageFormat: "补丁方法 '{0}' 必须标注 [HarmonyPrefix]/[HarmonyPostfix]/[HarmonyTranspiler]/[HarmonyFinalizer] 之一",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "没有补丁类型标注时 Harmony 无法确定该方法的角色，PatchManager 会拒绝注册。");

    internal static readonly DiagnosticDescriptor PatchMethodTargetMissing = new(
        id: "AZF2003",
        title: "[ManagedPatchMethod] 目标方法在编译期找不到",
        messageFormat: "[ManagedPatchMethod] 目标 '{0}.{1}' 在编译期找不到匹配成员",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:
        "目标方法不存在时运行期会拒绝该补丁。常见原因：方法名拼写错误、参数类型不匹配、" +
        "目标类型选错了重载。");

    internal static readonly DiagnosticDescriptor ManagedPatchNeedsTarget = new(
        id: "AZF2101",
        title: "[ManagedPatch] 类缺少目标声明",
        messageFormat: "类型 '{0}' 标注了 [ManagedPatch]，但既没有 [HarmonyPatch] 也未实现 IManualPatch",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:
        "类级托管补丁必须通过 [HarmonyPatch] 声明目标，或实现 IManualPatch 自行应用/卸载补丁。");

    internal static readonly DiagnosticDescriptor VersionAttributeUnused = new(
        id: "AZF3001",
        title: "版本特性仅对补丁生效",
        messageFormat: "'{0}' 上的版本特性不会生效：只有被补丁管理器管理的类型或方法才会做版本校验",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description:
        "PatchApiVersion / PatchGameVersion 目前只在 PatchManager 中消费；标在角色、技能或事件处理器上" +
        "不会产生任何校验。");

    internal static readonly DiagnosticDescriptor SystemsNamespaceLeak = new(
        id: "AZF4001",
        title: "框架内部实现命名空间不应被外部引用",
        messageFormat: "'{0}' 属于框架内部实现，外部请改用 Astra.Apis 中的 Api 门面",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
        "Astra.Systems 下的类型为内部调度实现（管理器、补丁、缓存），可能随时重构。" +
        "插件应通过 Astra.Apis 的 Api 门面调用，通过 Abstractions 基类扩展。");

    internal static readonly DiagnosticDescriptor ManagedEventNeedsEventHandler = new(
        id: "AZF5001",
        title: "[ManagedEvent] 只能标在事件处理器上",
        messageFormat: "类型 '{0}' 标注了 [ManagedEvent]，但没有派生自 CustomEventsHandler",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:
        "EventManager 只登记 CustomEventsHandler 派生类型；不派生会被静默跳过，处理器永远不会被调用。");

    internal static readonly DiagnosticDescriptor ManagedPatchStaticMismatch = new(
        id: "AZF5002",
        title: "[ManagedPatch] 类形态与补丁方式不匹配",
        messageFormat: "类型 '{0}'：{1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:
        "声明式补丁（[HarmonyPatch]）应用静态类承载；IManualPatch 由框架实例化后调用，不能是静态类。");

    internal static readonly DiagnosticDescriptor PatchMethodHostMustBeStatic = new(
        id: "AZF5003",
        title: "[ManagedPatchMethod] 建议放在静态类",
        messageFormat: "类型 '{0}' 包含 [ManagedPatchMethod]，但自身不是静态类",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
        "补丁方法本身必须为静态方法（AZF2001 强制）；载体类用静态类更清晰，可避免实例状态与补丁生命周期耦合。");

    internal static readonly DiagnosticDescriptor EventMethodOutsideHandler = new(
        id: "AZF5004",
        title: "[EventMethod] 必须在事件处理器类型内",
        messageFormat: "方法 '{0}' 标注了 [EventMethod]，但所在类型未派生自 CustomEventsHandler",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:
        "事件方法由 EnhancedEventsHandler 实例构造时注册；类型不派生自 CustomEventsHandler 时不会被扫描到。");

    internal static readonly DiagnosticDescriptor ReflectionInstantiationContract = new(
        id: "AZF5005",
        title: "被框架反射实例化的类型不满足实例化契约",
        messageFormat: "类型 '{0}'：{1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:
        "框架通过 Activator.CreateInstance 实例化角色/技能/手动补丁/注册回调；" +
        "要求类型非抽象、非静态，且具备 public 无参构造。");

    internal static ImmutableArray<DiagnosticDescriptor> All { get; } = ImmutableArray.Create(
        EventMethodMustOverride,
        EventMethodSignature,
        EventMethodAccessibility,
        EventMethodTargetMissing,
        PatchMethodMustBeStatic,
        PatchMethodMissingHarmonyKind,
        PatchMethodTargetMissing,
        ManagedPatchNeedsTarget,
        VersionAttributeUnused,
        SystemsNamespaceLeak,
        ManagedEventNeedsEventHandler,
        ManagedPatchStaticMismatch,
        PatchMethodHostMustBeStatic,
        EventMethodOutsideHandler,
        ReflectionInstantiationContract);
}

/// <summary>特性名匹配工具：Harmony 的特性类名不带 Attribute 后缀，需要同时兼容两种形式。</summary>
internal static class AttributeNames
{
    /// <summary>解析特性语法对应的特性类型。</summary>
    /// <param name="semanticModel">语义模型。</param>
    /// <param name="attribute">特性语法。</param>
    /// <returns>特性类型；无法解析时为 <see langword="null"/>。</returns>
    internal static INamedTypeSymbol? Resolve(SemanticModel semanticModel, AttributeSyntax attribute)
    {
        var symbol = semanticModel.GetSymbolInfo(attribute).Symbol;

        return symbol switch
        {
            IMethodSymbol constructor => constructor.ContainingType,
            INamedTypeSymbol type => type,
            _ => null,
        };
    }

    /// <summary>判断特性类型名是否匹配指定基名。</summary>
    /// <param name="type">特性类型。</param>
    /// <param name="baseName">不带 Attribute 后缀的名字，如 <c>HarmonyPatch</c>。</param>
    /// <returns>匹配时为 <see langword="true"/>。</returns>
    internal static bool Is(INamedTypeSymbol? type, string baseName) =>
        type is not null &&
        (string.Equals(type.Name, baseName, StringComparison.Ordinal) ||
         string.Equals(type.Name, baseName + "Attribute", StringComparison.Ordinal));
}
