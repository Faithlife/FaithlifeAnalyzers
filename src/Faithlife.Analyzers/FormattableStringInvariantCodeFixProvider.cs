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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FormattableStringInvariantCodeFixProvider)), Shared]
public sealed class FormattableStringInvariantCodeFixProvider : CodeFixProvider
{
	public sealed override ImmutableArray<string> FixableDiagnosticIds => [FormattableStringInvariantAnalyzer.DiagnosticId];

	public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		if (root is not CompilationUnitSyntax)
			return;

		var diagnostic = context.Diagnostics.First();
		var invocation = root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<InvocationExpressionSyntax>();
		if (invocation?.Expression is not MemberAccessExpressionSyntax memberAccess ||
			memberAccess.DescendantTrivia().Any(x => x.IsDirective || x.IsKind(SyntaxKind.SingleLineCommentTrivia) || x.IsKind(SyntaxKind.MultiLineCommentTrivia)))
			return;

		var annotation = new SyntaxAnnotation();
		var changedDocument = await ReplaceValueAsync(context.Document, invocation, annotation, context.CancellationToken).ConfigureAwait(false);
		if (!await FormattableStringInvariantUsing.BindsToInvariantAsync(changedDocument, annotation, context.CancellationToken).ConfigureAwait(false))
			return;

		context.RegisterCodeFix(
			CodeAction.Create(
				title: "Use Invariant with a static import",
				createChangedDocument: _ => Task.FromResult(changedDocument),
				"use-formattablestring-static-import"),
			diagnostic);
	}

	private static async Task<Document> ReplaceValueAsync(Document document, InvocationExpressionSyntax invocation, SyntaxAnnotation annotation, CancellationToken cancellationToken)
	{
		var root = (CompilationUnitSyntax) (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))!;
		var originalLeadingTrivia = root.GetLeadingTrivia();
		var memberAccess = (MemberAccessExpressionSyntax) invocation.Expression;
		var replacement = invocation.WithExpression(IdentifierName("Invariant").WithTriviaFrom(memberAccess)).WithAdditionalAnnotations(annotation);
		root = root.ReplaceNode(invocation, replacement);
		root = FormattableStringInvariantUsing.AddIfMissing(root, invocation);

		if (memberAccess.Expression is IdentifierNameSyntax receiver)
		{
			var usingDirective = root.Usings.FirstOrDefault(x =>
				x.Alias?.Name.Identifier.ValueText == receiver.Identifier.ValueText ||
				x.Alias is null && x.Name?.ToString() == "System" && receiver.Identifier.ValueText == "FormattableString");
			if (usingDirective is not null)
				root = root.ReplaceNode(usingDirective, usingDirective.WithAdditionalAnnotations(Simplifier.Annotation));
		}

		document = await Simplifier.ReduceAsync(document.WithSyntaxRoot(root), cancellationToken: cancellationToken).ConfigureAwait(false);
		root = (CompilationUnitSyntax) (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))!;
		return document.WithSyntaxRoot(root.WithLeadingTrivia(originalLeadingTrivia));
	}
}
