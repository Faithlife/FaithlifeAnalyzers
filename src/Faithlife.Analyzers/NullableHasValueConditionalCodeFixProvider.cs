using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Faithlife.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NullableHasValueConditionalCodeFixProvider)), Shared]
public sealed class NullableHasValueConditionalCodeFixProvider : CodeFixProvider
{
	public sealed override ImmutableArray<string> FixableDiagnosticIds => [NullableHasValueConditionalAnalyzer.DiagnosticId];

	public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

		var diagnostic = context.Diagnostics.First();
		var diagnosticSpan = diagnostic.Location.SourceSpan;

		if (root.FindNode(diagnosticSpan, getInnermostNodeForTie: true) is not ConditionalExpressionSyntax conditionalExpression)
			return;

		if (TryExtractNullableExpression(conditionalExpression) is not { } nullableExpression)
			return;

		context.RegisterCodeFix(
			CodeAction.Create(
				title: "Use null propagation",
				createChangedDocument: token => ReplaceWithNullConditionalAsync(context.Document, conditionalExpression, nullableExpression, token),
				"use-null-propagation"),
			diagnostic);
	}

	private static ExpressionSyntax? TryExtractNullableExpression(ConditionalExpressionSyntax conditionalExpression)
	{
		// extract nullable expression from HasValue check
		if (conditionalExpression.Condition is MemberAccessExpressionSyntax
			{
				Name.Identifier.ValueText: "HasValue",
				Expression: var nullableExpression,
			})
		{
			return nullableExpression;
		}

		return null;
	}

	private static async Task<Document> ReplaceWithNullConditionalAsync(Document document, ConditionalExpressionSyntax conditionalExpression,
		ExpressionSyntax nullableExpression, CancellationToken cancellationToken)
	{
		// build the null-conditional expression by taking the original true expression and replacing nullable.Value with nullable?
		var trueExpression = conditionalExpression.WhenTrue;
		var nullConditionalExpression = TransformExpression(trueExpression, nullableExpression);

		// replace the conditional expression with the null-conditional expression
		var root = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))
			.ReplaceNode(conditionalExpression, nullConditionalExpression.WithTriviaFrom(conditionalExpression).WithAdditionalAnnotations(Formatter.Annotation));

		return document.WithSyntaxRoot(root);
	}

	private static ExpressionSyntax TransformExpression(ExpressionSyntax expression, ExpressionSyntax nullableExpression)
	{
		switch (expression)
		{
			case MemberAccessExpressionSyntax
			{
				Expression: var receiver,
				Name.Identifier.ValueText: "Value",
			}
			when AreEquivalent(receiver, nullableExpression):
				// this is nullable.Value - this should be removed and the parent should use conditional access
				return receiver;

			case MemberAccessExpressionSyntax
			{
				Expression: MemberAccessExpressionSyntax
				{
					Expression: var receiver,
					Name.Identifier.ValueText: "Value",
				},
			}
			memberAccess when AreEquivalent(receiver, nullableExpression):
				// transform nullable.Value.Something to nullable?.Something
				return ConditionalAccessExpression(
					receiver,
					MemberBindingExpression(memberAccess.Name));

			case MemberAccessExpressionSyntax memberAccess:
				// recursively handle nested member accesses
				var transformedInner = TransformExpression(memberAccess.Expression, nullableExpression);

				if (transformedInner is ConditionalAccessExpressionSyntax conditionalAccess)
				{
					// chain the member access after conditional access
					return conditionalAccess.WithWhenNotNull(MemberAccessExpression(
						SyntaxKind.SimpleMemberAccessExpression, conditionalAccess.WhenNotNull, memberAccess.Name));
				}
				break;

			case InvocationExpressionSyntax invocation:
				// transform the expression that the method is being called on
				var transformedExpression = TransformExpression(invocation.Expression, nullableExpression);
				if (transformedExpression is ConditionalAccessExpressionSyntax conditionalInvocation)
					return conditionalInvocation.WithWhenNotNull(InvocationExpression(conditionalInvocation.WhenNotNull, invocation.ArgumentList));
				if (transformedExpression != invocation.Expression)
					return InvocationExpression(transformedExpression, invocation.ArgumentList);
				break;
		}

		return expression;
	}
}
