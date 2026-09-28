using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Faithlife.Analyzers;

internal static class FormattableStringInvariantUsing
{
	public static async Task<bool> BindsToInvariantAsync(Document document, SyntaxAnnotation annotation, CancellationToken cancellationToken)
	{
		var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		var invocation = root?.GetAnnotatedNodes(annotation).OfType<InvocationExpressionSyntax>().FirstOrDefault();
		var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
		return invocation is not null &&
			semanticModel?.Compilation.GetTypeByMetadataName("System.FormattableString") is { } formattableStringType &&
			semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol { Name: "Invariant", IsStatic: true } method &&
			SymbolEqualityComparer.Default.Equals(method.ContainingType, formattableStringType);
	}

	public static CompilationUnitSyntax AddIfMissing(CompilationUnitSyntax root, InvocationExpressionSyntax invocation)
	{
		if (HasStaticUsing(root.Usings) ||
			invocation.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Any(x => HasStaticUsing(x.Usings)))
			return root;

		var lineBreak = EndOfLine(root.ToFullString().Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n");
		var usingDirective = UsingDirective(ParseName("System.FormattableString"))
			.WithStaticKeyword(Token(SyntaxKind.StaticKeyword));
		if (root.Usings.LastOrDefault() is { } lastUsing)
		{
			var trailing = lastUsing.GetTrailingTrivia();
			var firstLineBreak = trailing.IndexOf(trailing.FirstOrDefault(x => x.IsKind(SyntaxKind.EndOfLineTrivia)));
			if (firstLineBreak >= 0)
			{
				var lastUsingTrailing = TriviaList(trailing.Take(firstLineBreak + 1));
				var newUsingTrailing = TriviaList(trailing.Skip(firstLineBreak));
				root = root.ReplaceNode(lastUsing, lastUsing.WithTrailingTrivia(lastUsingTrailing));
				usingDirective = usingDirective.WithTrailingTrivia(newUsingTrailing);
			}
			else
			{
				root = root.ReplaceNode(lastUsing, lastUsing.WithTrailingTrivia(trailing.Add(lineBreak)));
				usingDirective = usingDirective.WithTrailingTrivia(lineBreak);
			}
		}
		else
		{
			usingDirective = usingDirective
				.WithLeadingTrivia(root.GetLeadingTrivia())
				.WithTrailingTrivia(lineBreak, lineBreak);
			root = root.WithLeadingTrivia();
		}

		return root.WithUsings(root.Usings.Add(usingDirective));
	}

	private static bool HasStaticUsing(SyntaxList<UsingDirectiveSyntax> usings) =>
		usings.Any(x => x.StaticKeyword.IsKind(SyntaxKind.StaticKeyword) &&
			(AreEquivalent(x.Name, s_name) || AreEquivalent(x.Name, s_globalName)));

	private static readonly NameSyntax s_name = ParseName("System.FormattableString");
	private static readonly NameSyntax s_globalName = ParseName("global::System.FormattableString");
}
