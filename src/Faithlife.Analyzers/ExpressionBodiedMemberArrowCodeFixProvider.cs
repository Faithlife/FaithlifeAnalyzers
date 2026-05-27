using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Faithlife.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ExpressionBodiedMemberArrowCodeFixProvider)), Shared]
public sealed class ExpressionBodiedMemberArrowCodeFixProvider : CodeFixProvider
{
	public override ImmutableArray<string> FixableDiagnosticIds => [ExpressionBodiedMemberArrowAnalyzer.DiagnosticId];

	public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		if (root is null)
			return;

		var diagnostic = context.Diagnostics.First();
		var arrowToken = root.FindToken(diagnostic.Location.SourceSpan.Start);
		if (!arrowToken.IsKind(SyntaxKind.EqualsGreaterThanToken))
			return;

		var previousToken = arrowToken.GetPreviousToken();
		if (previousToken.IsKind(SyntaxKind.None))
			return;

		context.RegisterCodeFix(
			CodeAction.Create(
				title: "Move => to the previous line",
				createChangedDocument: token => MoveArrowToPreviousLineAsync(context.Document, previousToken, arrowToken, token),
				"move-expression-bodied-member-arrow"),
			diagnostic);
	}

	private static async Task<Document> MoveArrowToPreviousLineAsync(Document document, SyntaxToken previousToken, SyntaxToken arrowToken, CancellationToken cancellationToken)
	{
		var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
		var nextToken = arrowToken.GetNextToken();
		var whitespaceBeforeArrow = sourceText.ToString(TextSpan.FromBounds(previousToken.Span.End, arrowToken.SpanStart));
		var changedText = sourceText.WithChanges(
			new TextChange(TextSpan.FromBounds(previousToken.Span.End, arrowToken.SpanStart), " "),
			new TextChange(TextSpan.FromBounds(arrowToken.Span.End, nextToken.SpanStart), whitespaceBeforeArrow));
		return document.WithText(changedText);
	}
}
