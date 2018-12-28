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

namespace Faithlife.Analyzers
{
		[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CurrentAsyncWorkItemCodeFixProvider)), Shared]
		public sealed class OverloadWithStringComparisonCodeFixProvider : CodeFixProvider
		{
				public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(OverloadWithStringComparisonAnalyzer.DiagnosticId);

				public sealed override FixAllProvider GetFixAllProvider() => null;

				public override async Task RegisterCodeFixesAsync(CodeFixContext context)
				{
						var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
						var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

						var diagnostic = context.Diagnostics.First();
						var diagnosticSpan = diagnostic.Location.SourceSpan;

						var invocation = root.FindNode(diagnosticSpan) as InvocationExpressionSyntax;
						if (invocation == null)
								return;

						var methodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
						if (methodSymbol == null)
								return;

						context.RegisterCodeFix(
							CodeAction.Create(
								title: $"Invoke {methodSymbol.Name} with StringComparison.Ordinal",
								createChangedDocument: token => AddStringComparisonArgumentAsync(context.Document, invocation, token),
								"use-stringcomparison-ordinal"),
							diagnostic);
				}

				private static async Task<Document> AddStringComparisonArgumentAsync(Document document, InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
				{
						// TODO: find the "best" overload and map all the existing arguments to it
						// for now, assume that there is an overload that takes all the existing arguments and also a StringComparison enum member
						var root = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))
							.ReplaceNode(invocation.ArgumentList, invocation.ArgumentList.AddArguments(SyntaxFactory.Argument(SyntaxFactory.ParseExpression("System.StringComparison.Ordinal"))))
							.WithAdditionalAnnotations(Simplifier.Annotation);

						return await Simplifier.ReduceAsync(document.WithSyntaxRoot(root), cancellationToken: cancellationToken).ConfigureAwait(false);
				}
		}
}
