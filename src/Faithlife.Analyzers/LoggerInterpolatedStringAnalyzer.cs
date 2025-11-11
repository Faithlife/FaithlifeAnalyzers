using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Faithlife.Analyzers;

/// <summary>
/// Detects interpolated strings passed directly to Libronix.Utility.Logging.Logger methods and suggests
/// converting them to composite format strings with arguments (e.g., $"X {y}" -> "X {0}", y).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LoggerInterpolatedStringAnalyzer : DiagnosticAnalyzer
{
	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
	}

	public const string DiagnosticId = "FL0024";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

	private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
	{
		var invocation = (InvocationExpressionSyntax) context.Node;
		var argumentList = invocation.ArgumentList;
		if (argumentList.Arguments.Count == 0)
			return;

		var args = argumentList.Arguments;

		// Resolve symbol first to determine which argument to check.
		if (context.SemanticModel.GetSymbolInfo(invocation.Expression).Symbol is not IMethodSymbol methodSymbol)
			return;

		if (!IsLoggerMethod(methodSymbol))
			return;

		// For Write method, check second argument (message), otherwise check first argument.
		var messageArgIndex = methodSymbol.Name == "Write" ? 1 : 0;
		if (args.Count <= messageArgIndex)
			return;

		// Only inspect the message argument.
		if (args[messageArgIndex].Expression is not InterpolatedStringExpressionSyntax interpolated)
			return;

		// Ignore if there are no interpolation holes (FL0014 already handles empty interpolation cases).
		if (interpolated.Contents.All(c => c is InterpolatedStringTextSyntax))
			return;

		// If caller already supplies additional arguments that could be a params array, allow fix anyway,
		// but skip if next argument is explicitly an array creation to avoid ambiguity.
		var nextArgIndex = messageArgIndex + 1;
		if (args.Count > nextArgIndex && args[nextArgIndex].Expression is (ArrayCreationExpressionSyntax or ImplicitArrayCreationExpressionSyntax))
			return;

		context.ReportDiagnostic(Diagnostic.Create(s_rule, args[messageArgIndex].GetLocation()));
	}

	private static bool IsLoggerMethod(IMethodSymbol method)
	{
		// Require containing type Libronix.Utility.Logging.Logger
		var containing = method.ContainingType;
		if (containing is null)
			return false;
		if (containing.Name != "Logger")
			return false;
		if (containing.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) != "global::Libronix.Utility.Logging.Logger")
			return false;

		return method.Name is "Debug" or "Info" or "Warn" or "Error" or "Fatal" or "Write";
	}

	private static readonly DiagnosticDescriptor s_rule = new(
		id: DiagnosticId,
		title: "Use composite format string instead of interpolated string",
		messageFormat: "Replace interpolated string with composite format string arguments",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: true,
		helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{DiagnosticId}");
}
