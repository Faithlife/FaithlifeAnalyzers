using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Faithlife.Analyzers
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class UntilCanceledAnalyzer : DiagnosticAnalyzer
	{
		public override void Initialize(AnalysisContext context)
		{
			context.RegisterSyntaxNodeAction(AnalyzeSyntax, SyntaxKind.InvocationExpression);
		}

		public const string DiagnosticId = "FL0003";

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_rule);

		private static void AnalyzeSyntax(SyntaxNodeAnalysisContext context)
		{
			var invocation = (InvocationExpressionSyntax) context.Node;

			var asyncEnumerableUtility = context.SemanticModel.Compilation.GetTypeByMetadataName("Libronix.Utility.Threading.AsyncEnumerableUtility");
			var asyncAction = context.SemanticModel.Compilation.GetTypeByMetadataName("Libronix.Utility.Threading.AsyncAction");
			if (asyncEnumerableUtility is null || asyncAction is null)
				return;

			var method = context.SemanticModel.GetSymbolInfo(invocation.Expression).Symbol as IMethodSymbol;
			if (method?.Name != "UntilCanceled" || method.ContainingType != asyncEnumerableUtility)
				return;

			if (invocation.ArgumentList.Arguments.Count != 0)
				return;

			var containingMethod = invocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
			if (containingMethod is null)
				return;

			var ienumerable = context.SemanticModel.Compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1");
			var returnTypeSymbol = ModelExtensions.GetSymbolInfo(context.SemanticModel, containingMethod.ReturnType).Symbol as INamedTypeSymbol;
			if (returnTypeSymbol?.ConstructedFrom == ienumerable && returnTypeSymbol?.TypeArguments[0] == asyncAction)
				return;

			context.ReportDiagnostic(Diagnostic.Create(s_rule, invocation.ArgumentList.GetLocation()));
		}

		static readonly DiagnosticDescriptor s_rule = new DiagnosticDescriptor(
			id: DiagnosticId,
			title: "UntilCanceled() Usage",
			messageFormat: "UntilCanceled() may only be used in methods that return IEnumerable<AsyncAction>.",
			category: "Usage",
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{DiagnosticId}");
	}
}
