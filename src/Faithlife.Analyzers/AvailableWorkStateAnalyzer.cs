using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Faithlife.Analyzers
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class AvailableWorkStateAnalyzer : DiagnosticAnalyzer
	{
		public override void Initialize(AnalysisContext context)
		{
			context.RegisterSyntaxNodeAction(AnalyzeSyntax, SyntaxKind.SimpleMemberAccessExpression);
		}

		public const string DiagnosticId = "FL0008";

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(s_rule); } }

		private static void AnalyzeSyntax(SyntaxNodeAnalysisContext context)
		{
			var syntax = (MemberAccessExpressionSyntax)context.Node;

			var iworkState = context.SemanticModel.Compilation.GetTypeByMetadataName("Libronix.Utility.Threading.IWorkState");
			if (iworkState == null)
				return;

			var workStateClass = context.SemanticModel.Compilation.GetTypeByMetadataName("Libronix.Utility.Threading.WorkState");
			if (workStateClass == null)
				return;

			var workStateNone = workStateClass.GetMembers("None");
			if (workStateNone == null || workStateNone.Length != 1)
				return;

			var workStateToDo = workStateClass.GetMembers("ToDo");
			if (workStateToDo == null || workStateToDo.Length != 1)
				return;

			var symbolInfo = context.SemanticModel.GetSymbolInfo(syntax.Expression);
			if (symbolInfo.Symbol == null || !symbolInfo.Symbol.Equals(workStateClass))
				return;

			var memberSymbolInfo = context.SemanticModel.GetSymbolInfo(syntax.Name);
			if (memberSymbolInfo.Symbol == null || (!memberSymbolInfo.Symbol.Equals(workStateNone[0]) && !memberSymbolInfo.Symbol.Equals(workStateToDo[0])))
				return;

			var containingMethod = syntax.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
			if (containingMethod == null)
				return;

			var asyncAction = context.SemanticModel.Compilation.GetTypeByMetadataName("Libronix.Utility.Threading.AsyncAction");
			if (asyncAction != null)
			{
				var ienumerable = context.SemanticModel.Compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1");
				var returnTypeSymbol = context.SemanticModel.GetSymbolInfo(containingMethod.ReturnType).Symbol as INamedTypeSymbol;
				if (returnTypeSymbol != null && returnTypeSymbol.ConstructedFrom != null && returnTypeSymbol.ConstructedFrom.Equals(ienumerable) &&
					returnTypeSymbol.TypeArguments[0].Equals(asyncAction))
				{
					context.ReportDiagnostic(Diagnostic.Create(s_rule, syntax.GetLocation()));
					return;
				}
			}

			var cancellationToken = context.SemanticModel.Compilation.GetTypeByMetadataName("System.Threading.CancellationToken");
			var asyncMethodContext = context.SemanticModel.Compilation.GetTypeByMetadataName("Libronix.Utility.Threading.AsyncMethodContext");
			var hasWorkStateParameters = containingMethod.ParameterList.Parameters.Any(parameter =>
			{
				var parameterSymbolInfo = context.SemanticModel.GetSymbolInfo(parameter.Type);
				if (parameterSymbolInfo.Symbol is null)
					return false;

				if (symbolInfo.Symbol.Equals(iworkState))
					return true;

				var namedTypeSymbol = parameterSymbolInfo.Symbol as INamedTypeSymbol;
				if (namedTypeSymbol is null)
					return false;

				if (namedTypeSymbol.Equals(iworkState) || namedTypeSymbol.Equals(cancellationToken) || namedTypeSymbol.Equals(asyncMethodContext))
					return true;

				return namedTypeSymbol.AllInterfaces.Any(x => x.Equals(iworkState));
			});

			if (!hasWorkStateParameters)
				return;

			context.ReportDiagnostic(Diagnostic.Create(s_rule, syntax.GetLocation()));
		}

		static readonly DiagnosticDescriptor s_rule = new DiagnosticDescriptor(
			id: DiagnosticId,
			title: "WorkState.None and WorkState.ToDo Usage",
			messageFormat: "WorkState.None and WorkState.ToDo must not be used when an IWorkState is available.",
			category: "Usage",
			defaultSeverity: DiagnosticSeverity.Error,
			isEnabledByDefault: true,
			helpLinkUri: "https://github.com/Faithlife/FaithlifeAnalyzers/wiki/FL0008");
	}
}
