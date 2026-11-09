using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Astra.Analyzers;

/// <summary>
/// 架构守卫：禁止框架之外的代码引用 <c>Astra.Systems</c> 内部实现。
/// </summary>
/// <remarks>
/// 框架自身的程序集（<c>Astra</c>）例外；插件应通过 <c>Astra.Apis</c> 调用，
/// 通过 <c>Astra.Abstractions</c> 扩展。
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ArchitectureAnalyzer : DiagnosticAnalyzer
{
    private const string SystemsNamespace = "Astra.Systems";
    private const string FrameworkAssembly = "Astra";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Diagnostics.SystemsNamespaceLeak);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeUsing, SyntaxKind.UsingDirective);
    }

    /// <summary>检查 using 指令是否引用了内部实现命名空间。</summary>
    /// <param name="context">语法节点分析上下文。</param>
    private static void AnalyzeUsing(SyntaxNodeAnalysisContext context)
    {
        // 框架自身允许引用自己的实现
        if (context.Compilation.AssemblyName == FrameworkAssembly)
            return;

        if (context.Node is not UsingDirectiveSyntax directive)
            return;

        var name = directive.Name?.ToString();
        if (name is null)
            return;

        if (!string.Equals(name, SystemsNamespace, System.StringComparison.Ordinal) &&
            !name.StartsWith(SystemsNamespace + ".", System.StringComparison.Ordinal))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Diagnostics.SystemsNamespaceLeak, directive.GetLocation(), name));
    }
}
