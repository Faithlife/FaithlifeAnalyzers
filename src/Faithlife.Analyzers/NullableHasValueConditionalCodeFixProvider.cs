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
		if (root is null)
			return;

		var diagnostic = context.Diagnostics.First();
		var diagnosticSpan = diagnostic.Location.SourceSpan;

		var diagnosticNode = root.FindNode(diagnosticSpan);
		if (diagnosticNode is not ConditionalExpressionSyntax conditionalExpression)
			return;

		if (TryExtractNullableIdentifier(conditionalExpression) is not { } nullableIdentifier)
			return;

		context.RegisterCodeFix(
			CodeAction.Create(
				title: "Use null propagation",
				createChangedDocument: token => ReplaceWithNullConditionalAsync(context.Document, conditionalExpression, nullableIdentifier, token),
				"use-null-propagation"),
			diagnostic);
	}

	private static IdentifierNameSyntax? TryExtractNullableIdentifier(ConditionalExpressionSyntax conditionalExpression)
	{
		// extract nullable identifier from HasValue check
		if (conditionalExpression.Condition is MemberAccessExpressionSyntax
			{
				Name.Identifier.ValueText: "HasValue",
				Expression: IdentifierNameSyntax identifierName,
			})
		{
			return identifierName;
		}

		return null;
	}

	private static async Task<Document> ReplaceWithNullConditionalAsync(Document document, ConditionalExpressionSyntax conditionalExpression,
		IdentifierNameSyntax nullableIdentifier, CancellationToken cancellationToken)
	{
		// build the null-conditional expression by taking the original true expression and replacing nullable.Value with nullable?
		var trueExpression = conditionalExpression.WhenTrue;
		var nullConditionalExpression = TransformExpression(trueExpression, nullableIdentifier.Identifier.ValueText);

		// replace the conditional expression with the null-conditional expression
		var root = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))
			.ReplaceNode(conditionalExpression, nullConditionalExpression.WithAdditionalAnnotations(Formatter.Annotation));

		return document.WithSyntaxRoot(root);
	}

	private static ExpressionSyntax TransformExpression(ExpressionSyntax expression, string nullableIdentifierName)
	{
		switch (expression)
		{
			case MemberAccessExpressionSyntax
			{
				Expression: IdentifierNameSyntax { } identifier,
				Name.Identifier.ValueText: "Value",
			} when identifier.Identifier.ValueText == nullableIdentifierName:
				// this is nullable.Value - this should be removed and the parent should use conditional access
				return IdentifierName(nullableIdentifierName);

			case MemberAccessExpressionSyntax
			{
				Expression: MemberAccessExpressionSyntax
				{
					Expression: IdentifierNameSyntax { } innerIdentifier,
					Name.Identifier.ValueText: "Value",
				},
			} memberAccess when innerIdentifier.Identifier.ValueText == nullableIdentifierName:
				// transform nullable.Value.Something to nullable?.Something
				return ConditionalAccessExpression(
					IdentifierName(nullableIdentifierName),
					MemberBindingExpression(memberAccess.Name));

			case MemberAccessExpressionSyntax memberAccess:
				// recursively handle nested member accesses
				var transformedInner = TransformExpression(memberAccess.Expression, nullableIdentifierName);

				if (transformedInner is ConditionalAccessExpressionSyntax)
				{
					// chain the member access after conditional access
					return MemberAccessExpression(
						SyntaxKind.SimpleMemberAccessExpression,
						transformedInner,
						memberAccess.Name);
				}
				break;

			case InvocationExpressionSyntax invocation:
				// transform the expression that the method is being called on
				var transformedExpression = TransformExpression(invocation.Expression, nullableIdentifierName);
				if (transformedExpression != invocation.Expression)
					return InvocationExpression(transformedExpression, invocation.ArgumentList);
				break;
		}

		return expression;
	}
}
