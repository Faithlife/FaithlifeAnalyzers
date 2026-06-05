using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Faithlife.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(InvariantConvertCodeFixProvider)), Shared]
public sealed class InvariantConvertCodeFixProvider : CodeFixProvider
{
	public sealed override ImmutableArray<string> FixableDiagnosticIds => [InvariantConvertAnalyzer.DiagnosticId];

	public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		if (root is null)
			return;

		var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
		if (semanticModel is null || !InvariantConvertAnalyzer.HasInvariantConvert(semanticModel.Compilation))
			return;

		var diagnostic = context.Diagnostics.First();
		var diagnosticNode = root.FindNode(diagnostic.Location.SourceSpan);
		var invocation = diagnosticNode.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault();
		if (invocation is null)
			return;

		var match = InvariantConvertAnalyzer.TryCreateMatch(invocation, semanticModel, context.CancellationToken);
		if (match is null || !match.CanFix)
			return;

		context.RegisterCodeFix(
			CodeAction.Create(
				title: "Use InvariantConvert",
				createChangedDocument: token => ReplaceValueAsync(context.Document, match, token),
				"use-invariantconvert"),
			diagnostic);
	}

	private static async Task<Document> ReplaceValueAsync(Document document, InvariantConvertMatch match, CancellationToken cancellationToken)
	{
		var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		var replacement = CreateReplacement(match).WithTriviaFrom(match.Invocation).WithAdditionalAnnotations(
			Formatter.Annotation,
			Simplifier.Annotation,
			s_addImportsAnnotation);
		root = root.ReplaceNode(GetReplacementTarget(match), replacement);
		document = await ImportAdder.AddImportsAsync(document.WithSyntaxRoot(root), s_addImportsAnnotation, null, cancellationToken).ConfigureAwait(false);

		root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false) ?? root;

		foreach (var namespaceName in s_simplifiableNamespaces)
			root = SimplifyUsing(root, namespaceName);

		return await Simplifier.ReduceAsync(
			await Simplifier.ReduceAsync(document.WithSyntaxRoot(root), cancellationToken: cancellationToken).ConfigureAwait(false),
			cancellationToken: cancellationToken).ConfigureAwait(false);
	}

	private static SyntaxNode GetReplacementTarget(InvariantConvertMatch match)
	{
		if (match.Kind != InvariantConvertMatchKind.TryParse || !match.IsNegated)
			return match.Invocation;

		SyntaxNode currentNode = match.Invocation;
		while (currentNode.Parent is ParenthesizedExpressionSyntax)
			currentNode = currentNode.Parent;

		if (currentNode.Parent is PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.LogicalNotExpression } logicalNotExpression)
		{
			return logicalNotExpression;
		}

		return match.Invocation;
	}

	private static ExpressionSyntax CreateReplacement(InvariantConvertMatch match) =>
		match.Kind switch
		{
			InvariantConvertMatchKind.Parse => CreateInvariantConvertInvocation(match.MethodName, match.InputExpression!),
			InvariantConvertMatchKind.TryParse => CreateTryParseReplacement(match),
			InvariantConvertMatchKind.ToString => CreateInvariantConvertInvocation(match.MethodName, match.ReceiverExpression!),
			_ => throw new InvalidOperationException("Unsupported InvariantConvert match."),
		};

	private static ExpressionSyntax CreateTryParseReplacement(InvariantConvertMatch match)
	{
		var replacement = CreateInvariantConvertInvocation(match.MethodName, match.InputExpression!);
		var patternText = match.IsNegated ?
			$"value is not {{ }} {match.OutVariableIdentifier.ValueText}" :
			$"value is {{ }} {match.OutVariableIdentifier.ValueText}";

		return SyntaxUtility.ReplaceIdentifier(ParseExpression(patternText), "value", replacement);
	}

	private static ExpressionSyntax CreateInvariantConvertInvocation(string methodName, ExpressionSyntax inputExpression) =>
		SyntaxUtility.ReplaceIdentifier(
			ParseExpression($"global::Libronix.Utility.Invariant.InvariantConvert.{methodName}(value)"),
			"value",
			inputExpression);

	private static SyntaxNode SimplifyUsing(SyntaxNode root, NameSyntax namespaceName)
	{
		var usingDirective = root.DescendantNodes()
			.OfType<UsingDirectiveSyntax>()
			.FirstOrDefault(x => AreEquivalent(x.Name, namespaceName));

		if (usingDirective is null)
			return root;

		return root.ReplaceNode(usingDirective, usingDirective.WithAdditionalAnnotations(Simplifier.Annotation));
	}

	private static readonly SyntaxAnnotation s_addImportsAnnotation = new();
	private static readonly NameSyntax[] s_simplifiableNamespaces =
	[
		ParseName("System.Globalization"),
		ParseName("System.Globalization.CultureInfo"),
		ParseName("System.Globalization.NumberStyles"),
		ParseName("System.Boolean"),
		ParseName("System.Double"),
		ParseName("System.Int32"),
		ParseName("System.Int64"),
	];
}
