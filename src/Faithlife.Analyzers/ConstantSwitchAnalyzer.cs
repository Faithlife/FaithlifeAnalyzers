using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Faithlife.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConstantSwitchAnalyzer : DiagnosticAnalyzer
{
	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
		{
			compilationStartAnalysisContext.RegisterOperationAction(AnalyzeOperation, OperationKind.SwitchExpression);
		});
	}

	public const string DiagnosticId = "FL0017";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

	private static void AnalyzeOperation(OperationAnalysisContext context)
	{
		var operation = (ISwitchExpressionOperation) context.Operation;
		if (operation.Value.ConstantValue.HasValue)
			context.ReportDiagnostic(Diagnostic.Create(s_rule, context.Operation.Syntax.GetLocation()));
	}

	private static readonly DiagnosticDescriptor s_rule = new(
		id: DiagnosticId,
		title: "Do not switch on a constant value",
		messageFormat: "Do not use a constant value as the target of a switch expression",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{DiagnosticId}");
}
