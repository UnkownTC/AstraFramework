#:package Microsoft.CodeAnalysis.CSharp
#:property NoWarn=$(NoWarn);CA1307;CA1861;CA1870;IDE0005
// tools/ApiDoc.cs —— docs/API.md 中「公开 API 清单」自动段的生成器。
//
// 为什么要它：docs/API.md 是「改公开签名时对照」的清单，手抄必然与源码漂移。
// 本脚本用 Roslyn 语法树扫描 src/Astra.Framework/**/*.cs，把 public / protected 的
// 类型与成员连同 XML 摘要一并渲染成 Markdown，只替换 docs/API.md 里
// AUTO-GEN 标记之间的内容，标记之外的正文（用法说明）不受影响。
//
// 用法（仓库根目录执行，需 .NET 10 SDK；包版本取自 Directory.Packages.props 的中央版本）：
//   dotnet run tools/ApiDoc.cs                 # 重新生成自动段
//   dotnet run tools/ApiDoc.cs -- --check      # 只校验：与源码不一致则退出码 1
//
// 约定：
//   * 输出必须是确定性的（同源码 → 同字节），故不写入时间戳；内容变化才产生 diff。
//   * 判定口径见 docs/STANDARD.md §4：public 为对外 API，protected 因可被子类使用同样收录；
//     internal / private / file / private protected 不属对外 API，不收录。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

const string BeginMarker = "<!-- AUTO-GEN:BEGIN（由 tools/ApiDoc.cs 生成，勿手改） -->";
const string EndMarker = "<!-- AUTO-GEN:END -->";

var commandLine = args;
return Run();

int Run()
{
    var check = commandLine.Any(argument => string.Equals(argument, "--check", StringComparison.Ordinal));

    var root = FindRepoRoot(Directory.GetCurrentDirectory());
    if (root is null)
    {
        Console.Error.WriteLine("[ApiDoc] 找不到仓库根（应含 AGENTS.md）。请在仓库根目录执行。");
        return 2;
    }

    var srcDir = Path.Combine(root, "src", "Astra.Framework");
    var docPath = Path.Combine(root, "docs", "API.md");
    if (!Directory.Exists(srcDir))
    {
        Console.Error.WriteLine($"[ApiDoc] 源码目录不存在：{srcDir}");
        return 2;
    }

    if (!File.Exists(docPath))
    {
        Console.Error.WriteLine($"[ApiDoc] 目标文档不存在：{docPath}");
        return 2;
    }

    var collected = CollectTypes(root, srcDir);
    var generated = Render(collected);

    var existing = NormalizeNewlines(File.ReadAllText(docPath, Encoding.UTF8));
    var beginIndex = existing.IndexOf(BeginMarker, StringComparison.Ordinal);
    var endIndex = existing.IndexOf(EndMarker, StringComparison.Ordinal);
    if (beginIndex < 0 || endIndex < 0 || endIndex < beginIndex)
    {
        Console.Error.WriteLine("[ApiDoc] docs/API.md 缺少 AUTO-GEN 标记，未写入。");
        return 2;
    }

    var head = existing[..(beginIndex + BeginMarker.Length)];
    var tail = existing[endIndex..];
    var updated = NormalizeNewlines(head + "\n\n" + generated + "\n\n" + tail);

    if (check)
    {
        if (string.Equals(updated, existing, StringComparison.Ordinal))
        {
            Console.WriteLine("[ApiDoc] 校验通过：docs/API.md 与源码一致。");
            return 0;
        }

        var line = FirstDifference(existing, updated);
        Console.Error.WriteLine($"[ApiDoc] 校验失败：docs/API.md 与源码不一致（首个差异在文档第 {line} 行）。");
        Console.Error.WriteLine("[ApiDoc] 执行 `dotnet run tools/ApiDoc.cs` 重新生成后提交。");
        return 1;
    }

    File.WriteAllText(docPath, updated, new UTF8Encoding(false));
    Console.WriteLine($"[ApiDoc] 已更新 {Path.GetRelativePath(root, docPath)}："
        + $"公开类型 {collected.PublicTypes.Count} 个。");
    return 0;
}

static string? FindRepoRoot(string start)
{
    var directory = new DirectoryInfo(start);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            return directory.FullName;
        directory = directory.Parent;
    }

    return null;
}

// ---------------------------------------------------------------- 采集

static Collected CollectTypes(string root, string srcDir)
{
    var collected = new Collected();

    var files = Directory.EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories)
        .Select(path => Path.GetFullPath(path))
        .Where(path => !IsUnderBuildDirectory(path))
        .OrderBy(path => path, StringComparer.Ordinal);

    foreach (var file in files)
    {
        var text = File.ReadAllText(file, Encoding.UTF8);
        var tree = CSharpSyntaxTree.ParseText(
            text,
            new CSharpParseOptions(LanguageVersion.Latest),
            path: file);
        var relativeFile = Path.GetRelativePath(root, file).Replace('\\', '/');

        Walk(tree.GetRoot(), string.Empty, parentIsPublic: true, relativeFile, collected);
    }

    return collected;
}

static bool IsUnderBuildDirectory(string path)
{
    var normalized = path.Replace('\\', '/');
    return normalized.Contains("/obj/", StringComparison.Ordinal)
        || normalized.Contains("/bin/", StringComparison.Ordinal);
}

static void Walk(SyntaxNode node, string ns, bool parentIsPublic, string relativeFile, Collected collected)
{
    foreach (var child in node.ChildNodes())
    {
        switch (child)
        {
            case BaseNamespaceDeclarationSyntax namespaceDeclaration:
            {
                var name = namespaceDeclaration.Name.ToString();
                var fullName = string.IsNullOrEmpty(ns) ? name : ns + "." + name;
                Walk(namespaceDeclaration, fullName, parentIsPublic, relativeFile, collected);
                break;
            }

            case TypeDeclarationSyntax typeDeclaration:
                HandleType(typeDeclaration, ns, parentIsPublic, relativeFile, collected);
                break;

            case EnumDeclarationSyntax enumDeclaration:
                HandleEnum(enumDeclaration, ns, parentIsPublic, relativeFile, collected);
                break;

            case DelegateDeclarationSyntax delegateDeclaration:
                HandleDelegate(delegateDeclaration, ns, parentIsPublic, relativeFile, collected);
                break;
        }
    }
}

static void HandleType(TypeDeclarationSyntax declaration, string ns, bool parentIsPublic, string relativeFile,
    Collected collected)
{
    var accessibility = AccessibilityText(declaration.Modifiers, defaultPublic: false);
    var isPublicApi = parentIsPublic && accessibility == "public";

    var info = new TypeInfo
    {
        Namespace = ns,
        Name = declaration.Identifier.Text + TypeParameterList(declaration.TypeParameterList),
        Kind = TypeKind(declaration),
        Accessibility = accessibility,
        BaseList = BaseListOf(declaration.BaseList),
        Constraints = Constraints(declaration.ConstraintClauses),
        RecordParameterList = declaration is RecordDeclarationSyntax { ParameterList: { } parameters }
            ? Compress(parameters.ToString())
            : null,
        Summary = SummaryOf(declaration),
        Attributes = AttributesOf(declaration),
        File = relativeFile,
    };

    foreach (var kind in new[]
             {
                 SyntaxKind.StaticKeyword, SyntaxKind.AbstractKeyword, SyntaxKind.SealedKeyword,
                 SyntaxKind.ReadOnlyKeyword, SyntaxKind.RefKeyword, SyntaxKind.UnsafeKeyword,
             })
    {
        if (declaration.Modifiers.Any(kind))
            info.Modifiers.Add(declaration.Modifiers.First(token => token.IsKind(kind)).Text);
    }

    CollectMembers(declaration, info, declaration is InterfaceDeclarationSyntax);

    if (isPublicApi)
        collected.PublicTypes.Add(info);

    foreach (var nested in declaration.Members.OfType<TypeDeclarationSyntax>())
        HandleNested(nested, ns, info.Name, isPublicApi, relativeFile, collected);
}

static void HandleNested(TypeDeclarationSyntax declaration, string ns, string parentName, bool parentIsPublic,
    string relativeFile, Collected collected)
{
    var accessibility = AccessibilityText(declaration.Modifiers, defaultPublic: false);
    var isPublicApi = parentIsPublic && accessibility == "public";
    var name = parentName + "." + declaration.Identifier.Text + TypeParameterList(declaration.TypeParameterList);

    var info = new TypeInfo
    {
        Namespace = ns,
        Name = name,
        Kind = "嵌套 " + TypeKind(declaration),
        Accessibility = accessibility,
        BaseList = BaseListOf(declaration.BaseList),
        Constraints = Constraints(declaration.ConstraintClauses),
        Summary = SummaryOf(declaration),
        Attributes = AttributesOf(declaration),
        File = relativeFile,
    };

    CollectMembers(declaration, info, declaration is InterfaceDeclarationSyntax);

    if (isPublicApi)
        collected.PublicTypes.Add(info);

    foreach (var nested in declaration.Members.OfType<TypeDeclarationSyntax>())
        HandleNested(nested, ns, name, isPublicApi, relativeFile, collected);
}

static void HandleEnum(EnumDeclarationSyntax declaration, string ns, bool parentIsPublic, string relativeFile,
    Collected collected)
{
    var accessibility = AccessibilityText(declaration.Modifiers, defaultPublic: false);
    var isPublicApi = parentIsPublic && accessibility == "public";

    var info = new TypeInfo
    {
        Namespace = ns,
        Name = declaration.Identifier.Text,
        Kind = "enum",
        Accessibility = accessibility,
        BaseList = BaseListOf(declaration.BaseList),
        Summary = SummaryOf(declaration),
        Attributes = AttributesOf(declaration),
        File = relativeFile,
    };

    var order = 0;
    foreach (var member in declaration.Members)
    {
        info.Members.Add(new MemberInfo
        {
            Signature = member.Identifier.Text
                        + (member.EqualsValue is null
                            ? string.Empty
                            : " = " + Compress(member.EqualsValue.Value.ToString())),
            Summary = SummaryOf(member),
            KindOrder = 0,
            Position = order++,
        });
    }

    if (isPublicApi)
        collected.PublicTypes.Add(info);
}

static void HandleDelegate(DelegateDeclarationSyntax declaration, string ns, bool parentIsPublic, string relativeFile,
    Collected collected)
{
    var accessibility = AccessibilityText(declaration.Modifiers, defaultPublic: false);
    var isPublicApi = parentIsPublic && accessibility == "public";

    var info = new TypeInfo
    {
        Namespace = ns,
        Name = declaration.Identifier.Text + TypeParameterList(declaration.TypeParameterList),
        Kind = "delegate",
        Accessibility = accessibility,
        Constraints = Constraints(declaration.ConstraintClauses),
        Summary = SummaryOf(declaration),
        File = relativeFile,
    };

    var signature = new StringBuilder();
    signature.Append(accessibility).Append(" delegate ");
    signature.Append(Compress(declaration.ReturnType.ToString())).Append(' ');
    signature.Append(declaration.Identifier.Text);
    signature.Append(TypeParameterList(declaration.TypeParameterList));
    signature.Append(Compress(declaration.ParameterList.ToString()));
    if (!string.IsNullOrEmpty(info.Constraints))
        signature.Append(' ').Append(info.Constraints);

    info.Members.Add(new MemberInfo
    {
        Signature = Compress(signature.ToString()),
        KindOrder = 0,
        Position = 0,
    });

    if (isPublicApi)
        collected.PublicTypes.Add(info);
}

static void CollectMembers(TypeDeclarationSyntax declaration, TypeInfo info, bool isInterface)
{
    foreach (var member in declaration.Members)
    {
        switch (member)
        {
            case MethodDeclarationSyntax method:
            {
                if (method.ExplicitInterfaceSpecifier is not null || !IncludeMember(method.Modifiers, isInterface))
                    break;

                info.Members.Add(Member(
                    SignatureOf(method, method.Body, method.ExpressionBody?.ArrowToken),
                    method,
                    kindOrder: 5));
                break;
            }

            case ConstructorDeclarationSyntax constructor:
            {
                if (constructor.Modifiers.Any(SyntaxKind.StaticKeyword)
                    || !IncludeMember(constructor.Modifiers, isInterface))
                    break;

                info.Members.Add(Member(
                    SignatureOf(constructor, constructor.Body, constructor.ExpressionBody?.ArrowToken),
                    constructor,
                    kindOrder: 0));
                break;
            }

            case PropertyDeclarationSyntax property:
            {
                if (property.ExplicitInterfaceSpecifier is not null
                    || !IncludeMember(property.Modifiers, isInterface))
                    break;

                var signature = new StringBuilder();
                signature.Append(ModifiersText(property.Modifiers)).Append(' ');
                signature.Append(Compress(property.Type.ToString())).Append(' ').Append(property.Identifier.Text);
                signature.Append(property.AccessorList is null
                    ? " { get; }"
                    : AccessorText(property.AccessorList));

                info.Members.Add(Member(Compress(signature.ToString()), property, kindOrder: 2));
                break;
            }

            case IndexerDeclarationSyntax indexer:
            {
                if (indexer.ExplicitInterfaceSpecifier is not null
                    || !IncludeMember(indexer.Modifiers, isInterface))
                    break;

                var signature = new StringBuilder();
                signature.Append(ModifiersText(indexer.Modifiers)).Append(' ');
                signature.Append(Compress(indexer.Type.ToString())).Append(" this[");
                signature.Append(Compress(indexer.ParameterList.ToString()).TrimStart('[').TrimEnd(']'))
                    .Append(']');
                signature.Append(indexer.AccessorList is null ? " { get; }" : AccessorText(indexer.AccessorList));

                info.Members.Add(Member(Compress(signature.ToString()), indexer, kindOrder: 3));
                break;
            }

            case FieldDeclarationSyntax field:
            {
                if (!IncludeMember(field.Modifiers, isInterface))
                    break;

                var modifiers = ModifiersText(field.Modifiers);
                var type = Compress(field.Declaration.Type.ToString());
                var isConst = field.Modifiers.Any(SyntaxKind.ConstKeyword);
                var order = 0;
                foreach (var variable in field.Declaration.Variables)
                {
                    var signature = $"{modifiers} {type} {variable.Identifier.Text}".Trim();
                    if (isConst && variable.Initializer is not null)
                        signature += " = " + Compress(variable.Initializer.Value.ToString());

                    info.Members.Add(Member(signature, variable, kindOrder: 1, positionOffset: order++));
                }

                break;
            }

            case EventFieldDeclarationSyntax eventField:
            {
                if (!IncludeMember(eventField.Modifiers, isInterface))
                    break;

                var modifiers = ModifiersText(eventField.Modifiers);
                var type = Compress(eventField.Declaration.Type.ToString());
                var order = 0;
                foreach (var variable in eventField.Declaration.Variables)
                {
                    info.Members.Add(Member($"{(modifiers + " event").Trim()} {type} {variable.Identifier.Text}",
                        variable, kindOrder: 4, positionOffset: order++));
                }

                break;
            }

            case EventDeclarationSyntax eventDeclaration:
            {
                if (!IncludeMember(eventDeclaration.Modifiers, isInterface))
                    break;

                var signature = new StringBuilder();
                signature.Append(ModifiersText(eventDeclaration.Modifiers)).Append(" event ");
                signature.Append(Compress(eventDeclaration.Type.ToString())).Append(' ')
                    .Append(eventDeclaration.Identifier.Text);
                if (eventDeclaration.AccessorList is not null)
                    signature.Append(AccessorText(eventDeclaration.AccessorList));

                info.Members.Add(Member(Compress(signature.ToString()), eventDeclaration, kindOrder: 4));
                break;
            }

            case OperatorDeclarationSyntax operatorDeclaration:
            {
                if (!IncludeMember(operatorDeclaration.Modifiers, isInterface))
                    break;

                info.Members.Add(Member(
                    SignatureOf(operatorDeclaration, operatorDeclaration.Body,
                        operatorDeclaration.ExpressionBody?.ArrowToken),
                    operatorDeclaration,
                    kindOrder: 6));
                break;
            }

            case ConversionOperatorDeclarationSyntax conversion:
            {
                if (!IncludeMember(conversion.Modifiers, isInterface))
                    break;

                info.Members.Add(Member(
                    SignatureOf(conversion, conversion.Body, conversion.ExpressionBody?.ArrowToken),
                    conversion,
                    kindOrder: 6));
                break;
            }
        }
    }
}

static MemberInfo Member(string signature, SyntaxNode node, int kindOrder, int positionOffset = 0)
{
    return new MemberInfo
    {
        Signature = signature,
        Summary = SummaryOf(node),
        KindOrder = kindOrder,
        Position = node.SpanStart + positionOffset,
    };
}

static bool IncludeMember(SyntaxTokenList modifiers, bool isInterface)
{
    var accessibility = AccessibilityText(modifiers, defaultPublic: isInterface);
    return accessibility is "public" or "protected" or "protected internal";
}

static string SignatureOf(SyntaxNode node, BlockSyntax? body, SyntaxToken? arrow)
{
    var start = SignatureStart(node);
    var text = node.SyntaxTree.GetText();

    if (body is not null)
        return Compress(text.ToString(Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(start, body.SpanStart)));

    if (arrow is { } arrowToken)
        return Compress(text.ToString(
            Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(start, arrowToken.SpanStart)));

    var signature = text.ToString(
        Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(start, node.Span.End));
    var semicolon = signature.LastIndexOf(';');
    if (semicolon >= 0)
        signature = signature[..semicolon];

    return Compress(signature);
}

// 签名不含成员上的特性（如 [CanBeNull]）：从最后一个特性列表之后起截。
static int SignatureStart(SyntaxNode node)
{
    var attributeLists = node.ChildNodes().OfType<AttributeListSyntax>().ToList();
    return attributeLists.Count == 0 ? node.SpanStart : attributeLists[^1].Span.End;
}

static string AccessorText(AccessorListSyntax list)
{
    var parts = new List<string>();
    foreach (var accessor in list.Accessors)
    {
        var modifiers = ModifiersText(accessor.Modifiers);
        parts.Add((modifiers.Length > 0 ? modifiers + " " : string.Empty) + accessor.Keyword.Text + ";");
    }

    return " { " + string.Join(" ", parts) + " }";
}

static string ModifiersText(SyntaxTokenList modifiers)
{
    return string.Join(" ", modifiers.Select(token => token.Text));
}

static string? BaseListOf(BaseListSyntax? baseList)
{
    if (baseList is null)
        return null;

    var text = Compress(baseList.ToString());
    return text.TrimStart(':', ' ');
}

static string TypeKind(TypeDeclarationSyntax declaration)
{
    return declaration switch
    {
        InterfaceDeclarationSyntax => "interface",
        StructDeclarationSyntax => "struct",
        RecordDeclarationSyntax record => record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword)
            ? "record struct"
            : "record",
        _ => "class",
    };
}

static string TypeParameterList(TypeParameterListSyntax? list)
{
    return list is null ? string.Empty : Compress(list.ToString());
}

static string? Constraints(IEnumerable<TypeParameterConstraintClauseSyntax> clauses)
{
    var text = string.Join(" ", clauses.Select(clause => Compress(clause.ToString())));
    return text.Length == 0 ? null : text;
}

static List<string> AttributesOf(SyntaxNode node)
{
    return node.ChildNodes()
        .OfType<AttributeListSyntax>()
        .SelectMany(list => list.Attributes)
        .Select(attribute => Compress(attribute.ToString()))
        .ToList();
}

static string AccessibilityText(SyntaxTokenList modifiers, bool defaultPublic)
{
    var isPublic = modifiers.Any(SyntaxKind.PublicKeyword);
    var isProtected = modifiers.Any(SyntaxKind.ProtectedKeyword);
    var isInternal = modifiers.Any(SyntaxKind.InternalKeyword);
    var isPrivate = modifiers.Any(SyntaxKind.PrivateKeyword);

    if (modifiers.Any(SyntaxKind.FileKeyword))
        return "file";
    if (isPublic && isProtected)
        return "protected internal";
    if (isPublic)
        return "public";
    if (isProtected && isInternal)
        return "protected internal";
    if (isProtected && isPrivate)
        return "private protected";
    if (isProtected)
        return "protected";
    if (isInternal)
        return "internal";
    if (isPrivate)
        return "private";
    return defaultPublic ? "public" : "private";
}

// ---------------------------------------------------------------- 文档注释

static string? SummaryOf(SyntaxNode node)
{
    foreach (var trivia in node.GetLeadingTrivia())
    {
        if (!trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
            && !trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            continue;

        var text = trivia.ToFullString();
        text = Regex.Replace(text, @"^\s*///\s?", string.Empty, RegexOptions.Multiline);
        if (trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            text = Regex.Replace(text, @"^\s*\*\s?", string.Empty, RegexOptions.Multiline);

        var match = Regex.Match(text, "<summary>(.*?)</summary>", RegexOptions.Singleline);
        if (!match.Success)
            return null;

        var cleaned = CleanXml(match.Groups[1].Value);
        return cleaned.Length == 0 ? null : cleaned;
    }

    return null;
}

static string CleanXml(string text)
{
    text = text.Replace("<summary>", string.Empty, StringComparison.Ordinal);
    text = Regex.Replace(text, @"<code>.*?</code>", string.Empty, RegexOptions.Singleline);
    text = Regex.Replace(text, @"<see cref=""([^""]+)""\s*/>",
        match => Regex.Replace(match.Groups[1].Value, @"^.*\.", string.Empty));
    text = Regex.Replace(text, @"<see langword=""([^""]+)""\s*/>", "$1");
    text = Regex.Replace(text, @"<(?:typeparamref|paramref) name=""([^""]+)""\s*/>", "$1");
    text = Regex.Replace(text,
        @"</?(?:c|b|i|para|remarks|inheritdoc|list|item|description|term|seealso)[^>]*>",
        string.Empty);
    text = Regex.Replace(text, @"<[^>]+>", string.Empty);
    text = System.Net.WebUtility.HtmlDecode(text);
    return Compress(text);
}

static string Compress(string text) => DocText.Compress(text);

// ---------------------------------------------------------------- 渲染

static string Render(Collected collected)
{
    var publicTypes = collected.PublicTypes
        .OrderBy(type => type.Namespace, StringComparer.Ordinal)
        .ThenBy(type => type.Name, StringComparer.Ordinal)
        .ToList();

    var builder = new StringBuilder();

    builder.AppendLine("> 本节由 `tools/ApiDoc.cs` 扫描 `src/Astra.Framework/**/*.cs` 生成，**请勿手改**；改动公开签名后重新生成。");
    builder.AppendLine();
    builder.Append("- **统计**：公开类型 ").Append(publicTypes.Count).Append(" 个（")
        .Append(string.Join(" / ", publicTypes.GroupBy(type => type.Kind)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Key + " " + group.Count())))
        .Append("），命名空间 ").Append(publicTypes.Select(type => type.Namespace).Distinct().Count())
        .Append(" 个；公开成员 ").Append(publicTypes.Sum(type => type.Members.Count)).Append(" 个。").AppendLine();
    builder.Append("- **口径**：`public` 与 `protected` 成员均属对外 API（后者可被子类使用）；")
        .Append("`internal` / `private` / `file` / `private protected` 不收录。")
        .AppendLine();

    builder.AppendLine();
    builder.AppendLine("### 命名空间索引");
    builder.AppendLine();
    builder.AppendLine("| 命名空间 | 公开类型 |");
    builder.AppendLine("| --- | --- |");
    foreach (var group in publicTypes.GroupBy(type => type.Namespace, StringComparer.Ordinal))
    {
        builder.Append("| `").Append(group.Key).Append("` | ")
            .Append(string.Join("、", group.Select(type => "`" + type.Name + "`")))
            .AppendLine(" |");
    }

    builder.AppendLine();
    builder.AppendLine("---");

    foreach (var group in publicTypes.GroupBy(type => type.Namespace, StringComparer.Ordinal))
    {
        builder.AppendLine();
        builder.Append("### `").Append(group.Key).Append('`').AppendLine();
        builder.AppendLine();

        foreach (var type in group)
        {
            builder.Append("#### `").Append(type.Name).Append('`').AppendLine();
            builder.AppendLine();
            builder.Append('`').Append(type.Signature).Append('`').AppendLine();
            builder.AppendLine();

            if (type.Attributes.Count > 0)
            {
                builder.Append("特性：")
                    .Append(string.Join("、", type.Attributes.Select(attribute => "`[" + attribute + "]`")))
                    .AppendLine();
                builder.AppendLine();
            }

            builder.Append("源码：`").Append(type.File).Append('`').AppendLine();
            builder.AppendLine();

            if (!string.IsNullOrEmpty(type.Summary))
            {
                builder.Append("> ").Append(type.Summary).AppendLine();
                builder.AppendLine();
            }

            var members = type.Members
                .OrderBy(member => member.KindOrder)
                .ThenBy(member => member.Position)
                .ToList();

            if (members.Count == 0)
            {
                builder.AppendLine("（无公开成员）");
                builder.AppendLine();
                continue;
            }

            foreach (var member in members)
            {
                builder.Append("- `").Append(member.Signature).Append('`');
                if (!string.IsNullOrEmpty(member.Summary))
                    builder.Append(" — ").Append(member.Summary);
                builder.AppendLine();
            }

            builder.AppendLine();
        }
    }

    return builder.ToString().TrimEnd();
}

static int FirstDifference(string left, string right)
{
    var leftLines = left.Split('\n');
    var rightLines = right.Split('\n');
    var count = Math.Min(leftLines.Length, rightLines.Length);
    for (var index = 0; index < count; index++)
    {
        if (!string.Equals(leftLines[index], rightLines[index], StringComparison.Ordinal))
            return index + 1;
    }

    return count + 1;
}

static string NormalizeNewlines(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

// ---------------------------------------------------------------- 数据模型

sealed class TypeInfo
{
    public string Namespace = string.Empty;
    public string Name = string.Empty;
    public string Kind = string.Empty;
    public string Accessibility = string.Empty;
    public List<string> Modifiers = new();
    public string? BaseList;
    public string? Constraints;
    public string? RecordParameterList;
    public List<string> Attributes = new();
    public string? Summary;
    public string File = string.Empty;
    public readonly List<MemberInfo> Members = new();

    public string Signature
    {
        get
        {
            var builder = new StringBuilder();
            builder.Append(Accessibility).Append(' ');
            foreach (var modifier in Modifiers)
                builder.Append(modifier).Append(' ');
            builder.Append(Kind).Append(' ').Append(Name);
            if (!string.IsNullOrEmpty(RecordParameterList))
                builder.Append(RecordParameterList);
            if (!string.IsNullOrEmpty(BaseList))
                builder.Append(" : ").Append(BaseList);
            if (!string.IsNullOrEmpty(Constraints))
                builder.Append(' ').Append(Constraints);
            return DocText.Compress(builder.ToString());
        }
    }
}

sealed class MemberInfo
{
    public string Signature = string.Empty;
    public string? Summary;
    public int KindOrder;
    public int Position;
}

sealed class Collected
{
    public readonly List<TypeInfo> PublicTypes = new();
}

static class DocText
{
    public static string Compress(string text)
    {
        text = Regex.Replace(text, @"\s+", " ").Trim();
        text = Regex.Replace(text, @"\s+([,;\)\]])", "$1");
        text = Regex.Replace(text, @"([\(\[])\s+", "$1");
        text = Regex.Replace(text, @",(?=\S)", ", ");
        return text.Trim();
    }
}
