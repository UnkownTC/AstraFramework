using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Astra.Analyzers;

/// <summary>校验 [EventMethod] 的用法：可访问性、override 约定、与目标事件签名是否匹配。</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EventMethodAnalyzer : DiagnosticAnalyzer
{
    private const string AttributeName = "EventMethod";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        Diagnostics.EventMethodMustOverride,
        Diagnostics.EventMethodSignature,
        Diagnostics.EventMethodAccessibility,
        Diagnostics.EventMethodTargetMissing);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.Attribute);
    }

    /// <summary>校验单个 [EventMethod] 标注。</summary>
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

        if (method.DeclaredAccessibility != Accessibility.Public)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.EventMethodAccessibility, location, method.Name));
        }

        // 静态方法走类型扫描注册，不要求 override；实例方法必须是 override 或本身是虚方法声明点。
        if (!method.IsStatic &&
            method.OverriddenMethod is null &&
            !method.IsVirtual &&
            !method.IsAbstract)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.EventMethodMustOverride, location, method.Name));
        }

        CheckEventSignature(context, attribute, method, location);
    }

    /// <summary>校验方法签名是否与目标事件的委托签名一致。</summary>
    /// <param name="context">语法节点分析上下文。</param>
    /// <param name="attribute">特性语法。</param>
    /// <param name="method">被标注的方法。</param>
    /// <param name="location">报告位置。</param>
    private static void CheckEventSignature(SyntaxNodeAnalysisContext context, AttributeSyntax attribute,
        IMethodSymbol method, Location location)
    {
        var arguments = attribute.ArgumentList?.Arguments;
        if (arguments is null || arguments.Value.Count < 2)
            return;

        if (arguments.Value[0].Expression is not TypeOfExpressionSyntax typeOf ||
            arguments.Value[1].Expression is not LiteralExpressionSyntax literal ||
            !literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return;
        }

        if (context.SemanticModel.GetTypeInfo(typeOf.Type).Type is not INamedTypeSymbol ownerType)
            return;

        var eventName = literal.Token.ValueText;
        var eventSymbol = FindEvent(ownerType, eventName);

        if (eventSymbol is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.EventMethodTargetMissing, location, ownerType.Name, eventName, method.Name));
            return;
        }

        if (eventSymbol.Type is not INamedTypeSymbol { DelegateInvokeMethod: { } invoke })
            return;

        var expected = invoke.Parameters;
        var actual = method.Parameters;

        var matches = expected.Length == actual.Length &&
                      expected.Zip(actual, (left, right) =>
                              context.Compilation.ClassifyConversion(right.Type, left.Type).IsImplicit)
                          .All(ok => ok);

        if (matches)
            return;

        var expectedText = $"{eventName}({string.Join(", ", expected.Select(p => p.Type.ToDisplayString()))})";
        context.ReportDiagnostic(Diagnostic.Create(
            Diagnostics.EventMethodSignature, location, method.Name, expectedText));
    }

    /// <summary>在类型及其基类型中查找事件。</summary>
    /// <param name="ownerType">声明事件的类型。</param>
    /// <param name="eventName">事件名。</param>
    /// <returns>找到的事件符号；找不到时为 <see langword="null"/>。</returns>
    private static IEventSymbol? FindEvent(INamedTypeSymbol ownerType, string eventName)
    {
        for (var current = ownerType; current is not null; current = current.BaseType)
        {
            var found = current.GetMembers(eventName).OfType<IEventSymbol>().FirstOrDefault();
            if (found is not null)
                return found;
        }

        return null;
    }
}
