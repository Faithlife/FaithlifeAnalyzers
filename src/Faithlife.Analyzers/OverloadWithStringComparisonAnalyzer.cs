using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Faithlife.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OverloadWithStringComparisonAnalyzer : DiagnosticAnalyzer
{
	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		// NOTE: some parts of this implementation derived from https://github.com/dotnet/roslyn-analyzers/blob/7a2540618fc32c5b38bdb43bc3a70ba6401ed135/src/Microsoft.NetCore.Analyzers/Core/Runtime/UseOrdinalStringComparison.cs
		context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
		{
			var cultureInfoType = compilationStartAnalysisContext.Compilation.GetTypeByMetadataName("System.Globalization.CultureInfo");

			if (compilationStartAnalysisContext.Compilation.GetTypeByMetadataName("System.StringComparison") is { } stringComparisonType)
			{
				compilationStartAnalysisContext.RegisterOperationAction(operationContext =>
				{
					var operation = (IInvocationOperation) operationContext.Operation;
					var methodSymbol = operation.TargetMethod;
					if (methodSymbol?.ContainingType.SpecialType == SpecialType.System_String && s_affectedMethods.Contains(methodSymbol.Name))
					{
						if (!IsAcceptableOverload(methodSymbol, stringComparisonType, cultureInfoType))
						{
							// wrong overload
							var rule = (methodSymbol.Name == "Equals" && ((methodSymbol.Parameters.Length == 1 && !methodSymbol.IsStatic) || (methodSymbol.Parameters.Length == 2 && methodSymbol.IsStatic))) ?
								s_avoidStringEqualsRule : s_useStringComparisonRule;
							operationContext.ReportDiagnostic(Diagnostic.Create(rule, operation.GetMethodNameLocation()));
						}
						else if (methodSymbol.Name == "Equals" && ((methodSymbol.Parameters.Length == 2 && !methodSymbol.IsStatic) || (methodSymbol.Parameters.Length == 3 && methodSymbol.IsStatic)))
						{
							var lastArgument = operation.Arguments.Last();
							if (lastArgument.Value.Kind == OperationKind.FieldReference)
							{
								var fieldSymbol = ((IFieldReferenceOperation) lastArgument.Value).Field;
								if (SymbolEqualityComparer.Default.Equals(fieldSymbol?.ContainingType, stringComparisonType) && fieldSymbol.Name == "Ordinal")
								{
									// right overload, wrong value
									operationContext.ReportDiagnostic(Diagnostic.Create(s_avoidStringEqualsRule, operation.GetMethodNameLocation()));
								}
							}
						}
					}
				}, OperationKind.Invocation);
			}
		});
	}

	public const string UseStringComparisonDiagnosticId = "FL0002";
	public const string AvoidStringEqualsDiagnosticId = "FL0004";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => s_rules;

	private static bool IsAcceptableOverload(IMethodSymbol methodSymbol, INamedTypeSymbol stringComparisonType, INamedTypeSymbol cultureInfoType)
	{
		foreach (var parameter in methodSymbol.Parameters)
		{
			if (parameter.Type.SpecialType == SpecialType.System_Object)
				return true;
			if (parameter.Type.SpecialType == SpecialType.System_Char)
				return true;
			if (SymbolEqualityComparer.Default.Equals(parameter.Type, stringComparisonType))
				return true;
			if (SymbolEqualityComparer.Default.Equals(parameter.Type, cultureInfoType))
				return true;
		}

		return false;
	}

	private static readonly HashSet<string> s_affectedMethods = ["Equals", "Compare", "IndexOf", "LastIndexOf", "StartsWith", "EndsWith"];

	private static readonly DiagnosticDescriptor s_useStringComparisonRule = new(
		id: UseStringComparisonDiagnosticId,
		title: "Use StringComparison overload",
		messageFormat: "Use an overload that takes a StringComparison",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "The desired StringComparison must be explicitly specified.",
		helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{UseStringComparisonDiagnosticId}");

	private static readonly DiagnosticDescriptor s_avoidStringEqualsRule = new(
		id: AvoidStringEqualsDiagnosticId,
		title: "Avoid string.Equals(string, string)",
		messageFormat: "Use operator== or a non-ordinal StringComparison",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{AvoidStringEqualsDiagnosticId}");

	private static readonly ImmutableArray<DiagnosticDescriptor> s_rules = [s_useStringComparisonRule, s_avoidStringEqualsRule];
}
