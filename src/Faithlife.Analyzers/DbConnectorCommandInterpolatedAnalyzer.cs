using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Faithlife.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DbConnectorCommandInterpolatedAnalyzer : DiagnosticAnalyzer
{
	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
		{
			if (compilationStartAnalysisContext.Compilation.GetTypeByMetadataName("Faithlife.Data.DbConnector") is { } dbConnectorType)
			{
				compilationStartAnalysisContext.RegisterSyntaxNodeAction(syntaxNodeAnalysisContext =>
				{
					if (syntaxNodeAnalysisContext.Node is InvocationExpressionSyntax invocation &&
						syntaxNodeAnalysisContext.SemanticModel.GetSymbolInfo(invocation.Expression).Symbol is IMethodSymbol method &&
						Equals(method.ContainingType, dbConnectorType) &&
						method.Name == "Command" &&
						method.Parameters.Length != 0 &&
						method.Parameters[0].Type.SpecialType == SpecialType.System_String &&
						invocation.ArgumentList.Arguments[0].Expression is InterpolatedStringExpressionSyntax)
					{
						syntaxNodeAnalysisContext.ReportDiagnostic(Diagnostic.Create(s_rule, invocation.GetLocation()));
					}
				}, SyntaxKind.InvocationExpression);
			}
		});
	}

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_rule);

	public const string DiagnosticId = "FL0012";

	private static readonly DiagnosticDescriptor s_rule = new(
		id: DiagnosticId,
		title: "Use DbConnector.CommandFormat",
		messageFormat: "Command should not be used with an interpolated string; use CommandFormat instead.",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{DiagnosticId}"
	);
}
