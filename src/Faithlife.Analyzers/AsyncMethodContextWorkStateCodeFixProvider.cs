using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Faithlife.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AsyncMethodContextWorkStateCodeFixProvider)), Shared]
public sealed class AsyncMethodContextWorkStateCodeFixProvider : CodeFixProvider
{
	public override ImmutableArray<string> FixableDiagnosticIds => [AsyncMethodContextWorkStateAnalyzer.DiagnosticId];

	public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

		var diagnostic = context.Diagnostics.First();
		var diagnosticSpan = diagnostic.Location.SourceSpan;

		if (root.FindNode(diagnosticSpan) is not InvocationExpressionSyntax invocation)
			return;

		// extract the context expression from WorkState.FromCancellationToken(context.CancellationToken)
		if (invocation.ArgumentList.Arguments.Count != 1 ||
			invocation.ArgumentList.Arguments[0].Expression is not MemberAccessExpressionSyntax { Name.Identifier.Text: "CancellationToken" } cancellationTokenAccess)
		{
			return;
		}

		// create the replacement: context.WorkState
		var contextExpression = cancellationTokenAccess.Expression;
		var replacementExpression = MemberAccessExpression(
			SyntaxKind.SimpleMemberAccessExpression,
			contextExpression,
			IdentifierName("WorkState"));

		context.RegisterCodeFix(
			CodeAction.Create(
				title: $"Use '{contextExpression}.WorkState'",
				createChangedDocument: token => ReplaceInvocationAsync(context.Document, invocation, replacementExpression, token),
				"use-context-workstate"),
			diagnostic);
	}

	private static async Task<Document> ReplaceInvocationAsync(Document document, InvocationExpressionSyntax invocation, ExpressionSyntax replacementExpression, CancellationToken cancellationToken)
	{
		var root = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))
			.ReplaceNode(invocation, replacementExpression.WithTriviaFrom(invocation));
		return await Simplifier.ReduceAsync(document.WithSyntaxRoot(root), cancellationToken: cancellationToken).ConfigureAwait(false);
	}
}
