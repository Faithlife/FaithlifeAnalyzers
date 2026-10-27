using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace Faithlife.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantCancellationCheckCodeFixProvider)), Shared]
public sealed class RedundantCancellationCheckCodeFixProvider : CodeFixProvider
{
	public override ImmutableArray<string> FixableDiagnosticIds => [RedundantCancellationCheckAnalyzer.DiagnosticId];

	public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		var diagnostic = context.Diagnostics[0];
		if (root?.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<ExpressionStatementSyntax>() is not { } statement)
			return;

		context.RegisterCodeFix(
			CodeAction.Create(
				title: "Remove redundant ThrowIfCanceled call",
				createChangedDocument: token => RemoveStatementAsync(context.Document, statement, token),
				equivalenceKey: "remove-redundant-cancellation-check"),
			diagnostic);
	}

	private static async Task<Document> RemoveStatementAsync(Document document, ExpressionStatementSyntax statement, CancellationToken cancellationToken)
	{
		var root = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))!;
		SyntaxNode node = statement.Parent is GlobalStatementSyntax global ? global : statement;
		var newLine = root.DescendantTrivia().FirstOrDefault(x => x.IsKind(SyntaxKind.EndOfLineTrivia));
		if (newLine == default)
			newLine = SyntaxFactory.EndOfLine("\n");
		var interiorComments = statement.DescendantTrivia()
			.Where(x => statement.Span.Contains(x.Span) &&
				(x.IsKind(SyntaxKind.SingleLineCommentTrivia) || x.IsKind(SyntaxKind.MultiLineCommentTrivia)))
			.ToArray();
		if (interiorComments.Length != 0)
		{
			var leadingTrivia = node.GetLeadingTrivia();
			foreach (var comment in interiorComments)
				leadingTrivia = leadingTrivia.Add(comment).Add(newLine);
			var annotation = new SyntaxAnnotation();
			root = root.ReplaceNode(node, node.WithLeadingTrivia(leadingTrivia).WithAdditionalAnnotations(annotation));
			node = root.GetAnnotatedNodes(annotation).Single();
		}

		var keepTrivia = node.GetLeadingTrivia().Concat(node.GetTrailingTrivia())
			.Any(x => !x.IsKind(SyntaxKind.WhitespaceTrivia) && !x.IsKind(SyntaxKind.EndOfLineTrivia));
		var options = SyntaxRemoveOptions.KeepUnbalancedDirectives |
			(keepTrivia ? SyntaxRemoveOptions.KeepExteriorTrivia : SyntaxRemoveOptions.KeepNoTrivia);
		var start = node.FullSpan.Start;
		root = root.RemoveNode(node, options)!;
		document = document.WithSyntaxRoot(root);
		if (!keepTrivia)
			return document;

		var nextToken = root.FindToken(start);
		var formattingOptions = (await document.GetOptionsAsync(cancellationToken).ConfigureAwait(false))
			.WithChangedOption(FormattingOptions.NewLine, LanguageNames.CSharp, newLine.ToFullString());
		return await Formatter.FormatAsync(document, nextToken.FullSpan, formattingOptions, cancellationToken).ConfigureAwait(false);
	}
}
