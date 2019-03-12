using System;
using System.Collections.Generic;
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
	public class AvailableWorkStateCodeFixProvider : CodeFixProvider
	{
		public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(AvailableWorkStateAnalyzer.DiagnosticId);

		public sealed override FixAllProvider GetFixAllProvider() => null;

		public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

			var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
			var iworkState = semanticModel.Compilation.GetTypeByMetadataName("Libronix.Utility.Threading.IWorkState");
			if (iworkState is null)
				return;

			var diagnostic = context.Diagnostics.First();
			var diagnosticSpan = diagnostic.Location.SourceSpan;

			var diagnosticNode = root.FindNode(diagnosticSpan);
			var memberAccess = diagnosticNode.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>().FirstOrDefault();
			if (memberAccess is null)
				return;

			var containingMethod = diagnosticNode.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
			if (containingMethod is null)
				return;

			var asyncAction = semanticModel.Compilation.GetTypeByMetadataName("Libronix.Utility.Threading.AsyncAction");
			var asyncMethodContext = semanticModel.Compilation.GetTypeByMetadataName("Libronix.Utility.Threading.AsyncMethodContext");
			if (asyncAction != null)
			{
				var ienumerable = semanticModel.Compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1");
				var returnTypeSymbol = semanticModel.GetSymbolInfo(containingMethod.ReturnType).Symbol as INamedTypeSymbol;
				if (returnTypeSymbol != null && returnTypeSymbol.ConstructedFrom != null && returnTypeSymbol.ConstructedFrom.Equals(ienumerable) &&
					returnTypeSymbol.TypeArguments[0].Equals(asyncAction))
				{
					context.RegisterCodeFix(
						CodeAction.Create(
							title: $"Use AsyncWorkItem.Current",
							createChangedDocument: token => ReplaceValueAsync(context.Document, memberAccess, s_currentWorkItemExpression, token),
							$"use-asyncworkitem-current"),
						diagnostic);
				}
			}

			var cancellationToken = semanticModel.Compilation.GetTypeByMetadataName("System.Threading.CancellationToken");
			foreach (var parameter in containingMethod.ParameterList.Parameters)
			{
				var symbolInfo = semanticModel.GetSymbolInfo(parameter.Type);
				if (symbolInfo.Symbol is null)
					continue;

				if (symbolInfo.Symbol.Equals(iworkState) || (symbolInfo.Symbol is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.AllInterfaces.Any(x => x.Equals(iworkState))))
				{
					context.RegisterCodeFix(
						CodeAction.Create(
							title: $"Use '{parameter.Identifier.Text}' parameter of {containingMethod.Identifier.Text}",
							createChangedDocument: token => ReplaceValueAsync(context.Document, memberAccess, IdentifierName(parameter.Identifier), token),
							$"use-{parameter.Identifier.Text}"),
						diagnostic);
				}
				else if (symbolInfo.Symbol.Equals(cancellationToken))
				{
					context.RegisterCodeFix(
						CodeAction.Create(
							title: $"Use '{parameter.Identifier.Text}' parameter of {containingMethod.Identifier.Text}",
							createChangedDocument: token => ReplaceValueAsync(
								context.Document,
								memberAccess,
								SyntaxUtility.ReplaceIdentifier(s_fromCancellationTokenExpression, "token", IdentifierName(parameter.Identifier)),
								token),
							$"use-{parameter.Identifier.Text}"),
						diagnostic);
				}
				else if (symbolInfo.Symbol.Equals(asyncMethodContext))
				{
					context.RegisterCodeFix(
						CodeAction.Create(
							title: $"Use '{parameter.Identifier.Text}' parameter of {containingMethod.Identifier.Text}",
							createChangedDocument: token => ReplaceValueAsync(
								context.Document,
								memberAccess,
								SyntaxUtility.ReplaceIdentifier(s_contextWorkStateExpression, "asyncMethodContext", IdentifierName(parameter.Identifier)),
								token),
							$"use-{parameter.Identifier.Text}"),
						diagnostic);
				}
			}
		}

		private static async Task<Document> ReplaceValueAsync(Document document, MemberAccessExpressionSyntax memberAccess, ExpressionSyntax replacementNode, CancellationToken cancellationToken)
		{
			var root = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))
				.ReplaceNode(memberAccess, replacementNode);

			return await Simplifier.ReduceAsync(document.WithSyntaxRoot(root), cancellationToken: cancellationToken).ConfigureAwait(false);
		}

		private static readonly ExpressionSyntax s_currentWorkItemExpression = SyntaxUtility.ReplaceIdentifier(
			ParseExpression("AsyncWorkItem.Current"),
			"AsyncWorkItem",
			SyntaxUtility.ParseSimplifiableTypeName("Libronix.Utility.Threading.AsyncWorkItem"));

		private static readonly ExpressionSyntax s_fromCancellationTokenExpression = SyntaxUtility.ReplaceIdentifier(
			ParseExpression("WorkState.FromCancellationToken(token)"),
			"WorkState",
			SyntaxUtility.ParseSimplifiableTypeName("Libronix.Utility.Threading.WorkState"));

		private static readonly ExpressionSyntax s_contextWorkStateExpression = ParseExpression("asyncMethodContext.WorkState");
	}
}
