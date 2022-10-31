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

namespace Faithlife.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CurrentAsyncWorkItemCodeFixProvider)), Shared]
public class CurrentAsyncWorkItemCodeFixProvider : CodeFixProvider
{
	public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(CurrentAsyncWorkItemAnalyzer.DiagnosticId);

	public sealed override FixAllProvider? GetFixAllProvider() => null;

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

		var workStateParameters = containingMethod.ParameterList.Parameters.Where(parameter =>
		{
			var symbolInfo = semanticModel.GetSymbolInfo(parameter.Type);
			if (symbolInfo.Symbol is null)
				return false;

			if (SymbolEqualityComparer.Default.Equals(symbolInfo.Symbol, iworkState))
				return true;

			var namedTypeSymbol = symbolInfo.Symbol as INamedTypeSymbol;
			if (namedTypeSymbol is null)
				return false;

			return namedTypeSymbol.AllInterfaces.Any(x => SymbolEqualityComparer.Default.Equals(x, iworkState));
		});

		foreach (var parameter in workStateParameters)
		{
			context.RegisterCodeFix(
				CodeAction.Create(
					title: $"Use '{parameter.Identifier.Text}' parameter of {containingMethod.Identifier.Text}",
					createChangedDocument: token => ReplaceValueAsync(context.Document, memberAccess, parameter, token),
					$"use-{parameter.Identifier.Text}"),
				diagnostic);
		}

		context.RegisterCodeFix(
			CodeAction.Create(
				title: $"Add new IWorkState parameter to {containingMethod.Identifier.Text}",
				createChangedDocument: token => AddParameterAsync(context.Document, memberAccess, containingMethod, token),
				"add-parameter"),
			diagnostic);
	}

	private static async Task<Document> ReplaceValueAsync(Document document, MemberAccessExpressionSyntax memberAccess, ParameterSyntax replacementParameter, CancellationToken cancellationToken)
	{
		var root = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))
			.ReplaceNode(memberAccess, IdentifierName(replacementParameter.Identifier));

		return document.WithSyntaxRoot(root);
	}

	private static async Task<Document> AddParameterAsync(Document document, MemberAccessExpressionSyntax memberAccess, MethodDeclarationSyntax containingMethod, CancellationToken cancellationToken)
	{
		var parameterIdentifier = SyntaxUtility.GetHoistableIdentifier("workState", containingMethod);

		var root = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))
			.ReplaceNode(containingMethod, containingMethod
				.ReplaceNode(memberAccess, IdentifierName(parameterIdentifier))
				.AddParameterListParameters(Parameter(parameterIdentifier)
					.WithType(s_iworkStateTypeName)));

		return await Simplifier.ReduceAsync(document.WithSyntaxRoot(root), cancellationToken: cancellationToken).ConfigureAwait(false);
	}

	private static readonly TypeSyntax s_iworkStateTypeName = SyntaxUtility.ParseSimplifiableTypeName("Libronix.Utility.Threading.IWorkState");
}
