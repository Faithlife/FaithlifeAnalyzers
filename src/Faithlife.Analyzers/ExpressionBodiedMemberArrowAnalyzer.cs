using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Faithlife.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExpressionBodiedMemberArrowAnalyzer : DiagnosticAnalyzer
{
	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterSyntaxNodeAction(AnalyzeSyntax, SyntaxKind.ArrowExpressionClause);
	}

	public const string DiagnosticId = "FL0024";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

	private static void AnalyzeSyntax(SyntaxNodeAnalysisContext context)
	{
		var arrowExpressionClause = (ArrowExpressionClauseSyntax) context.Node;
		var arrowToken = arrowExpressionClause.ArrowToken;

		var previousToken = arrowToken.GetPreviousToken();
		if (previousToken.IsKind(SyntaxKind.None))
			return;

		var sourceText = arrowToken.SyntaxTree.GetText(context.CancellationToken);
		if (!IsFirstNonWhitespaceOnLine(sourceText, arrowToken) ||
			!ContainsOnlyWhitespace(sourceText, TextSpan.FromBounds(previousToken.Span.End, arrowToken.SpanStart)))
		{
			return;
		}

		context.ReportDiagnostic(Diagnostic.Create(s_rule, arrowToken.GetLocation()));
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
		title: "Expression-bodied member arrow should end the previous line",
		messageFormat: "Move => to the end of the previous line",
		category: "Style",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/blob/-/docs/{DiagnosticId}.md");
}
