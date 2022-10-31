using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Faithlife.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UriToStringAnalyzer : DiagnosticAnalyzer
{
	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
		{
			var uriType = compilationStartAnalysisContext.Compilation.GetTypeByMetadataName("System.Uri");
			if (uriType is null)
				return;

			if (uriType.GetMembers("ToString") is { Length: 1 } members)
				compilationStartAnalysisContext.RegisterOperationAction(x => AnalyzeOperation(x, members[0]), OperationKind.Invocation);
		});
	}

	private static void AnalyzeOperation(OperationAnalysisContext context, ISymbol uriToStringMethod)
	{
		var invocation = (IInvocationOperation) context.Operation;
		if (!SymbolEqualityComparer.Default.Equals(uriToStringMethod, invocation.TargetMethod))
			return;

		context.ReportDiagnostic(Diagnostic.Create(s_rule, invocation.Syntax.GetLocation()));
	}

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_rule);

	public const string DiagnosticId = "FL0013";

	private static readonly DiagnosticDescriptor s_rule = new(
		id: DiagnosticId,
		title: "Uri ToString Usage",
		messageFormat: "Do not use Uri.ToString()",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{DiagnosticId}"
	);
}
