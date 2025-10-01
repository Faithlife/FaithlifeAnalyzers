using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Faithlife.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsyncMethodContextWorkStateAnalyzer : DiagnosticAnalyzer
{
	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
		{
			if (compilationStartAnalysisContext.Compilation.GetTypeByMetadataName("Libronix.Utility.Threading.WorkState") is not { } workStateClass)
				return;

			if (compilationStartAnalysisContext.Compilation.GetTypeByMetadataName("Libronix.Utility.Threading.AsyncMethodContext") is not { } asyncMethodContext)
				return;

			var fromCancellationTokenMethods = workStateClass.GetMembers("FromCancellationToken").OfType<IMethodSymbol>().ToArray();
			if (fromCancellationTokenMethods.Length == 0)
				return;

			compilationStartAnalysisContext.RegisterSyntaxNodeAction(c => AnalyzeSyntax(c, fromCancellationTokenMethods, asyncMethodContext), SyntaxKind.InvocationExpression);
		});
	}

	public const string DiagnosticId = "FL0022";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

	private static void AnalyzeSyntax(SyntaxNodeAnalysisContext context, IMethodSymbol[] fromCancellationTokenMethods, INamedTypeSymbol asyncMethodContext)
	{
		var invocation = (InvocationExpressionSyntax) context.Node;

		// check if this is a call to WorkState.FromCancellationToken
		if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.Text: "FromCancellationToken" } memberAccess)
			return;
		if (context.SemanticModel.GetSymbolInfo(invocation.Expression).Symbol is not IMethodSymbol method ||
			!fromCancellationTokenMethods.Any(m => SymbolEqualityComparer.Default.Equals(m, method)))
		{
			return;
		}

		// check if the argument is context.CancellationToken where context is AsyncMethodContext
		if (invocation.ArgumentList.Arguments.Count != 1)
			return;

		var argument = invocation.ArgumentList.Arguments[0];
		if (argument.Expression is not MemberAccessExpressionSyntax { Name.Identifier.Text: "CancellationToken" } cancellationTokenAccess)
			return;

		// get the type of the expression before .CancellationToken
		var expressionType = context.SemanticModel.GetTypeInfo(cancellationTokenAccess.Expression).Type;
		if (!SymbolEqualityComparer.Default.Equals(expressionType, asyncMethodContext))
			return;

		context.ReportDiagnostic(Diagnostic.Create(s_rule, invocation.GetLocation()));
	}

	private static readonly DiagnosticDescriptor s_rule = new(
		id: DiagnosticId,
		title: "Use AsyncMethodContext.WorkState instead of WorkState.FromCancellationToken",
		messageFormat: "Use AsyncMethodContext.WorkState",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{DiagnosticId}");
}
