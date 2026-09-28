using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Faithlife.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FormattableStringInvariantAnalyzer : DiagnosticAnalyzer
{
	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
		{
			if (compilationStartAnalysisContext.Compilation.GetTypeByMetadataName("System.FormattableString") is not { } formattableStringType)
				return;

			var invariantMethod = formattableStringType.GetMembers("Invariant")
				.OfType<IMethodSymbol>()
				.FirstOrDefault(x => x.IsStatic && x.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(x.Parameters[0].Type, formattableStringType));
			if (invariantMethod is null)
				return;

			compilationStartAnalysisContext.RegisterSyntaxNodeAction(c => AnalyzeSyntax(c, invariantMethod), SyntaxKind.InvocationExpression);
		});
	}

	public const string DiagnosticId = "FL0027";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

	private static void AnalyzeSyntax(SyntaxNodeAnalysisContext context, IMethodSymbol invariantMethod)
	{
		var invocation = (InvocationExpressionSyntax) context.Node;
		if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Invariant" } ||
			context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol methodSymbol ||
			!SymbolEqualityComparer.Default.Equals(methodSymbol.OriginalDefinition, invariantMethod))
			return;

		context.ReportDiagnostic(Diagnostic.Create(s_rule, invocation.GetLocation()));
	}

	private static readonly DiagnosticDescriptor s_rule = new(
		id: DiagnosticId,
		title: "Use static import for FormattableString.Invariant",
		messageFormat: "Use Invariant with a static import of System.FormattableString",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: true,
		helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/blob/-/docs/{DiagnosticId}.md");
}
