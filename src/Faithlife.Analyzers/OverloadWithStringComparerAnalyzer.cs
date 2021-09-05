using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Faithlife.Analyzers
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class OverloadWithStringComparerAnalyzer : DiagnosticAnalyzer
	{
		public override void Initialize(AnalysisContext context)
		{
			context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
			{
				var enumerableType = compilationStartAnalysisContext.Compilation.GetTypeByMetadataName("System.Linq.Enumerable");
				if (enumerableType != null)
				{
					compilationStartAnalysisContext.RegisterOperationAction(operationContext =>
					{
						var operation = (IInvocationOperation) operationContext.Operation;
						var targetMethod = operation.TargetMethod;

						if (targetMethod != null &&
							targetMethod.ContainingType == enumerableType &&
							(targetMethod.Name == "OrderBy" || targetMethod.Name == "OrderByDescending" || targetMethod.Name == "ThenBy" || targetMethod.Name == "ThenByDescending") &&
							targetMethod.TypeArguments.Length == 2 &&
							targetMethod.TypeArguments[1].SpecialType == SpecialType.System_String &&
							targetMethod.Parameters.Length == 2)
						{
							operationContext.ReportDiagnostic(Diagnostic.Create(s_useStringComparerRule, operation.GetMethodNameLocation()));
						}
					}, OperationKind.Invocation);
				}
			});
		}

		public const string UseStringComparerDiagnosticId = "FL0006";

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => s_rules;

		private static readonly DiagnosticDescriptor s_useStringComparerRule = new DiagnosticDescriptor(
			id: UseStringComparerDiagnosticId,
			title: "Use IComparer<string> overload",
			messageFormat: "Use the overload that takes an IComparer<string>.",
			category: "Usage",
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			description: "The desired comparer must be explicitly specified. Consider StringComparer.Ordinal.",
			helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{UseStringComparerDiagnosticId}");

		private static readonly ImmutableArray<DiagnosticDescriptor> s_rules = ImmutableArray.Create(s_useStringComparerRule);
	}
}
