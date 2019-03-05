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
			context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
			{
				var asyncWorkItem = compilationStartAnalysisContext.Compilation.GetTypeByMetadataName("Libronix.Utility.Threading.AsyncWorkItem");
				if (asyncWorkItem is null)
					return;

				var currentMembers = asyncWorkItem.GetMembers("Current");
				if (currentMembers.Length != 1)
					return;

				var asyncAction = compilationStartAnalysisContext.Compilation.GetTypeByMetadataName("Libronix.Utility.Threading.AsyncAction");
				if (asyncAction is null)
					return;

				compilationStartAnalysisContext.RegisterSyntaxNodeAction(c => AnalyzeSyntax(c, asyncWorkItem, asyncAction, currentMembers[0]), SyntaxKind.SimpleMemberAccessExpression);
			});
		}

		public const string DiagnosticId = "FL0001";

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(s_rule); } }

		private static void AnalyzeSyntax(SyntaxNodeAnalysisContext context, INamedTypeSymbol asyncWorkItem, INamedTypeSymbol asyncAction, ISymbol asyncWorkItemCurrent)
		{
			var syntax = (MemberAccessExpressionSyntax) context.Node;

			var symbolInfo = context.SemanticModel.GetSymbolInfo(syntax.Expression);
			if (symbolInfo.Symbol == null || !symbolInfo.Symbol.Equals(asyncWorkItem))
				return;

			var memberSymbolInfo = context.SemanticModel.GetSymbolInfo(syntax.Name);
			if (memberSymbolInfo.Symbol == null || !memberSymbolInfo.Symbol.Equals(asyncWorkItemCurrent))
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

			// check for AsyncWorkItem.Current being used in a lambda passed as an argument to AsyncWorkItem.Start
			var invocation = syntax.FirstAncestorOrSelf<LambdaExpressionSyntax>()?.FirstAncestorOrSelf<ArgumentSyntax>()?.FirstAncestorOrSelf<InvocationExpressionSyntax>();
			if (invocation?.Expression is MemberAccessExpressionSyntax memberAccess)
			{
				if (memberAccess.Name.Identifier.Text == "Start")
				{
					symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess.Expression);
					if (symbolInfo.Symbol.Equals(asyncWorkItem))
						return;
				}
			}

			context.ReportDiagnostic(Diagnostic.Create(s_rule, syntax.GetLocation()));
		}

		static readonly DiagnosticDescriptor s_rule = new DiagnosticDescriptor(
			id: DiagnosticId,
			title: "AsyncWorkItem.Current Usage",
			messageFormat: "AsyncWorkItem.Current must only be used in methods that return IEnumerable<AsyncAction>.",
			category: "Usage",
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			helpLinkUri: "https://github.com/Faithlife/FaithlifeAnalyzers/wiki/FL0001");
	}
}
