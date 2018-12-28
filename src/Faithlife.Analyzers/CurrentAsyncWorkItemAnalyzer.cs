using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Faithlife.Analyzers
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class CurrentAsyncWorkItemAnalyzer : DiagnosticAnalyzer
	{
		public override void Initialize(AnalysisContext context)
		{
			context.RegisterSyntaxNodeAction(AnalyzeSyntax, SyntaxKind.SimpleMemberAccessExpression);
		}

		public const string DiagnosticId = "FL0001";

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(s_rule); } }

		private static void AnalyzeSyntax(SyntaxNodeAnalysisContext context)
		{
			var syntax = (MemberAccessExpressionSyntax)context.Node;

			var asyncWorkItem = context.SemanticModel.Compilation.GetTypeByMetadataName("Libronix.Utility.Threading.AsyncWorkItem");
			if (asyncWorkItem == null)
				return;

			var asyncAction = context.SemanticModel.Compilation.GetTypeByMetadataName("Libronix.Utility.Threading.AsyncAction");
			if (asyncAction == null)
				return;

			var currentMembers = asyncWorkItem.GetMembers("Current");
			if (currentMembers == null || currentMembers.Length != 1)
				return;

			var symbolInfo = context.SemanticModel.GetSymbolInfo(syntax.Expression);
			if (symbolInfo.Symbol == null || !symbolInfo.Symbol.Equals(asyncWorkItem))
				return;

			var memberSymbolInfo = context.SemanticModel.GetSymbolInfo(syntax.Name);
			if (memberSymbolInfo.Symbol == null || !memberSymbolInfo.Symbol.Equals(currentMembers[0]))
				return;

			var containingMethod = syntax.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
			if (containingMethod == null)
				return;

			var ienumerable = context.SemanticModel.Compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1");
			var returnTypeSymbol = context.SemanticModel.GetSymbolInfo(containingMethod.ReturnType).Symbol as INamedTypeSymbol;
			if (returnTypeSymbol != null && returnTypeSymbol.ConstructedFrom != null && returnTypeSymbol.ConstructedFrom.Equals(ienumerable) &&
				returnTypeSymbol.TypeArguments[0].Equals(asyncAction))
			{
				return;
			}

			context.ReportDiagnostic(Diagnostic.Create(s_rule, syntax.GetLocation()));
		}

		static readonly DiagnosticDescriptor s_rule = new DiagnosticDescriptor(
			id: DiagnosticId,
			title: "AsyncWorkItem.Current Usage",
			messageFormat: "AsyncWorkItem.Current must only be used in methods that return IEnumerable<AsyncAction>.",
			category: "Usage",
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true);
	}
}
