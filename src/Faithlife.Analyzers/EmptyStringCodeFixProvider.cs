using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Faithlife.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CurrentAsyncWorkItemCodeFixProvider)), Shared]
public sealed class EmptyStringCodeFixProvider : CodeFixProvider
{
	public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(EmptyStringAnalyzer.DiagnosticId);

	public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		if (root is null)
			return;

		var diagnostic = context.Diagnostics.First();
		var diagnosticSpan = diagnostic.Location.SourceSpan;

		var diagnosticNode = root.FindNode(diagnosticSpan);
		var memberAccess = diagnosticNode.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>().FirstOrDefault();
		if (memberAccess is null)
			return;

		context.RegisterCodeFix(
			CodeAction.Create(
				title: "Use \"\"",
				createChangedDocument: token => ReplaceValueAsync(context.Document, memberAccess, s_emptyStringExpression, token),
				"use-empty-string-literal"),
			diagnostic);
	}

	private static async Task<Document> ReplaceValueAsync(Document document, MemberAccessExpressionSyntax memberAccess, ExpressionSyntax replacementExpression, CancellationToken cancellationToken)
	{
		var root = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))
			.ReplaceNode(memberAccess, replacementExpression);

		return document.WithSyntaxRoot(root);
	}

	private static readonly ExpressionSyntax s_emptyStringExpression = ParseExpression("\"\"");
}
