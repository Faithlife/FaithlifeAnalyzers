using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Faithlife.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FormatInvariantAnalyzer : DiagnosticAnalyzer
{
	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
		{
			var stringUtilityType = compilationStartAnalysisContext.Compilation.GetTypeByMetadataName("Libronix.Utility.StringUtility");
			if (stringUtilityType == null)
				return;

			var formatInvariantMethods = stringUtilityType.GetMembers("FormatInvariant");
			if (formatInvariantMethods.Length == 0)
				return;

			compilationStartAnalysisContext.RegisterSyntaxNodeAction(c => AnalyzeSyntax(c, formatInvariantMethods), SyntaxKind.InvocationExpression);
		});
	}

	public const string DiagnosticId = "FL0018";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_rule);

	private static void AnalyzeSyntax(SyntaxNodeAnalysisContext context, ImmutableArray<ISymbol> formatInvariantMethods)
	{
		var syntax = (InvocationExpressionSyntax) context.Node;

		var methodSymbol = context.SemanticModel.GetSymbolInfo(syntax.Expression).Symbol as IMethodSymbol;
		if (methodSymbol == null ||
			(methodSymbol.ReducedFrom == null && methodSymbol.ConstructedFrom == null) ||
			!formatInvariantMethods.Any(x => SymbolEqualityComparer.Default.Equals(x, methodSymbol.ReducedFrom) || SymbolEqualityComparer.Default.Equals(x, methodSymbol.ConstructedFrom)))
			return;

		context.ReportDiagnostic(Diagnostic.Create(s_rule, syntax.GetLocation()));
	}

	private static readonly DiagnosticDescriptor s_rule = new DiagnosticDescriptor(
		id: DiagnosticId,
		title: "FormatInvariant deprecation",
		messageFormat: "Prefer string interpolation over FormatInvariant",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: true,
		helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{DiagnosticId}");
}
