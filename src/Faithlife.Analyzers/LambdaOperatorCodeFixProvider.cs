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
		var operatorToken = root.FindToken(diagnostic.Location.SourceSpan.Start);
		if (!operatorToken.IsKind(SyntaxKind.EqualsGreaterThanToken))
			return;

		var previousToken = operatorToken.GetPreviousToken();
		if (previousToken.IsKind(SyntaxKind.None))
			return;

		context.RegisterCodeFix(
			CodeAction.Create(
				title: "Move => to the previous line",
				createChangedDocument: token => MoveOperatorToPreviousLineAsync(context.Document, previousToken, operatorToken, token),
				"move-lambda-operator"),
			diagnostic);
	}

	private static async Task<Document> MoveOperatorToPreviousLineAsync(Document document, SyntaxToken previousToken, SyntaxToken operatorToken, CancellationToken cancellationToken)
	{
		var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
		var nextToken = operatorToken.GetNextToken();
		var whitespaceBeforeOperator = sourceText.ToString(TextSpan.FromBounds(previousToken.Span.End, operatorToken.SpanStart));
		var textAfterOperator = sourceText.ToString(TextSpan.FromBounds(operatorToken.Span.End, nextToken.SpanStart));
		var changedText = ContainsOnlyWhitespace(textAfterOperator) ?
			sourceText.WithChanges(
				new TextChange(TextSpan.FromBounds(previousToken.Span.End, operatorToken.SpanStart), " "),
				new TextChange(TextSpan.FromBounds(operatorToken.Span.End, nextToken.SpanStart), whitespaceBeforeOperator)) :
			sourceText.WithChanges(new TextChange(TextSpan.FromBounds(previousToken.Span.End, operatorToken.SpanStart), " "));
		return document.WithText(changedText);
	}

	private static bool ContainsOnlyWhitespace(string text) => text.All(char.IsWhiteSpace);
}
