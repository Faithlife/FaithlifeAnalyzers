using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Faithlife.Analyzers
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class GetOrAddValueAnalyzer : DiagnosticAnalyzer
	{
		public override void Initialize(AnalysisContext context)
		{
			context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
			{
				var dictionaryUtility = compilationStartAnalysisContext.Compilation.GetTypeByMetadataName("Libronix.Utility.DictionaryUtility");
				if (dictionaryUtility is null)
					return;

				var concurrentDictionary = compilationStartAnalysisContext.Compilation.GetTypeByMetadataName("System.Collections.Concurrent.ConcurrentDictionary`2");
				if (concurrentDictionary is null)
					return;

				compilationStartAnalysisContext.RegisterSyntaxNodeAction(c => AnalyzeSyntax(c, dictionaryUtility, concurrentDictionary), SyntaxKind.InvocationExpression);
			});
		}

		private static void AnalyzeSyntax(SyntaxNodeAnalysisContext context, INamedTypeSymbol dictionaryUtility, INamedTypeSymbol concurrentDictionary)
		{
			var invocation = (InvocationExpressionSyntax) context.Node;

			var method = context.SemanticModel.GetSymbolInfo(invocation.Expression).Symbol as IMethodSymbol;
			if (method?.Name != "GetOrAddValue" || method.ContainingType != dictionaryUtility)
				return;

			var memberAccess = (MemberAccessExpressionSyntax) invocation.Expression;
			var targetType = context.SemanticModel.GetTypeInfo(memberAccess.Expression).Type.OriginalDefinition;
			if (targetType?.Equals(concurrentDictionary) != true)
				return;

			context.ReportDiagnostic(Diagnostic.Create(s_rule, memberAccess.Name.GetLocation()));
		}

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_rule);

		public const string DiagnosticId = "FL0011";

		private static readonly DiagnosticDescriptor s_rule = new(
			id: DiagnosticId,
			title: "GetOrAddValue() Usage",
			messageFormat: "GetOrAddValue() is not threadsafe and should not be used with ConcurrentDictionary; use GetOrAdd() instead.",
			category: "Usage",
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{DiagnosticId}"
		);
	}
}
