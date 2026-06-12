using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Faithlife.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LambdaOperatorAnalyzer : DiagnosticAnalyzer
{
	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterSyntaxNodeAction(AnalyzeSyntax,
			SyntaxKind.ArrowExpressionClause,
			SyntaxKind.SimpleLambdaExpression,
			SyntaxKind.ParenthesizedLambdaExpression,
			SyntaxKind.SwitchExpressionArm);
	}

	public const string DiagnosticId = "FL0024";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

	private static void AnalyzeSyntax(SyntaxNodeAnalysisContext context)
	{
		var lambdaOperatorToken = GetLambdaOperatorToken(context.Node);
		if (!lambdaOperatorToken.IsKind(SyntaxKind.EqualsGreaterThanToken))
			return;

		var previousToken = lambdaOperatorToken.GetPreviousToken();
		if (previousToken.IsKind(SyntaxKind.None))
			return;

		var sourceText = lambdaOperatorToken.SyntaxTree.GetText(context.CancellationToken);
		if (!IsFirstNonWhitespaceOnLine(sourceText, lambdaOperatorToken) ||
			!ContainsOnlyWhitespace(sourceText, TextSpan.FromBounds(previousToken.Span.End, lambdaOperatorToken.SpanStart)))
		{
			return;
		}

		context.ReportDiagnostic(Diagnostic.Create(s_rule, lambdaOperatorToken.GetLocation()));
	}

	private static SyntaxToken GetLambdaOperatorToken(SyntaxNode node) =>
		node switch
		{
			ArrowExpressionClauseSyntax arrowExpressionClause => arrowExpressionClause.ArrowToken,
			ParenthesizedLambdaExpressionSyntax parenthesizedLambdaExpression => parenthesizedLambdaExpression.ArrowToken,
			SimpleLambdaExpressionSyntax simpleLambdaExpression => simpleLambdaExpression.ArrowToken,
			SwitchExpressionArmSyntax switchExpressionArm => switchExpressionArm.EqualsGreaterThanToken,
			_ => default,
		};

	private static bool IsFirstNonWhitespaceOnLine(SourceText sourceText, SyntaxToken token)
	{
		var line = sourceText.Lines.GetLineFromPosition(token.SpanStart);
		return ContainsOnlyWhitespace(sourceText, TextSpan.FromBounds(line.Start, token.SpanStart));
	}

	private static bool ContainsOnlyWhitespace(SourceText sourceText, TextSpan span)
	{
		for (int index = span.Start; index < span.End; index++)
		{
			if (!char.IsWhiteSpace(sourceText[index]))
				return false;
		}

		return true;
	}

	private static readonly DiagnosticDescriptor s_rule = new(
		id: DiagnosticId,
		title: "Lambda operator should end the previous line",
		messageFormat: "Move => to the end of the previous line",
		category: "Style",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/blob/-/docs/{DiagnosticId}.md");
}
