using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Faithlife.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UseInvariantConvertAnalyzer : DiagnosticAnalyzer
{
	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
		{
			var invariantConvertContext = InvariantConvertContext.TryCreate(compilationStartAnalysisContext.Compilation);
			if (invariantConvertContext is null)
				return;

			compilationStartAnalysisContext.RegisterSyntaxNodeAction(c => AnalyzeSyntax(c, invariantConvertContext), SyntaxKind.InvocationExpression);
		});
	}

	public const string DiagnosticId = "FL0026";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

	private static void AnalyzeSyntax(SyntaxNodeAnalysisContext context, InvariantConvertContext invariantConvertContext)
	{
		var invocation = (InvocationExpressionSyntax) context.Node;

		var match = invariantConvertContext.Match(invocation, context.SemanticModel, context.CancellationToken);
		if (match is null)
			return;

		context.ReportDiagnostic(Diagnostic.Create(s_rule, invocation.GetLocation(), match.SuggestedMethodName));
	}

	private static readonly DiagnosticDescriptor s_rule = new(
		id: DiagnosticId,
		title: "Use InvariantConvert",
		messageFormat: "Use 'InvariantConvert.{0}'",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: true,
		helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/blob/-/docs/{DiagnosticId}.md");
}
