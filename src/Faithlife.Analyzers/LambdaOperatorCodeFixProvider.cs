using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Faithlife.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(LambdaOperatorCodeFixProvider)), Shared]
public sealed class LambdaOperatorCodeFixProvider : CodeFixProvider
{
	public override ImmutableArray<string> FixableDiagnosticIds => [LambdaOperatorAnalyzer.DiagnosticId];

	public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		if (root is null)
			return;

		var diagnostic = context.Diagnostics.First();
		var lambdaOperatorToken = root.FindToken(diagnostic.Location.SourceSpan.Start);
		if (!lambdaOperatorToken.IsKind(SyntaxKind.EqualsGreaterThanToken))
			return;

		var previousToken = lambdaOperatorToken.GetPreviousToken();
		if (previousToken.IsKind(SyntaxKind.None))
			return;

		context.RegisterCodeFix(
			CodeAction.Create(
				title: "Move => to the previous line",
				createChangedDocument: token => MoveLambdaOperatorToPreviousLineAsync(context.Document, previousToken, lambdaOperatorToken, token),
				"move-lambda-operator"),
			diagnostic);
	}

	private static async Task<Document> MoveLambdaOperatorToPreviousLineAsync(Document document, SyntaxToken previousToken, SyntaxToken lambdaOperatorToken, CancellationToken cancellationToken)
	{
		var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
		var nextToken = lambdaOperatorToken.GetNextToken();
		var whitespaceBeforeLambdaOperator = sourceText.ToString(TextSpan.FromBounds(previousToken.Span.End, lambdaOperatorToken.SpanStart));
		var textAfterLambdaOperator = sourceText.ToString(TextSpan.FromBounds(lambdaOperatorToken.Span.End, nextToken.SpanStart));
		var changedText = ContainsOnlyWhitespace(textAfterLambdaOperator) ?
			sourceText.WithChanges(
				new TextChange(TextSpan.FromBounds(previousToken.Span.End, lambdaOperatorToken.SpanStart), " "),
				new TextChange(TextSpan.FromBounds(lambdaOperatorToken.Span.End, nextToken.SpanStart), whitespaceBeforeLambdaOperator)) :
			sourceText.WithChanges(new TextChange(TextSpan.FromBounds(previousToken.Span.End, lambdaOperatorToken.SpanStart), " "));
		return document.WithText(changedText);
	}

	private static bool ContainsOnlyWhitespace(string text) => text.All(char.IsWhiteSpace);
}
