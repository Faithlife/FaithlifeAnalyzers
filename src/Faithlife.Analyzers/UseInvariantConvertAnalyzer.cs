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
			var compilation = compilationStartAnalysisContext.Compilation;

			// Only offer the diagnostic if Libronix.Utility.Invariant.InvariantConvert is available to be used.
			if (compilation.GetTypeByMetadataName("Libronix.Utility.Invariant.InvariantConvert") is null)
				return;

			if (compilation.GetTypeByMetadataName("System.Globalization.CultureInfo") is not { } cultureInfoType ||
				compilation.GetTypeByMetadataName("System.Globalization.NumberStyles") is not { } numberStylesType ||
				compilation.GetTypeByMetadataName("System.IFormatProvider") is not { } formatProviderType)
			{
				return;
			}

			compilationStartAnalysisContext.RegisterSyntaxNodeAction(
				c => AnalyzeSyntax(c, cultureInfoType, numberStylesType, formatProviderType),
				SyntaxKind.InvocationExpression);
		});
	}

	public const string DiagnosticId = "FL0026";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

	internal const string KindPropertyKey = "kind";
	internal const string SuffixPropertyKey = "suffix";
	internal const string ParseKindValue = "Parse";
	internal const string TryParseKindValue = "TryParse";
	internal const string ToStringKindValue = "ToString";

	private static void AnalyzeSyntax(SyntaxNodeAnalysisContext context, INamedTypeSymbol cultureInfoType, INamedTypeSymbol numberStylesType, INamedTypeSymbol formatProviderType)
	{
		var invocation = (InvocationExpressionSyntax) context.Node;
		var semanticModel = context.SemanticModel;

		if (semanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method)
			return;

		if (GetParseKind(method.ContainingType) is not { } parseKind)
			return;
		var (kind, suffix) = parseKind;

		// Named arguments could appear in any order; only handle the common positional case.
		var arguments = invocation.ArgumentList.Arguments;
		if (arguments.Any(x => x.NameColon is not null))
			return;

		bool IsInvariantCulture(ExpressionSyntax expression) =>
			semanticModel.GetSymbolInfo(expression, context.CancellationToken).Symbol is IPropertySymbol { Name: "InvariantCulture" } property &&
			SymbolEqualityComparer.Default.Equals(property.ContainingType, cultureInfoType);

		bool IsAcceptableNumberStyles(ExpressionSyntax expression) =>
			semanticModel.GetSymbolInfo(expression, context.CancellationToken).Symbol is IFieldSymbol field &&
			SymbolEqualityComparer.Default.Equals(field.ContainingType, numberStylesType) &&
			GetAcceptableNumberStyles(kind).Contains(field.Name);

		// Validates that the (string, [NumberStyles], IFormatProvider, [out]) arguments are all compatible with InvariantConvert.
		bool ValidateProviderAndStyle()
		{
			if (arguments.Count != method.Parameters.Length)
				return false;

			var sawProvider = false;
			for (var index = 0; index < method.Parameters.Length; index++)
			{
				var parameter = method.Parameters[index];
				if (index == 0 || parameter.RefKind == RefKind.Out)
					continue;

				var argument = arguments[index].Expression;
				if (SymbolEqualityComparer.Default.Equals(parameter.Type, numberStylesType))
				{
					if (!IsAcceptableNumberStyles(argument))
						return false;
				}
				else if (SymbolEqualityComparer.Default.Equals(parameter.Type, formatProviderType))
				{
					if (!IsInvariantCulture(argument))
						return false;
					sawProvider = true;
				}
				else
				{
					return false;
				}
			}

			return sawProvider;
		}

		switch (method.Name)
		{
			case "ToString":
				if (method.IsStatic || method.Parameters.Length != 1 || arguments.Count != 1)
					return;
				if (!SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, formatProviderType))
					return;
				if (!IsInvariantCulture(arguments[0].Expression))
					return;
				Report(ToStringKindValue);
				return;

			case "Parse":
				if (!method.IsStatic || method.Parameters.Length == 0 || method.Parameters[0].Type.SpecialType != SpecialType.System_String)
					return;
				if (kind == ParseKind.Boolean)
				{
					// bool.Parse(string) is always invariant.
					if (method.Parameters.Length != 1 || arguments.Count != 1)
						return;
				}
				else if (!ValidateProviderAndStyle())
				{
					return;
				}
				Report(ParseKindValue);
				return;

			case "TryParse":
				if (!method.IsStatic || method.Parameters.Length == 0 || method.Parameters[0].Type.SpecialType != SpecialType.System_String)
					return;
				if (!method.Parameters.Any(x => x.RefKind == RefKind.Out))
					return;
				if (kind == ParseKind.Boolean)
				{
					// bool.TryParse(string, out bool) is always invariant.
					if (method.Parameters.Length != 2 || arguments.Count != 2)
						return;
				}
				else if (!ValidateProviderAndStyle())
				{
					return;
				}
				Report(TryParseKindValue);
				return;
		}

		void Report(string kindValue)
		{
			var properties = ImmutableDictionary<string, string?>.Empty
				.Add(KindPropertyKey, kindValue)
				.Add(SuffixPropertyKey, suffix);
			context.ReportDiagnostic(Diagnostic.Create(s_rule, invocation.GetLocation(), properties));
		}
	}

	private static (ParseKind Kind, string Suffix)? GetParseKind(ITypeSymbol type) => type.SpecialType switch
	{
		SpecialType.System_Boolean => (ParseKind.Boolean, "Boolean"),
		SpecialType.System_Int32 => (ParseKind.Int32, "Int32"),
		SpecialType.System_Int64 => (ParseKind.Int64, "Int64"),
		SpecialType.System_Double => (ParseKind.Double, "Double"),
		_ => null,
	};

	private static ImmutableArray<string> GetAcceptableNumberStyles(ParseKind kind) => kind switch
	{
		ParseKind.Double => ["None", "Float"],
		ParseKind.Int32 or ParseKind.Int64 => ["None", "Integer"],
		_ => [],
	};

	private enum ParseKind
	{
		Boolean,
		Int32,
		Int64,
		Double,
	}

	private static readonly DiagnosticDescriptor s_rule = new(
		id: DiagnosticId,
		title: "Use InvariantConvert",
		messageFormat: "Use InvariantConvert",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: true,
		helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/blob/-/docs/{DiagnosticId}.md");
}
