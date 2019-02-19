using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Faithlife.Analyzers
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class StringAnalyzer : DiagnosticAnalyzer
	{
		public override void Initialize(AnalysisContext analysisContext)
		{
			analysisContext.RegisterCompilationStartAction(compilationStartAnalysisContext =>
			{
				compilationStartAnalysisContext.RegisterOperationBlockStartAction(context =>
				{
					context.RegisterOperationAction(AnalyzeOperation, OperationKind.InterpolatedString);
				});
			});
		}

		public const string DiagnosticId = "FL0007";

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_rule);

		private static void AnalyzeOperation(OperationAnalysisContext context)
		{
			var invocationOperation = (IInterpolatedStringOperation) context.Operation;
			var foundDollarSign = false;

			foreach (var child in invocationOperation.Children)
			{
				if ((child as IInterpolatedStringTextOperation)?.Text.Syntax.ToFullString().EndsWith("$", StringComparison.Ordinal) ?? false)
				{
					foundDollarSign = true;
				}
				else if ((child is IInterpolatedStringContentOperation) && foundDollarSign)
				{
					context.ReportDiagnostic(Diagnostic.Create(s_rule, child.Syntax.GetLocation()));
				}
				else
				{
					foundDollarSign = false;
				}
			}
		}

		static readonly DiagnosticDescriptor s_rule = new DiagnosticDescriptor(
			id: DiagnosticId,
			title: "Unintentional ${} in interpolated strings",
			messageFormat: "Avoid using ${} in interpolated strings.",
			category: "Usage",
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{DiagnosticId}");
	}
}
