using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Faithlife.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class IfNotNullAnalyzer : DiagnosticAnalyzer
{
	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
		{
			if (compilationStartAnalysisContext.Compilation.GetTypeByMetadataName("Libronix.Utility.IfNotNull.IfNotNullExtensionMethod") is not { } ifNotNullExtensionMethodType)
				return;

			var ifNotNullMethods = ifNotNullExtensionMethodType.GetMembers("IfNotNull");
			if (ifNotNullMethods.Length == 0)
				return;

			var expressionType = compilationStartAnalysisContext.Compilation.GetTypeByMetadataName("System.Linq.Expressions.Expression`1");

			compilationStartAnalysisContext.RegisterSyntaxNodeAction(c => AnalyzeSyntax(c, ifNotNullMethods, expressionType), SyntaxKind.InvocationExpression);
		});
	}

	public const string DiagnosticId = "FL0010";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

	private static void AnalyzeSyntax(SyntaxNodeAnalysisContext context, ImmutableArray<ISymbol> ifNotNullMethods, INamedTypeSymbol? expressionType)
	{
		var syntax = (InvocationExpressionSyntax) context.Node;

		if (context.SemanticModel.GetSymbolInfo(syntax.Expression).Symbol is not IMethodSymbol methodSymbol ||
			(methodSymbol.ReducedFrom == null && methodSymbol.ConstructedFrom == null) ||
			!ifNotNullMethods.Any(x => SymbolEqualityComparer.Default.Equals(x, methodSymbol.ReducedFrom) || SymbolEqualityComparer.Default.Equals(x, methodSymbol.ConstructedFrom)))
			return;

		if (expressionType is not null && IsInExpressionTree(context, syntax, expressionType))
			return;

		context.ReportDiagnostic(Diagnostic.Create(s_rule, syntax.GetLocation()));
	}

	private static bool IsInExpressionTree(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax syntax, INamedTypeSymbol expressionType)
	{
		foreach (var anonymousFunction in syntax.Ancestors().OfType<AnonymousFunctionExpressionSyntax>())
		{
			if (context.SemanticModel.GetTypeInfo(anonymousFunction, context.CancellationToken).ConvertedType is INamedTypeSymbol convertedType &&
				SymbolEqualityComparer.Default.Equals(convertedType.OriginalDefinition, expressionType))
			{
				return true;
			}
		}

		return false;
	}

	private static readonly DiagnosticDescriptor s_rule = new(
		id: DiagnosticId,
		title: "IfNotNull deprecation",
		messageFormat: "Prefer modern language features over IfNotNull usage",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: true,
		helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/blob/-/docs/{DiagnosticId}.md");
}
