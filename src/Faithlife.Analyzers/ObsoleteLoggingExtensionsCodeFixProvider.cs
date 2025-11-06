using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Faithlife.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ObsoleteLoggingExtensionsCodeFixProvider)), Shared]
public sealed class ObsoleteLoggingExtensionsCodeFixProvider : CodeFixProvider
{
	public sealed override ImmutableArray<string> FixableDiagnosticIds => [ObsoleteLoggingExtensionsAnalyzer.DiagnosticId];

	public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		if (root is null)
			return;

		var diagnostic = context.Diagnostics.First();
		var diagnosticSpan = diagnostic.Location.SourceSpan;

		var diagnosticNode = root.FindNode(diagnosticSpan);
		var invocation = diagnosticNode.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault();
		if (invocation is null)
			return;

		if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
			return;

		var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
		if (semanticModel is null)
			return;

		if (semanticModel.GetSymbolInfo(invocation.Expression).Symbol is not IMethodSymbol methodSymbol)
			return;

		var oldMethodName = methodSymbol.Name;
		var newMethodName = GetReplacementMethodName(oldMethodName);

		context.RegisterCodeFix(
			CodeAction.Create(
				title: $"Replace with '{newMethodName}'",
				createChangedDocument: token => ReplaceMethodAsync(context.Document, invocation, memberAccess, newMethodName, token),
				"replace-with-ilogger-method"),
			diagnostic);
	}

	private static string GetReplacementMethodName(string obsoleteMethodName) =>
		obsoleteMethodName switch
		{
			"Debug" => "LogDebug",
			"Info" => "LogInformation",
			"Warn" => "LogWarning",
			"Error" => "LogError",
			"Fatal" => "LogCritical",
			_ => obsoleteMethodName,
		};

	private static async Task<Document> ReplaceMethodAsync(Document document, InvocationExpressionSyntax invocation, MemberAccessExpressionSyntax memberAccess, string newMethodName, CancellationToken cancellationToken)
	{
		// Create new member access with the replacement method name
		var newMemberAccess = memberAccess.WithName(IdentifierName(newMethodName));
		var newInvocation = invocation.WithExpression(newMemberAccess);

		var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		root = root.ReplaceNode(invocation, newInvocation);

		// Try to remove the obsolete using directive if it's no longer needed
		var usingDirective = root.DescendantNodes()
			.OfType<UsingDirectiveSyntax>()
			.FirstOrDefault(x => AreEquivalent(x.Name, s_logosCommonLoggingNamespace));

		if (usingDirective != null)
			root = root.ReplaceNode(usingDirective, usingDirective.WithAdditionalAnnotations(Simplifier.Annotation));

		return await Simplifier.ReduceAsync(
			await Simplifier.ReduceAsync(document.WithSyntaxRoot(root), cancellationToken: cancellationToken).ConfigureAwait(false),
			cancellationToken: cancellationToken).ConfigureAwait(false);
	}

	private static readonly NameSyntax s_logosCommonLoggingNamespace = ParseName("Logos.Common.Logging");
}
