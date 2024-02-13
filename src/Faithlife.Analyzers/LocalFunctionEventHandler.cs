using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Faithlife.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class LocalFunctionEventHandler : DiagnosticAnalyzer
{
	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SubtractAssignmentExpression);
	}

	private void Analyze(SyntaxNodeAnalysisContext context)
	{
		var assignment = (AssignmentExpressionSyntax) context.Node;
		if (context.SemanticModel.GetSymbolInfo(assignment.Left).Symbol is not IEventSymbol)
			return;
		if (context.SemanticModel.GetSymbolInfo(assignment.Right).Symbol is not IMethodSymbol methodGroup)
			return;
		if (methodGroup is { MethodKind: MethodKind.LocalFunction, IsStatic: false })
			context.ReportDiagnostic(Diagnostic.Create(s_localFunctionRule, assignment.GetLocation()));
		else if (methodGroup.MethodKind == MethodKind.AnonymousFunction)
			context.ReportDiagnostic(Diagnostic.Create(s_lambdaRule, assignment.GetLocation()));
	}

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_localFunctionRule, s_lambdaRule);

	public const string LocalFunctionDiagnosticId = "FL0019";
	public const string LambdaDiagnosticId = "FL0020";

	private static readonly DiagnosticDescriptor s_localFunctionRule = new(
		id: LocalFunctionDiagnosticId,
		title: "Local Functions as Event Handlers",
		messageFormat: "Local functions should probably not be used as event handlers (unless they are static).",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{LocalFunctionDiagnosticId}"
	);

	private static readonly DiagnosticDescriptor s_lambdaRule = new(
		id: LambdaDiagnosticId,
		title: "Lambda Expressions as Event Handlers",
		messageFormat: "Lambda expressions may not be used as event handlers.",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{LambdaDiagnosticId}"
	);
}
