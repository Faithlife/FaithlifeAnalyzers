using System;
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
	public sealed class ToReadOnlyCollectionCodeFixProvider : CodeFixProvider
	{
		public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(ToReadOnlyCollectionAnalyzer.DiagnosticId);

		public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

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

			context.RegisterCodeFix(
				CodeAction.Create(
					title: "Use .ToList().AsReadOnly()",
					createChangedDocument: token => ConvertToToListAsReadOnlyAsync(context.Document, invocation, token),
					"to-list-as-read-only"),
				diagnostic);
		}

		private static async Task<Document> ConvertToToListAsReadOnlyAsync(Document document, InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
		{
			var root = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))
				.ReplaceNode(invocation, InvocationExpression(
						MemberAccessExpression(
							SyntaxKind.SimpleMemberAccessExpression,
							ReplaceInvocation(invocation),
							IdentifierName("AsReadOnly")))
					.NormalizeWhitespace());

			return await Simplifier.ReduceAsync(document.WithSyntaxRoot(root), cancellationToken: cancellationToken).ConfigureAwait(false);
		}

		private static ExpressionSyntax ReplaceInvocation(InvocationExpressionSyntax invocation)
		{
			ExpressionSyntax newExpression;
			switch (invocation.Expression)
			{
			case MemberAccessExpressionSyntax memberAccess:
				newExpression = MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					memberAccess.Expression,
					IdentifierName("ToList"));
				break;

			case MemberBindingExpressionSyntax _:
				newExpression = MemberBindingExpression(IdentifierName("ToList"));
				break;

			default:
				throw new NotSupportedException($"Can't handle {invocation.Expression.GetType()}");
			}

			return InvocationExpression(newExpression);
		}
	}
}
