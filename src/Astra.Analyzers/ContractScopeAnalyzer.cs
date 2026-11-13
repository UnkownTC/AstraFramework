using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Astra.Analyzers;

/// <summary>
/// 契约范围守卫：检查「只在特定场景有效」的特性、接口与类型形态是否被放到了错误的位置。
/// </summary>
/// <remarks>
/// 这些约定都无法由编译器天然表达——违反时框架大多会静默跳过，因此用分析器提前拦截。
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ContractScopeAnalyzer : DiagnosticAnalyzer
{
    private const string EventHandlerBase = "CustomEventsHandler";
    private const string ManagedEventAttribute = "ManagedEventAttribute";
    private const string ManagedPatchAttribute = "ManagedPatchAttribute";
    private const string PatchMethodAttribute = "PatchMethodAttribute";
    private const string EventMethodAttribute = "EventMethodAttribute";
    private const string ManualPatchInterface = "IManualPatch";

    /// <summary>由框架直接 <c>Activator.CreateInstance</c> 的基类与接口。</summary>
    private static readonly string[] InstantiatedTypes =
    [
        "CustomRole",
        "CustomAbility",
        "IManualPatch",
    ];

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        Diagnostics.ManagedEventNeedsEventHandler,
        Diagnostics.ManagedPatchStaticMismatch,
        Diagnostics.PatchMethodHostMustBeStatic,
        Diagnostics.EventMethodOutsideHandler,
        Diagnostics.ReflectionInstantiationContract);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
    }

    /// <summary>检查类型级契约。</summary>
    /// <param name="context">符号分析上下文。</param>
    private static void AnalyzeType(SymbolAnalysisContext context)
    {
        if (context.Symbol is not INamedTypeSymbol type)
            return;

        if (HasAttribute(type, ManagedEventAttribute) && !DerivesFromEventHandler(type))
        {
            Report(context, Diagnostics.ManagedEventNeedsEventHandler, type, type.Name);
        }

        CheckPatchShape(context, type);
        CheckPatchMethodHost(context, type);
        CheckInstantiationContract(context, type);
    }

    /// <summary>检查方法级契约。</summary>
    /// <param name="context">符号分析上下文。</param>
    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        if (context.Symbol is not IMethodSymbol method)
            return;

        if (HasAttribute(method, EventMethodAttribute) &&
            (method.ContainingType is null || !DerivesFromEventHandler(method.ContainingType)))
        {
            Report(context, Diagnostics.EventMethodOutsideHandler, method, method.Name);
        }
    }

    /// <summary>补丁类形态与补丁方式必须匹配。</summary>
    /// <param name="context">符号分析上下文。</param>
    /// <param name="type">候选类型。</param>
    private static void CheckPatchShape(SymbolAnalysisContext context, INamedTypeSymbol type)
    {
        if (!HasAttribute(type, ManagedPatchAttribute))
            return;

        var implementsManual = type.AllInterfaces.Any(i => i.Name == ManualPatchInterface);

        // Harmony 只要求补丁方法 static，宿主类可以是普通类；仅 IManualPatch 因需实例化而不能是静态类
        if (implementsManual && type.IsStatic)
        {
            Report(context, Diagnostics.ManagedPatchStaticMismatch, type,
                type.Name, "IManualPatch 由框架实例化调用，不能是静态类");
        }
    }

    /// <summary>承载 [PatchMethod] 的类型建议声明为静态类。</summary>
    /// <param name="context">符号分析上下文。</param>
    /// <param name="type">候选类型。</param>
    private static void CheckPatchMethodHost(SymbolAnalysisContext context, INamedTypeSymbol type)
    {
        if (type.IsStatic)
            return;

        var hasPatchMethod = type.GetMembers()
            .OfType<IMethodSymbol>()
            .Any(method => HasAttribute(method, PatchMethodAttribute));

        if (hasPatchMethod)
        {
            Report(context, Diagnostics.PatchMethodHostMustBeStatic, type, type.Name);
        }
    }

    /// <summary>被框架反射实例化的类型必须满足实例化契约。</summary>
    /// <param name="context">符号分析上下文。</param>
    /// <param name="type">候选类型。</param>
    private static void CheckInstantiationContract(SymbolAnalysisContext context, INamedTypeSymbol type)
    {
        if (type.IsAbstract || type.IsStatic)
            return;   // 抽象基类与非实例化类型不在此列

        var derived = DerivesFromAny(type, InstantiatedTypes);
        var implemented = type.AllInterfaces.Any(i => InstantiatedTypes.Contains(i.Name));

        if (!derived && !implemented)
            return;

        var hasParameterlessCtor = type.InstanceConstructors
            .Any(ctor => ctor.Parameters.Length == 0 && ctor.DeclaredAccessibility == Accessibility.Public);

        if (hasParameterlessCtor)
            return;

        Report(context, Diagnostics.ReflectionInstantiationContract, type,
            type.Name, "缺少 public 无参构造，框架无法实例化");
    }

    /// <summary>判断类型是否派生自自定义事件处理器。</summary>
    /// <param name="type">候选类型。</param>
    /// <returns>派生自 <c>CustomEventsHandler</c> 时返回 <see langword="true"/>。</returns>
    private static bool DerivesFromEventHandler(INamedTypeSymbol type) =>
        DerivesFromAny(type, [EventHandlerBase]);

    /// <summary>沿基类链查找指定名称的基类。</summary>
    /// <param name="type">候选类型。</param>
    /// <param name="names">基类名称集合。</param>
    /// <returns>命中时返回 <see langword="true"/>。</returns>
    private static bool DerivesFromAny(INamedTypeSymbol type, string[] names)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (names.Contains(current.Name))
                return true;
        }

        return false;
    }

    /// <summary>判断符号是否带有指定特性（按名称匹配，兼容有无 Attribute 后缀）。</summary>
    /// <param name="symbol">符号。</param>
    /// <param name="baseName">不带后缀的特性名。</param>
    /// <returns>命中时返回 <see langword="true"/>。</returns>
    private static bool HasAttribute(ISymbol symbol, string baseName) =>
        symbol.GetAttributes().Any(attribute => AttributeNames.Is(attribute.AttributeClass, baseName));

    /// <summary>上报诊断（取首个源码位置）。</summary>
    /// <param name="context">符号分析上下文。</param>
    /// <param name="descriptor">诊断描述符。</param>
    /// <param name="symbol">目标符号。</param>
    /// <param name="args">消息参数。</param>
    private static void Report(SymbolAnalysisContext context, DiagnosticDescriptor descriptor,
        ISymbol symbol, params object[] args)
    {
        var location = symbol.Locations.FirstOrDefault(item => item.IsInSource);
        if (location is null)
            return;

        context.ReportDiagnostic(Diagnostic.Create(descriptor, location, args));
    }
}
