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
using Microsoft.CodeAnalysis.Simplification;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Faithlife.Analyzers
{
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CurrentAsyncWorkItemCodeFixProvider)), Shared]
	public sealed class OverloadWithStringComparisonCodeFixProvider : CodeFixProvider
	{
		public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(OverloadWithStringComparisonAnalyzer.UseStringComparisonDiagnosticId, OverloadWithStringComparisonAnalyzer.AvoidStringEqualsDiagnosticId);

		public override FixAllProvider? GetFixAllProvider() => null;

		public override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
			var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

			var diagnostic = context.Diagnostics.First();
			var diagnosticSpan = diagnostic.Location.SourceSpan;

			var invocation = root.FindNode(diagnosticSpan).FirstAncestorOrSelf<InvocationExpressionSyntax>();
			if (invocation is null)
				return;

			var methodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
			if (methodSymbol is null)
				return;

			if (diagnostic.Id == OverloadWithStringComparisonAnalyzer.UseStringComparisonDiagnosticId)
			{
				// suggest adding StringComparison.Ordinal
				context.RegisterCodeFix(
					CodeAction.Create(
						title: $"Invoke {methodSymbol.Name} with StringComparison.Ordinal",
						createChangedDocument: token => AddStringComparisonArgumentAsync(context.Document, invocation, "System.StringComparison.Ordinal", token),
						"use-stringcomparison-ordinal"),
					diagnostic);
			}
			else if (diagnostic.Id == OverloadWithStringComparisonAnalyzer.AvoidStringEqualsDiagnosticId)
			{
				// suggest changing to a == b
				context.RegisterCodeFix(
					CodeAction.Create(
						title: $"Replace {methodSymbol.Name} with ==",
						createChangedDocument: token => ReplaceWithOperatorEqualityAsync(context.Document, methodSymbol, invocation, token),
						"use-stringcomparison-ordinal"),
					diagnostic);

				if (methodSymbol.Parameters.Last().Type.Name != "StringComparison")
				{
					// for string.Equals(string), suggest adding StringComparison.OrdinalIgnoreCase
					context.RegisterCodeFix(
						CodeAction.Create(
							title: $"Invoke {methodSymbol.Name} with StringComparison.OrdinalIgnoreCase",
							createChangedDocument: token => AddStringComparisonArgumentAsync(context.Document, invocation, "System.StringComparison.OrdinalIgnoreCase", token),
							"use-stringcomparison-ordinal"),
						diagnostic);
				}
				else
				{
					// for string.Equals(string, StringComparison), suggest changing the enum value
					context.RegisterCodeFix(
						CodeAction.Create(
							title: $"Invoke {methodSymbol.Name} with StringComparison.OrdinalIgnoreCase",
							createChangedDocument: token => ReplaceStringComparisonArgumentAsync(context.Document, invocation, "System.StringComparison.OrdinalIgnoreCase", token),
							"use-stringcomparison-ordinal"),
						diagnostic);
				}
			}
		}

		private static async Task<Document> AddStringComparisonArgumentAsync(Document document, InvocationExpressionSyntax invocation, string expressionValue, CancellationToken cancellationToken)
		{
			// TODO: find the "best" overload and map all the existing arguments to it
			// for now, assume that there is an overload that takes all the existing arguments and also a StringComparison enum member
			var root = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))
				.ReplaceNode(invocation.ArgumentList, invocation.ArgumentList.AddArguments(Argument(ParseExpression(expressionValue).WithAdditionalAnnotations(Simplifier.Annotation))));

			return await Simplifier.ReduceAsync(document.WithSyntaxRoot(root), cancellationToken: cancellationToken).ConfigureAwait(false);
		}

		private static async Task<Document> ReplaceWithOperatorEqualityAsync(Document document, IMethodSymbol methodSymbol, InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
		{
			ExpressionSyntax left, right;
			if (methodSymbol.IsStatic)
			{
				var arguments = invocation.ArgumentList.Arguments;
				left = arguments[0].Expression;
				right = arguments[1].Expression;
			}
			else
			{
				left = invocation.Expression;
				if (left is MemberAccessExpressionSyntax memberAccess)
					left = memberAccess.Expression;
				right = invocation.ArgumentList.Arguments[0].Expression;
			}

			var root = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))
				.ReplaceNode(invocation, BinaryExpression(SyntaxKind.EqualsExpression, left, right)
					.WithAdditionalAnnotations(Simplifier.Annotation));

			return await Simplifier.ReduceAsync(document.WithSyntaxRoot(root), cancellationToken: cancellationToken).ConfigureAwait(false);
		}

		private static async Task<Document> ReplaceStringComparisonArgumentAsync(Document document, InvocationExpressionSyntax invocation, string expressionValue, CancellationToken cancellationToken)
		{
			var arguments = invocation.ArgumentList.Arguments;
			var newArguments = arguments.Replace(arguments.Last(), Argument(ParseExpression(expressionValue).WithAdditionalAnnotations(Simplifier.Annotation)));
			var newArgumentList = invocation.ArgumentList.WithArguments(newArguments);
			var root = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))
				.ReplaceNode(invocation.ArgumentList, newArgumentList);

			return await Simplifier.ReduceAsync(document.WithSyntaxRoot(root), cancellationToken: cancellationToken).ConfigureAwait(false);
		}
	}
}
