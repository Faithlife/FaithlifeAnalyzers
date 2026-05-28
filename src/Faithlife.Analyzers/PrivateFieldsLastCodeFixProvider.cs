using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

namespace Faithlife.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PrivateFieldsLastCodeFixProvider)), Shared]
public sealed class PrivateFieldsLastCodeFixProvider : CodeFixProvider
{
	public override ImmutableArray<string> FixableDiagnosticIds => [PrivateFieldsLastAnalyzer.DiagnosticId];

	public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		if (root?.FindNode(context.Diagnostics[0].Location.SourceSpan) is not { } node)
			return;

		if (node is not FieldDeclarationSyntax fieldDeclaration)
			fieldDeclaration = node.FirstAncestorOrSelf<FieldDeclarationSyntax>();
		if (fieldDeclaration?.Parent is not TypeDeclarationSyntax typeDeclaration)
			return;

		context.RegisterCodeFix(
			CodeAction.Create(
				title: "Move private fields to end of type",
				createChangedDocument: token => MovePrivateFieldsAsync(context.Document, typeDeclaration, token),
				equivalenceKey: "move-private-fields-to-end-of-type"),
			context.Diagnostics);
	}

	private static async Task<Document> MovePrivateFieldsAsync(Document document, TypeDeclarationSyntax typeDeclaration, CancellationToken cancellationToken)
	{
		var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
		if (semanticModel is null)
			return document;

		var leadingPrivateFieldCount = PrivateFieldsLastAnalyzer.GetLeadingPrivateFieldCount(typeDeclaration.Members,
			member => PrivateFieldsLastAnalyzer.IsPrivateField(semanticModel, member, cancellationToken));
		if (leadingPrivateFieldCount is 0 || leadingPrivateFieldCount == typeDeclaration.Members.Count)
			return document;

		var members = typeDeclaration.Members;
		var firstMemberLeadingTrivia = GetLeadingWhitespaceTrivia(members[0].GetLeadingTrivia());
		var reorderedMembers = members.Skip(leadingPrivateFieldCount).Concat(members.Take(leadingPrivateFieldCount)).ToArray();
		var reorderedFirstMemberLeadingTrivia = firstMemberLeadingTrivia.AddRange(reorderedMembers[0].GetLeadingTrivia().SkipWhile(IsWhitespaceOrEndOfLine));
		reorderedMembers[0] = reorderedMembers[0].WithLeadingTrivia(reorderedFirstMemberLeadingTrivia);

		var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
		editor.ReplaceNode(typeDeclaration, typeDeclaration.WithMembers(default(SyntaxList<MemberDeclarationSyntax>).AddRange(reorderedMembers)).WithAdditionalAnnotations(Formatter.Annotation));
		return editor.GetChangedDocument();
	}

	private static SyntaxTriviaList GetLeadingWhitespaceTrivia(SyntaxTriviaList triviaList)
	{
		return default(SyntaxTriviaList).AddRange(triviaList.TakeWhile(IsWhitespaceOrEndOfLine));
	}

	private static bool IsWhitespaceOrEndOfLine(SyntaxTrivia trivia) =>
		trivia.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.WhitespaceTrivia) ||
		trivia.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.EndOfLineTrivia);
}
