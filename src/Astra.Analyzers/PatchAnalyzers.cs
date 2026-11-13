using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Astra.Analyzers;

/// <summary>校验 [PatchMethod] 的用法：静态、有补丁类型、目标可解析。</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PatchMethodAnalyzer : DiagnosticAnalyzer
{
    private const string AttributeName = "PatchMethod";

    private static readonly string[] HarmonyKindAttributes =
    [
        "HarmonyPrefix",
        "HarmonyPostfix",
        "HarmonyTranspiler",
        "HarmonyFinalizer",
        "HarmonyReversePatch",
    ];

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        Diagnostics.PatchMethodMustBeStatic,
        Diagnostics.PatchMethodMissingHarmonyKind,
        Diagnostics.PatchMethodTargetMissing);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.Attribute);
    }

    /// <summary>校验单个 [PatchMethod] 标注。</summary>
    /// <param name="context">语法节点分析上下文。</param>
    private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not AttributeSyntax attribute)
            return;

        var attributeType = AttributeNames.Resolve(context.SemanticModel, attribute);
        if (!AttributeNames.Is(attributeType, AttributeName))
            return;

        if (context.ContainingSymbol is not IMethodSymbol method)
            return;

        var location = attribute.GetLocation();

        if (!method.IsStatic)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.PatchMethodMustBeStatic, location, method.Name));
        }

        var hasHarmonyKind = method.GetAttributes().Any(item =>
            HarmonyKindAttributes.Any(name => AttributeNames.Is(item.AttributeClass, name)));

        if (!hasHarmonyKind)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.PatchMethodMissingHarmonyKind, location, method.Name));
        }

        CheckTarget(context, attribute, location);
    }

    /// <summary>校验特性的目标类型与目标方法在编译期是否存在。</summary>
    /// <param name="context">语法节点分析上下文。</param>
    /// <param name="attribute">特性语法。</param>
    /// <param name="location">报告位置。</param>
    private static void CheckTarget(SyntaxNodeAnalysisContext context, AttributeSyntax attribute, Location location)
    {
        var arguments = attribute.ArgumentList?.Arguments;
        if (arguments is null || arguments.Value.Count < 2)
            return;   // 仅声明构造函数目标的重载，无需按名字查找

        var typeArgument = arguments.Value[0].Expression;
        if (typeArgument is not TypeOfExpressionSyntax typeOf)
            return;

        var targetType = context.SemanticModel.GetTypeInfo(typeOf.Type).Type;
        if (targetType is null || targetType.TypeKind == TypeKind.Error)
            return;   // 类型本身编译不过，交给编译器报错

        // 第二个参数不是字符串字面量时，说明是 MethodType 或参数类型数组，跳过名字校验。
        if (arguments.Value[1].Expression is not LiteralExpressionSyntax literal ||
            !literal.IsKind(SyntaxKind.StringLiteralExpression))
            return;

        var methodName = literal.Token.ValueText;
        if (string.IsNullOrEmpty(methodName))
            return;

        for (var current = targetType as INamedTypeSymbol; current is not null; current = current.BaseType)
        {
            if (current.GetMembers(methodName).Length > 0)
                return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Diagnostics.PatchMethodTargetMissing, location, targetType.Name, methodName));
    }
}

/// <summary>校验 [ManagedPatch] 类是否声明了目标（[HarmonyPatch] 或 IManualPatch）。</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ManagedPatchClassAnalyzer : DiagnosticAnalyzer
{
    private const string AttributeName = "ManagedPatch";
    private const string HarmonyPatchAttributeName = "HarmonyPatch";
    private const string ManualPatchInterfaceName = "IManualPatch";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Diagnostics.ManagedPatchNeedsTarget);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
    }

    /// <summary>校验单个类型。</summary>
    /// <param name="context">符号分析上下文。</param>
    private static void AnalyzeType(SymbolAnalysisContext context)
    {
        if (context.Symbol is not INamedTypeSymbol type)
            return;

        var hasManagedPatch = type.GetAttributes().Any(item => AttributeNames.Is(item.AttributeClass, AttributeName));
        if (!hasManagedPatch)
            return;

        var hasHarmonyPatch = type.GetAttributes().Any(item =>
            AttributeNames.Is(item.AttributeClass, HarmonyPatchAttributeName));
        var implementsManual = type.AllInterfaces.Any(item => item.Name == ManualPatchInterfaceName);

        if (hasHarmonyPatch || implementsManual)
            return;

        var location = type.Locations.FirstOrDefault(item => item.IsInSource);
        if (location is null)
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            Diagnostics.ManagedPatchNeedsTarget, location, type.Name));
    }
}

/// <summary>提示版本特性标在了不会被校验的成员上。</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class VersionAttributeAnalyzer : DiagnosticAnalyzer
{
    private static readonly string[] VersionAttributes = ["PatchApiVersion", "PatchGameVersion"];

    private static readonly string[] PatchAttributes =
    [
        "ManagedPatch",
        "PatchMethod",
        "HarmonyPatch",
        "HarmonyPrefix",
        "HarmonyPostfix",
        "HarmonyTranspiler",
        "HarmonyFinalizer",
        "HarmonyReversePatch",
    ];

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Diagnostics.VersionAttributeUnused);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType, SymbolKind.Method);
    }

    /// <summary>校验单个符号上的版本特性是否有意义。</summary>
    /// <param name="context">符号分析上下文。</param>
    private static void AnalyzeSymbol(SymbolAnalysisContext context)
    {
        var attributes = context.Symbol.GetAttributes();

        var hasVersion = attributes.Any(item =>
            VersionAttributes.Any(name => AttributeNames.Is(item.AttributeClass, name)));

        if (!hasVersion)
            return;

        var isPatched = attributes.Any(item =>
            PatchAttributes.Any(name => AttributeNames.Is(item.AttributeClass, name)));

        if (isPatched)
            return;

        var location = context.Symbol.Locations.FirstOrDefault(item => item.IsInSource);
        if (location is null)
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            Diagnostics.VersionAttributeUnused, location, context.Symbol.Name));
    }
}
