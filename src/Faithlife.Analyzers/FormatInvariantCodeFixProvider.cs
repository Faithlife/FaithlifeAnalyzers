using System.Collections.Immutable;
using System.Composition;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Faithlife.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FormatInvariantCodeFixProvider)), Shared]
public sealed class FormatInvariantCodeFixProvider : CodeFixProvider
{
	public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(FormatInvariantAnalyzer.DiagnosticId);

	public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

		var diagnostic = context.Diagnostics.First();
		var diagnosticSpan = diagnostic.Location.SourceSpan;

		var diagnosticNode = root.FindNode(diagnosticSpan);

		var invocation = diagnosticNode.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault();
		if (invocation is null)
			return;

		if (invocation.Expression is not MemberAccessExpressionSyntax { Expression: LiteralExpressionSyntax invocationTarget } || !invocationTarget.IsKind(SyntaxKind.StringLiteralExpression))
			return;

		var formatString = invocationTarget.Token.Value as string;
		if (formatString is null)
			return;

		var interpolatedStringExpression = InterpolatedStringExpression(Token(SyntaxKind.InterpolatedStringStartToken));
		var matches = Regex.Matches(formatString, @"(?!\\){\s*(\d+)(,-?\d+)?(:[^}]+)?\s*}");
		if (matches.Count == 0)
			return;

		var requiresInvariant = false;
		var stringType = semanticModel.Compilation.GetSpecialType(SpecialType.System_String);

		var index = 0;
		foreach (Match match in matches)
		{
			if (index < match.Index)
				interpolatedStringExpression = interpolatedStringExpression.AddContents(InterpolatedStringText(formatString.Substring(index, match.Index - index)));

			if (!int.TryParse(match.Groups[1].Value, out var argIndex) || argIndex < 0 || argIndex > invocation.ArgumentList.Arguments.Count )
				return;

			var arg = invocation.ArgumentList.Arguments[argIndex];
			if (!requiresInvariant)
			{
				var typeInfo = semanticModel.GetTypeInfo(arg.Expression);
				if (!SymbolEqualityComparer.Default.Equals(typeInfo.Type, stringType))
					requiresInvariant = true;
			}

			var interpolation = Interpolation(SyntaxUtility.SimplifiableParentheses(arg.Expression));
			if (match.Groups[2] is { Success: true, Value: { } alignment })
				interpolation = interpolation.WithAlignmentClause(InterpolationAlignmentClause(Token(SyntaxKind.CommaToken), ParseExpression(alignment.Substring(1))));
			if (match.Groups[3] is { Success: true, Value: { } format })
				interpolation = interpolation.WithFormatClause(InterpolationFormatClause(Token(SyntaxKind.ColonToken), InterpolatedStringTextToken(format.Substring(1))));
			interpolatedStringExpression = interpolatedStringExpression.AddContents(interpolation);

			index = match.Index + match.Length;
		}

		if (index < formatString.Length)
			interpolatedStringExpression = interpolatedStringExpression.AddContents(InterpolatedStringText(formatString.Substring(index)));

		ExpressionSyntax replacement = interpolatedStringExpression;
		if (requiresInvariant)
		{
			replacement = InvocationExpression(
				MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					ParseExpression("global::System.FormattableString").WithAdditionalAnnotations(Simplifier.Annotation),
					IdentifierName("Invariant")),
				ArgumentList().AddArguments(Argument(interpolatedStringExpression)));
		}

		context.RegisterCodeFix(
			CodeAction.Create(
				title: "Use interpolated string",
				createChangedDocument: token => ReplaceValueAsync(
					context.Document,
					invocation,
					SyntaxUtility.SimplifiableParentheses(replacement),
					token),
				c_fixName),
			diagnostic);
	}

	private static InterpolatedStringTextSyntax InterpolatedStringText(string text) =>
		SyntaxFactory.InterpolatedStringText().WithTextToken(InterpolatedStringTextToken(text));

	private static SyntaxToken InterpolatedStringTextToken(string text)
	{
		var escapedText = text
			.Replace("\\", "\\\\")
			.Replace("\r", "\\r")
			.Replace("\n", "\\n")
			.Replace("\"", "\\\"");
		return Token(
			TriviaList(),
			SyntaxKind.InterpolatedStringTextToken,
			escapedText,
			escapedText,
			TriviaList());
	}

	private static async Task<Document> ReplaceValueAsync(Document document, SyntaxNode replacementTarget, SyntaxNode replacementNode, CancellationToken cancellationToken)
	{
		var root = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))
			.ReplaceNode(replacementTarget, replacementNode);

		var usingDirective = root.DescendantNodes()
			.OfType<UsingDirectiveSyntax>()
			.FirstOrDefault(x => AreEquivalent(x.Name, s_libronixUtilityNamespace));

		if (usingDirective != null)
			root = root.ReplaceNode(usingDirective, usingDirective.WithAdditionalAnnotations(Simplifier.Annotation));

		return await Simplifier.ReduceAsync(
			await Simplifier.ReduceAsync(document.WithSyntaxRoot(root), cancellationToken: cancellationToken).ConfigureAwait(false),
			cancellationToken: cancellationToken).ConfigureAwait(false);
	}

	private static readonly NameSyntax s_libronixUtilityNamespace = ParseName("Libronix.Utility");

	private const string c_fixName = "use-modern-language-features";
}
