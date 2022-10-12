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
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;

namespace Faithlife.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UntilCanceledCodeFixProvider)), Shared]
public sealed class UntilCanceledCodeFixProvider : CodeFixProvider
{
	public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(UntilCanceledAnalyzer.DiagnosticId);

	public override FixAllProvider? GetFixAllProvider() => null;

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

		var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
		var iworkState = semanticModel.Compilation.GetTypeByMetadataName("Libronix.Utility.Threading.IWorkState");
		if (iworkState is null)
			return;

		var diagnostic = context.Diagnostics.First();
		var diagnosticSpan = diagnostic.Location.SourceSpan;

		var diagnosticNode = root.FindNode(diagnosticSpan);
		if (!(diagnosticNode is ArgumentListSyntax))
			return;

		var containingMethod = diagnosticNode.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
		if (containingMethod is null)
			return;

		var workStateParameters = containingMethod.ParameterList.Parameters.Where(parameter =>
		{
			var symbolInfo = semanticModel.GetSymbolInfo(parameter.Type);
			if (symbolInfo.Symbol is null)
				return false;

			if (symbolInfo.Symbol.Equals(iworkState))
				return true;

			var namedTypeSymbol = symbolInfo.Symbol as INamedTypeSymbol;
			if (namedTypeSymbol is null)
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
		var argumentList = (ArgumentListSyntax) diagnosticNode;
		var replacement = argumentList.AddArguments(SyntaxFactory.Argument(SyntaxFactory.IdentifierName(replacementParameter.Identifier)));
		var root = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))
			.ReplaceNode(argumentList, replacement.WithAdditionalAnnotations(Formatter.Annotation));

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
			.Where(x => x.StartsWith(preferredName, StringComparison.Ordinal)));

		string candidateName = preferredName;
		int suffix = 1;

		while (conflictingNames.Contains(candidateName))
		{
			candidateName = preferredName + suffix;
			suffix++;
		}

		var argumentList = (ArgumentListSyntax) diagnosticNode;
		var replacement = argumentList.AddArguments(SyntaxFactory.Argument(SyntaxFactory.IdentifierName(candidateName)));

		var root = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))
			.ReplaceNode(containingMethod, containingMethod
				.ReplaceNode(argumentList, replacement.WithAdditionalAnnotations(Formatter.Annotation))
				.AddParameterListParameters(SyntaxFactory.Parameter(SyntaxFactory.Identifier(candidateName))
					.WithType(s_iworkStateTypeName)));

		return await Simplifier.ReduceAsync(document.WithSyntaxRoot(root), cancellationToken: cancellationToken).ConfigureAwait(false);
	}

	private static readonly TypeSyntax s_iworkStateTypeName = SyntaxUtility.ParseSimplifiableTypeName("Libronix.Utility.Threading.IWorkState");
}
