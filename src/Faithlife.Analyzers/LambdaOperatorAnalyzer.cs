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

		context.RegisterSyntaxNodeAction(AnalyzeArrowExpressionClause, SyntaxKind.ArrowExpressionClause);
		context.RegisterSyntaxNodeAction(AnalyzeLambdaExpression, SyntaxKind.SimpleLambdaExpression, SyntaxKind.ParenthesizedLambdaExpression);
		context.RegisterSyntaxNodeAction(AnalyzeSwitchExpressionArm, SyntaxKind.SwitchExpressionArm);
	}

	public const string DiagnosticId = "FL0024";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

	private static void AnalyzeArrowExpressionClause(SyntaxNodeAnalysisContext context)
	{
		var arrowExpressionClause = (ArrowExpressionClauseSyntax) context.Node;
		AnalyzeOperator(context, arrowExpressionClause.ArrowToken);
	}

	private static void AnalyzeLambdaExpression(SyntaxNodeAnalysisContext context)
	{
		var lambdaExpression = (LambdaExpressionSyntax) context.Node;
		AnalyzeOperator(context, lambdaExpression.ArrowToken);
	}

	private static void AnalyzeSwitchExpressionArm(SyntaxNodeAnalysisContext context)
	{
		var switchExpressionArm = (SwitchExpressionArmSyntax) context.Node;
		AnalyzeOperator(context, switchExpressionArm.EqualsGreaterThanToken);
	}

	private static void AnalyzeOperator(SyntaxNodeAnalysisContext context, SyntaxToken operatorToken)
	{
		var previousToken = operatorToken.GetPreviousToken();
		if (previousToken.IsKind(SyntaxKind.None))
			return;

		var sourceText = operatorToken.SyntaxTree.GetText(context.CancellationToken);
		if (!IsFirstNonWhitespaceOnLine(sourceText, operatorToken) ||
			!ContainsOnlyWhitespace(sourceText, TextSpan.FromBounds(previousToken.Span.End, operatorToken.SpanStart)))
		{
			return;
		}

		context.ReportDiagnostic(Diagnostic.Create(s_rule, operatorToken.GetLocation()));
	}

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
