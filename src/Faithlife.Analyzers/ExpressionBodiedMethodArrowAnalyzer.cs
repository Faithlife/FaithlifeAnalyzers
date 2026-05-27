using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Faithlife.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExpressionBodiedMethodArrowAnalyzer : DiagnosticAnalyzer
{
	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterSyntaxNodeAction(AnalyzeSyntax, SyntaxKind.MethodDeclaration);
	}

	public const string DiagnosticId = "FL0024";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

	private static void AnalyzeSyntax(SyntaxNodeAnalysisContext context)
	{
		var methodDeclaration = (MethodDeclarationSyntax) context.Node;
		var arrowToken = methodDeclaration.ExpressionBody?.ArrowToken;
		if (arrowToken is null)
			return;

		var previousToken = arrowToken.Value.GetPreviousToken();
		if (previousToken.IsKind(SyntaxKind.None))
			return;

		var sourceText = arrowToken.Value.SyntaxTree.GetText(context.CancellationToken);
		if (!IsFirstNonWhitespaceOnLine(sourceText, arrowToken.Value) ||
			!ContainsOnlyWhitespace(sourceText, TextSpan.FromBounds(previousToken.Span.End, arrowToken.Value.SpanStart)))
		{
			return;
		}

		context.ReportDiagnostic(Diagnostic.Create(s_rule, arrowToken.Value.GetLocation()));
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
		title: "Expression-bodied method arrow should end the previous line",
		messageFormat: "Move => to the end of the previous line",
		category: "Style",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/blob/-/docs/{DiagnosticId}.md");
}
