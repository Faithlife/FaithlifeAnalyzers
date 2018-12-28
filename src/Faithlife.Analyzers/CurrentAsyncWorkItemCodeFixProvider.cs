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

namespace Faithlife.Analyzers
{
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CurrentAsyncWorkItemCodeFixProvider)), Shared]
	public class CurrentAsyncWorkItemCodeFixProvider : CodeFixProvider
	{
		public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(CurrentAsyncWorkItemAnalyzer.DiagnosticId);

		public sealed override FixAllProvider GetFixAllProvider() => null;

		public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

			var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken);
			var iworkState = semanticModel.Compilation.GetTypeByMetadataName("Libronix.Utility.Threading.IWorkState");
			if (iworkState == null)
				return;

			var diagnostic = context.Diagnostics.First();
			var diagnosticSpan = diagnostic.Location.SourceSpan;

			var diagnosticNode = root.FindNode(diagnosticSpan);
			if (!(diagnosticNode is ArgumentSyntax))
				return;

			var containingMethod = diagnosticNode.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
			if (containingMethod == null)
				return;

			var workStateParameters = containingMethod.ParameterList.Parameters.Where(parameter =>
			{
				var symbolInfo = semanticModel.GetSymbolInfo(parameter.Type);
				if (symbolInfo.Symbol == null)
					return false;

				if (symbolInfo.Symbol.Equals(iworkState))
					return true;

				var namedTypeSymbol = symbolInfo.Symbol as INamedTypeSymbol;
				if (namedTypeSymbol == null)
					return false;

				return namedTypeSymbol.AllInterfaces.Any(x => x.Equals(iworkState));
			});

			foreach (var parameter in workStateParameters)
			{
				context.RegisterCodeFix(
					CodeAction.Create(
						title: $"Use '{parameter.Identifier.Text}' parameter of {containingMethod.Identifier.Text}",
						createChangedDocument: token => ReplaceValueAsync(context.Document, diagnosticNode, parameter, token),
						$"use-{parameter.Identifier.Text}"),
					diagnostic);
			}

			context.RegisterCodeFix(
				CodeAction.Create(
					title: $"Add new IWorkState parameter to {containingMethod.Identifier.Text}",
					createChangedDocument: token => AddParameterAsync(context.Document, diagnosticNode, containingMethod, token),
					"add-parameter"),
				diagnostic);
		}

		private static async Task<Document> ReplaceValueAsync(Document document, SyntaxNode diagnosticNode, ParameterSyntax replacementParameter, CancellationToken cancellationToken)
		{
			var root = (await document.GetSyntaxRootAsync())
				.ReplaceNode(diagnosticNode, SyntaxFactory.Argument(SyntaxFactory.IdentifierName(replacementParameter.Identifier)));

			return document.WithSyntaxRoot(root);
		}

		private static async Task<Document> AddParameterAsync(Document document, SyntaxNode diagnosticNode, MethodDeclarationSyntax containingMethod, CancellationToken cancellationToken)
		{
			const string preferredName = "workState";

			var conflictingNames = new HashSet<string>(containingMethod.ParameterList.Parameters
				.Select(x => x.Identifier)
				.Concat(containingMethod.Body.DescendantNodes().OfType<VariableDeclaratorSyntax>()
					.Select(x => x.Identifier))
				.Select(x => x.Text)
				.Where(x => x.StartsWith(preferredName)));

			string candidateName = preferredName;
			int suffix = 1;

			while (conflictingNames.Contains(candidateName))
			{
				candidateName = preferredName + suffix;
				suffix++;
			}

			var root = (await document.GetSyntaxRootAsync())
				.ReplaceNode(containingMethod, containingMethod
					.ReplaceNode(diagnosticNode, SyntaxFactory.Argument(SyntaxFactory.IdentifierName(candidateName)))
					.AddParameterListParameters(SyntaxFactory.Parameter(SyntaxFactory.Identifier(candidateName))
						.WithType(s_iworkStateTypeName)
						.WithAdditionalAnnotations(Simplifier.Annotation)));

			return await Simplifier.ReduceAsync(document.WithSyntaxRoot(root), cancellationToken: cancellationToken);
		}

		static readonly QualifiedNameSyntax s_iworkStateTypeName = SyntaxFactory.QualifiedName(
			SyntaxFactory.QualifiedName(
				SyntaxFactory.QualifiedName(
					SyntaxFactory.IdentifierName("Libronix"),
					SyntaxFactory.IdentifierName("Utility")),
				SyntaxFactory.IdentifierName("Threading")),
			SyntaxFactory.IdentifierName("IWorkState"));
	}
}
