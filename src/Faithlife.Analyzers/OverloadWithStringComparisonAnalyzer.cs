using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Faithlife.Analyzers
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class OverloadWithStringComparisonAnalyzer : DiagnosticAnalyzer
	{
		public override void Initialize(AnalysisContext context)
		{
			context.RegisterSyntaxNodeAction(AnalyzeSyntax, SyntaxKind.InvocationExpression);
		}

		public const string DiagnosticId = "FL0002";

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => s_rules;

		private static void AnalyzeSyntax(SyntaxNodeAnalysisContext context)
		{
			var stringComparisonType = context.SemanticModel.Compilation.GetTypeByMetadataName("System.StringComparison");
			if (stringComparisonType == null)
				return;

			// check if a StringComparison is already passed to the method
			var invocation = (InvocationExpressionSyntax) context.Node;
			var argumentTypes = invocation.ArgumentList.Arguments.Select(x => context.SemanticModel.GetTypeInfo(x.Expression).Type);
			if (argumentTypes.Any(x => stringComparisonType.Equals(x)))
				return;

			// get the method being invoked
			var methodSymbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
			if (methodSymbol == null)
				return;

			// get all members with the same name
			var overloads = methodSymbol.ContainingType.GetMembers(methodSymbol.Name);

			// check if any overload takes a 'StringComparison' parameter
			if (overloads.OfType<IMethodSymbol>().SelectMany(x => x.Parameters).Any(x => stringComparisonType.Equals(x.Type)))
				context.ReportDiagnostic(Diagnostic.Create(s_rule, invocation.GetLocation()));
		}

		static readonly DiagnosticDescriptor s_rule = new DiagnosticDescriptor(
			id: DiagnosticId,
			title: "Use StringComparison overload",
			messageFormat: "Use an overload that takes a StringComparison.",
			category: "Usage",
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			description: "The desired StringComparison must be explicitly specified.",
			helpLinkUri: "https://github.com/Faithlife/FaithlifeAnalyzers/wiki/FL0002");

		static readonly ImmutableArray<DiagnosticDescriptor> s_rules = ImmutableArray.Create(s_rule);
	}
}
