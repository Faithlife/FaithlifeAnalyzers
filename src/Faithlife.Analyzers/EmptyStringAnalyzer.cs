using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Faithlife.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EmptyStringAnalyzer : DiagnosticAnalyzer
{
	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
		{
			var stringClass = compilationStartAnalysisContext.Compilation.GetTypeByMetadataName("System.String");
			if (stringClass == null)
				return;

			var emptyString = stringClass.GetMembers("Empty");
			if (emptyString.Length != 1)
				return;

			compilationStartAnalysisContext.RegisterOperationAction(x => AnalyzeOperation(x, emptyString[0]), OperationKind.FieldReference);
		});
	}

	public const string DiagnosticId = "FL0009";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_rule);

	private static void AnalyzeOperation(OperationAnalysisContext context, ISymbol emptyString)
	{
		var fieldReferenceOperation = (IFieldReferenceOperation) context.Operation;
		if (!fieldReferenceOperation.Field.Equals(emptyString))
			return;

		context.ReportDiagnostic(Diagnostic.Create(s_rule, context.Operation.Syntax.GetLocation()));
	}

	private static readonly DiagnosticDescriptor s_rule = new DiagnosticDescriptor(
		id: DiagnosticId,
		title: "Prefer \"\" over string.Empty",
		messageFormat: "Prefer \"\" over string.Empty",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{DiagnosticId}");
}
