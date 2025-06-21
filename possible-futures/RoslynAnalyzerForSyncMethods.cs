using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AsyncOnlyGraphOperationsAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor UseAsyncMethods = new(
        id: "GRAPH0001",
        title: "Use async methods for graph operations",
        messageFormat: "Use '{0}Async()' instead of '{0}()' for graph database operations",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Graph operations should be async due to database I/O operations. " +
                    "Sync methods are not supported and may cause performance issues.");

    public static readonly DiagnosticDescriptor AddAwaitKeyword = new(
        id: "GRAPH0002",
        title: "Add await keyword for async graph operations",
        messageFormat: "Add 'await' keyword when calling async method '{0}'",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Async graph operations should be awaited to prevent blocking calls.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(UseAsyncMethods, AddAwaitKeyword);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return;

        var methodName = memberAccess.Name.Identifier.ValueText;
        var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess.Expression);

        // Check if this is operating on a graph queryable
        if (!IsGraphQueryable(symbolInfo.Symbol?.Type, context.SemanticModel))
            return;

        // Check for sync materialization methods
        if (IsSyncMaterializationMethod(methodName))
        {
            var diagnostic = Diagnostic.Create(
                UseAsyncMethods,
                memberAccess.Name.GetLocation(),
                methodName);
            context.ReportDiagnostic(diagnostic);
        }

        // Check for missing await on async methods
        if (IsAsyncMaterializationMethod(methodName) && !IsAwaited(invocation))
        {
            var diagnostic = Diagnostic.Create(
                AddAwaitKeyword,
                invocation.GetLocation(),
                methodName);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsGraphQueryable(ITypeSymbol? type, SemanticModel semanticModel)
    {
        if (type == null) return false;

        // Check if the type implements IGraphQueryable or IGraphNodeQueryable
        return type.AllInterfaces.Any(i =>
            i.Name.Contains("IGraphQueryable") ||
            i.Name.Contains("IGraphNodeQueryable") ||
            i.Name.Contains("IGraphRelationshipQueryable")) ||
            type.Name.Contains("GraphQueryable");
    }

    private static bool IsSyncMaterializationMethod(string methodName)
    {
        var syncMethods = new[]
        {
            "ToList", "ToArray", "ToDictionary", "ToLookup",
            "First", "FirstOrDefault", "Last", "LastOrDefault",
            "Single", "SingleOrDefault", "Count", "LongCount",
            "Any", "All", "Sum", "Average", "Min", "Max",
            "Contains", "ElementAt", "ElementAtOrDefault"
        };

        return syncMethods.Contains(methodName);
    }

    private static bool IsAsyncMaterializationMethod(string methodName) =>
        methodName.EndsWith("Async") && IsSyncMaterializationMethod(methodName[..^5]);

    private static bool IsAwaited(InvocationExpressionSyntax invocation)
    {
        // Check if the invocation is preceded by an await keyword
        var parent = invocation.Parent;
        while (parent != null)
        {
            if (parent is AwaitExpressionSyntax)
                return true;

            // Stop looking if we hit a statement boundary
            if (parent is StatementSyntax)
                break;

            parent = parent.Parent;
        }

        return false;
    }
}






/// Code fix
/// 
/// using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AsyncOnlyGraphOperationsCodeFixProvider)), Shared]
public class AsyncOnlyGraphOperationsCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(AsyncOnlyGraphOperationsAnalyzer.UseAsyncMethods.Id);

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        var diagnostic = context.Diagnostics.FirstOrDefault(d =>
            FixableDiagnosticIds.Contains(d.Id));
        if (diagnostic == null) return;

        var diagnosticSpan = diagnostic.Location.SourceSpan;
        var memberAccess = root?.FindToken(diagnosticSpan.Start)
            .Parent?.AncestorsAndSelf()
            .OfType<MemberAccessExpressionSyntax>()
            .FirstOrDefault();

        if (memberAccess == null) return;

        var methodName = memberAccess.Name.Identifier.ValueText;
        var asyncMethodName = $"{methodName}Async";

        var action = CodeAction.Create(
            title: $"Use {asyncMethodName}() instead",
            createChangedDocument: c => MakeAsync(context.Document, memberAccess, asyncMethodName, c),
            equivalenceKey: asyncMethodName);

        context.RegisterCodeFix(action, diagnostic);
    }

    private static async Task<Document> MakeAsync(
        Document document,
        MemberAccessExpressionSyntax memberAccess,
        string asyncMethodName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        // Replace the method name
        var newMemberAccess = memberAccess.WithName(
            SyntaxFactory.IdentifierName(asyncMethodName));

        // Find the containing invocation to add await
        var invocation = memberAccess.FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation != null)
        {
            var awaitExpression = SyntaxFactory.AwaitExpression(
                invocation.WithExpression(newMemberAccess))
                .WithLeadingTrivia(invocation.GetLeadingTrivia());

            var newRoot = root.ReplaceNode(invocation, awaitExpression);
            return document.WithSyntaxRoot(newRoot);
        }

        // Fallback: just replace the member access
        var fallbackRoot = root.ReplaceNode(memberAccess, newMemberAccess);
        return document.WithSyntaxRoot(fallbackRoot);
    }
}

// ---------------
// Code fix

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AsyncOnlyGraphOperationsCodeFixProvider)), Shared]
public class AsyncOnlyGraphOperationsCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(AsyncOnlyGraphOperationsAnalyzer.UseAsyncMethods.Id);

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        var diagnostic = context.Diagnostics.FirstOrDefault(d =>
            FixableDiagnosticIds.Contains(d.Id));
        if (diagnostic == null) return;

        var diagnosticSpan = diagnostic.Location.SourceSpan;
        var memberAccess = root?.FindToken(diagnosticSpan.Start)
            .Parent?.AncestorsAndSelf()
            .OfType<MemberAccessExpressionSyntax>()
            .FirstOrDefault();

        if (memberAccess == null) return;

        var methodName = memberAccess.Name.Identifier.ValueText;
        var asyncMethodName = $"{methodName}Async";

        var action = CodeAction.Create(
            title: $"Use {asyncMethodName}() instead",
            createChangedDocument: c => MakeAsync(context.Document, memberAccess, asyncMethodName, c),
            equivalenceKey: asyncMethodName);

        context.RegisterCodeFix(action, diagnostic);
    }

    private static async Task<Document> MakeAsync(
        Document document,
        MemberAccessExpressionSyntax memberAccess,
        string asyncMethodName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        // Replace the method name
        var newMemberAccess = memberAccess.WithName(
            SyntaxFactory.IdentifierName(asyncMethodName));

        // Find the containing invocation to add await
        var invocation = memberAccess.FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation != null)
        {
            var awaitExpression = SyntaxFactory.AwaitExpression(
                invocation.WithExpression(newMemberAccess))
                .WithLeadingTrivia(invocation.GetLeadingTrivia());

            var newRoot = root.ReplaceNode(invocation, awaitExpression);
            return document.WithSyntaxRoot(newRoot);
        }

        // Fallback: just replace the member access
        var fallbackRoot = root.ReplaceNode(memberAccess, newMemberAccess);
        return document.WithSyntaxRoot(fallbackRoot);
    }
}

/// --- 
/// Analyzer package

< Project Sdk = "Microsoft.NET.Sdk" >

  < PropertyGroup >
    < TargetFramework > netstandard2.0 </ TargetFramework >
    < IncludeBuildOutput > false </ IncludeBuildOutput >
    < GeneratePackageOnBuild > true </ GeneratePackageOnBuild >
  </ PropertyGroup >

  < ItemGroup >
    < PackageReference Include = "Microsoft.CodeAnalysis.CSharp" Version = "4.5.0" PrivateAssets = "all" />
    < PackageReference Include = "Microsoft.CodeAnalysis.Analyzers" Version = "3.3.4" PrivateAssets = "all" />
  </ ItemGroup >

  < ItemGroup >
    < Analyzer Include = "AsyncOnlyGraphOperationsAnalyzer.cs" />
    < Analyzer Include = "AsyncOnlyGraphOperationsCodeFixProvider.cs" />
  </ ItemGroup >

</ Project >

