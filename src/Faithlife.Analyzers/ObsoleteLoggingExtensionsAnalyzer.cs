using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Faithlife.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ObsoleteLoggingExtensionsAnalyzer : DiagnosticAnalyzer
{
	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
		{
			if (compilationStartAnalysisContext.Compilation.GetTypeByMetadataName("Logos.Common.Logging.Extensions.LoggerExtensions") is not { } extensionsType)
				return;

			var obsoleteMethods = extensionsType.GetMembers()
				.Where(m => m is IMethodSymbol && s_obsoleteMethodNames.Contains(m.Name))
				.ToImmutableArray();

			if (obsoleteMethods.Length == 0)
				return;

			compilationStartAnalysisContext.RegisterSyntaxNodeAction(c => AnalyzeSyntax(c, obsoleteMethods), SyntaxKind.InvocationExpression);
		});
	}

	public const string DiagnosticId = "FL0023";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

	private static void AnalyzeSyntax(SyntaxNodeAnalysisContext context, ImmutableArray<ISymbol> obsoleteMethods)
	{
		var syntax = (InvocationExpressionSyntax) context.Node;

		if (context.SemanticModel.GetSymbolInfo(syntax.Expression).Symbol is not IMethodSymbol methodSymbol ||
			(methodSymbol.ReducedFrom == null && methodSymbol.ConstructedFrom == null) ||
			!obsoleteMethods.Any(x => SymbolEqualityComparer.Default.Equals(x, methodSymbol.ReducedFrom) || SymbolEqualityComparer.Default.Equals(x, methodSymbol.ConstructedFrom)))
			return;

		var methodName = methodSymbol.Name;
		var replacementMethodName = GetReplacementMethodName(methodName);

		var diagnostic = Diagnostic.Create(
			s_rule,
			syntax.GetLocation(),
			methodName,
			replacementMethodName);

		context.ReportDiagnostic(diagnostic);
	}

	internal static string GetReplacementMethodName(string obsoleteMethodName) =>
		obsoleteMethodName switch
		{
			"Debug" => "LogDebug",
			"Info" => "LogInformation",
			"Warn" => "LogWarning",
			"Error" => "LogError",
			"Fatal" => "LogCritical",
			_ => obsoleteMethodName,
		};

	private static readonly ImmutableHashSet<string> s_obsoleteMethodNames = ImmutableHashSet.Create(
		"Debug", "Info", "Warn", "Error", "Fatal");

	private static readonly DiagnosticDescriptor s_rule = new(
		id: DiagnosticId,
		title: "Obsolete Logos.Common.Logging extension method",
		messageFormat: "Replace obsolete '{0}' method with '{1}'",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{DiagnosticId}");
}
