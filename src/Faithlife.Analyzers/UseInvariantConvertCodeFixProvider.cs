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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseInvariantConvertCodeFixProvider)), Shared]
public sealed class UseInvariantConvertCodeFixProvider : CodeFixProvider
{
	public sealed override ImmutableArray<string> FixableDiagnosticIds => [UseInvariantConvertAnalyzer.DiagnosticId];

	public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
		if (semanticModel is null)
			return;

		var invariantConvertContext = InvariantConvertContext.TryCreate(semanticModel.Compilation);
		if (invariantConvertContext is null)
			return;

		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		if (root is null)
			return;

		var diagnostic = context.Diagnostics.First();
		var diagnosticNode = root.FindNode(diagnostic.Location.SourceSpan);

		var invocation = diagnosticNode.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault();
		if (invocation is null)
			return;

		var match = invariantConvertContext.Match(invocation, semanticModel, context.CancellationToken);
		if (match is null)
			return;

		SyntaxNode replacementTarget;
		ExpressionSyntax replacement;
		switch (match.Kind)
		{
			case InvariantConvertKind.Parse:
				replacementTarget = invocation;
				replacement = ParseExpression($"InvariantConvert.{match.SuggestedMethodName}({match.ValueExpression})");
				break;

			case InvariantConvertKind.ToString:
				replacementTarget = invocation;
				replacement = ParseExpression($"{match.ValueExpression}.ToInvariantString()");
				break;

			case InvariantConvertKind.TryParse:
			{
				var (tryParseTarget, tryParseReplacement) = TryBuildTryParseReplacement(match, invocation, semanticModel);
				if (tryParseTarget is null || tryParseReplacement is null)
					return;
				replacementTarget = tryParseTarget;
				replacement = tryParseReplacement;
				break;
			}

			default:
				return;
		}

		context.RegisterCodeFix(
			CodeAction.Create(
				title: c_title,
				createChangedDocument: token => ReplaceValueAsync(context.Document, replacementTarget, replacement.WithTriviaFrom(replacementTarget), token),
				c_title),
			diagnostic);
	}

	private static (SyntaxNode? Target, ExpressionSyntax? Replacement) TryBuildTryParseReplacement(InvariantConvertMatch match, InvocationExpressionSyntax invocation, SemanticModel semanticModel)
	{
		// Only rewrite when the out argument declares a named variable: `out var x` / `out int x`.
		if (match.OutArgument!.Expression is not DeclarationExpressionSyntax declaration)
			return default;
		if (declaration.Designation is not SingleVariableDesignationSyntax designation)
			return default;
		if (semanticModel.GetDeclaredSymbol(designation) is not ILocalSymbol outLocal)
			return default;

		// Only rewrite when the invocation is the whole condition of an `if`, optionally negated by a single `!`.
		bool negated;
		SyntaxNode replacementTarget;
		IfStatementSyntax ifStatement;
		if (invocation.Parent is IfStatementSyntax directIf && directIf.Condition == invocation)
		{
			negated = false;
			replacementTarget = invocation;
			ifStatement = directIf;
		}
		else if (invocation.Parent is PrefixUnaryExpressionSyntax prefix &&
			prefix.IsKind(SyntaxKind.LogicalNotExpression) &&
			prefix.Parent is IfStatementSyntax negatedIf &&
			negatedIf.Condition == prefix)
		{
			negated = true;
			replacementTarget = prefix;
			ifStatement = negatedIf;
		}
		else
		{
			return default;
		}

		// Converting `out var x` to a pattern variable moves where `x` is definitely assigned, so only rewrite
		// when the result is guaranteed to compile.
		var thenStatement = ifStatement.Statement;
		var scope = ifStatement.Ancestors().FirstOrDefault(x => x is BlockSyntax or ArrowExpressionClauseSyntax) ?? ifStatement.Parent;
		var referencesInThen = false;
		var referencesOutsideThen = false;
		if (scope is not null)
		{
			foreach (var identifier in scope.DescendantNodes().OfType<IdentifierNameSyntax>())
			{
				if (identifier.Identifier.ValueText != designation.Identifier.ValueText)
					continue;
				if (!SymbolEqualityComparer.Default.Equals(semanticModel.GetSymbolInfo(identifier).Symbol, outLocal))
					continue;

				if (thenStatement.Span.Contains(identifier.Span))
					referencesInThen = true;
				else
					referencesOutsideThen = true;
			}
		}

		if (negated)
		{
			// `if (!TryParse(...)) then`: `x` is only definitely assigned after the `if` when `then` never falls through.
			if (referencesInThen)
				return default;
			var controlFlow = semanticModel.AnalyzeControlFlow(thenStatement);
			if (controlFlow is not { Succeeded: true, EndPointIsReachable: false })
				return default;
		}
		else
		{
			// `if (TryParse(...)) then`: `x` is only in scope within `then`.
			if (referencesOutsideThen)
				return default;
		}

		var notKeyword = negated ? "not " : "";
		var replacement = ParseExpression($"InvariantConvert.{match.SuggestedMethodName}({match.ValueExpression}) is {notKeyword}{{ }} {designation.Identifier.ValueText}");
		return (replacementTarget, replacement);
	}

	private static async Task<Document> ReplaceValueAsync(Document document, SyntaxNode replacementTarget, SyntaxNode replacementNode, CancellationToken cancellationToken)
	{
		var root = (CompilationUnitSyntax) (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))!;
		root = root.ReplaceNode(replacementTarget, replacementNode);

		// Bring `InvariantConvert` and the `ToInvariantString` extension method into scope.
		if (!root.Usings.Any(x => AreEquivalent(x.Name, s_invariantNamespace)))
		{
			var invariantUsing = UsingDirective(s_invariantNamespace)
				.WithUsingKeyword(Token(SyntaxKind.UsingKeyword).WithTrailingTrivia(Space))
				.WithTrailingTrivia(EndOfLine("\n"));
			root = root.AddUsings(invariantUsing);
		}

		// Allow the simplifier to remove `using System.Globalization;` if the rewrite made it unnecessary.
		var globalizationUsing = root.Usings.FirstOrDefault(x => AreEquivalent(x.Name, s_globalizationNamespace));
		if (globalizationUsing is not null)
			root = root.ReplaceNode(globalizationUsing, globalizationUsing.WithAdditionalAnnotations(Simplifier.Annotation));

		return await Simplifier.ReduceAsync(
			await Simplifier.ReduceAsync(document.WithSyntaxRoot(root), cancellationToken: cancellationToken).ConfigureAwait(false),
			cancellationToken: cancellationToken).ConfigureAwait(false);
	}

	private static readonly NameSyntax s_invariantNamespace = ParseName("Libronix.Utility.Invariant");

	private static readonly NameSyntax s_globalizationNamespace = ParseName("System.Globalization");

	private const string c_title = "Use InvariantConvert";
}
