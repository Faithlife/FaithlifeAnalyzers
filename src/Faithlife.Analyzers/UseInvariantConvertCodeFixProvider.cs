using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Faithlife.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseInvariantConvertCodeFixProvider)), Shared]
public sealed class UseInvariantConvertCodeFixProvider : CodeFixProvider
{
	public sealed override ImmutableArray<string> FixableDiagnosticIds => [UseInvariantConvertAnalyzer.DiagnosticId];

	public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		if (root is null)
			return;

		var diagnostic = context.Diagnostics.First();
		var diagnosticNode = root.FindNode(diagnostic.Location.SourceSpan);
		var invocation = diagnosticNode.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault();
		if (invocation is null)
			return;

		if (!diagnostic.Properties.TryGetValue(UseInvariantConvertAnalyzer.KindPropertyKey, out var kind))
			return;
		diagnostic.Properties.TryGetValue(UseInvariantConvertAnalyzer.SuffixPropertyKey, out var suffix);

		SyntaxNode? replacementTarget = null;
		ExpressionSyntax? replacement = null;

		switch (kind)
		{
			case UseInvariantConvertAnalyzer.ToStringKindValue:
			{
				if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
					return;
				replacementTarget = invocation;
				replacement = ParseExpression($"{memberAccess.Expression}.ToInvariantString()");
				break;
			}

			case UseInvariantConvertAnalyzer.ParseKindValue:
			{
				var value = invocation.ArgumentList.Arguments[0].Expression;
				replacementTarget = invocation;
				replacement = ParseExpression($"InvariantConvert.Parse{suffix}({value})");
				break;
			}

			case UseInvariantConvertAnalyzer.TryParseKindValue:
			{
				var value = invocation.ArgumentList.Arguments[0].Expression;

				// Extract the name declared by the `out var result` (or `out int result`) argument; bail on anything else.
				var outArgument = invocation.ArgumentList.Arguments.FirstOrDefault(x => x.Expression is DeclarationExpressionSyntax);
				if (outArgument?.Expression is not DeclarationExpressionSyntax { Designation: SingleVariableDesignationSyntax designation })
					return;
				var outName = designation.Identifier.Text;

				// A negated TryParse becomes an `is not { }` pattern, replacing the enclosing `!` expression.
				var isNegated = invocation.Parent is PrefixUnaryExpressionSyntax prefix && prefix.IsKind(SyntaxKind.LogicalNotExpression);
				replacementTarget = isNegated ? invocation.Parent : invocation;
				var patternKeyword = isNegated ? "is not" : "is";
				replacement = ParseExpression($"InvariantConvert.TryParse{suffix}({value}) {patternKeyword} {{ }} {outName}");
				break;
			}
		}

		if (replacementTarget is null || replacement is null)
			return;

		var fixedReplacement = replacement.WithTriviaFrom(replacementTarget);

		context.RegisterCodeFix(
			CodeAction.Create(
				title: "Use InvariantConvert",
				createChangedDocument: token => ReplaceAsync(context.Document, replacementTarget, fixedReplacement, token),
				c_fixName),
			diagnostic);
	}

	private static async Task<Document> ReplaceAsync(Document document, SyntaxNode replacementTarget, SyntaxNode replacementNode, CancellationToken cancellationToken)
	{
		var root = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))!
			.ReplaceNode(replacementTarget, replacementNode);

		if (root is CompilationUnitSyntax compilationUnit &&
			!root.DescendantNodes().OfType<UsingDirectiveSyntax>().Any(x => x.Name is { } name && AreEquivalent(name, s_invariantNamespace)))
		{
			root = compilationUnit.AddUsings(
				UsingDirective(s_invariantNamespace).WithTrailingTrivia(EndOfLine("\n")));
		}

		return document.WithSyntaxRoot(root);
	}

	private static readonly NameSyntax s_invariantNamespace = ParseName("Libronix.Utility.Invariant");

	private const string c_fixName = "use-invariant-convert";
}
