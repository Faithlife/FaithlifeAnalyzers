using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Faithlife.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CurrentAsyncWorkItemAnalyzer : DiagnosticAnalyzer
{
	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
		{
			if (compilationStartAnalysisContext.Compilation.GetTypeByMetadataName("Libronix.Utility.Threading.AsyncWorkItem") is not { } asyncWorkItem)
				return;

			var currentMembers = asyncWorkItem.GetMembers("Current");
			if (currentMembers.Length != 1)
				return;

			if (compilationStartAnalysisContext.Compilation.GetTypeByMetadataName("Libronix.Utility.Threading.AsyncAction") is not { } asyncAction)
				return;

			compilationStartAnalysisContext.RegisterSyntaxNodeAction(c => AnalyzeSyntax(c, asyncWorkItem, asyncAction, currentMembers[0]), SyntaxKind.SimpleMemberAccessExpression);
		});
	}

	public const string DiagnosticId = "FL0001";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

	private static void AnalyzeSyntax(SyntaxNodeAnalysisContext context, INamedTypeSymbol asyncWorkItem, INamedTypeSymbol asyncAction, ISymbol asyncWorkItemCurrent)
	{
		var syntax = (MemberAccessExpressionSyntax) context.Node;

		var containingMethod = syntax.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
		if (containingMethod == null)
			return;

		var ienumerable = context.SemanticModel.Compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1");
		if (context.SemanticModel.GetSymbolInfo(containingMethod.ReturnType).Symbol is INamedTypeSymbol { ConstructedFrom: not null } returnTypeSymbol && SymbolEqualityComparer.Default.Equals(returnTypeSymbol.ConstructedFrom, ienumerable) &&
			SymbolEqualityComparer.Default.Equals(returnTypeSymbol.TypeArguments[0], asyncAction))
		{
			return;
		}

		var symbolInfo = context.SemanticModel.GetSymbolInfo(syntax.Expression);
		if (symbolInfo.Symbol == null || !SymbolEqualityComparer.Default.Equals(symbolInfo.Symbol, asyncWorkItem))
			return;

		var memberSymbolInfo = context.SemanticModel.GetSymbolInfo(syntax.Name);
		if (memberSymbolInfo.Symbol == null || !SymbolEqualityComparer.Default.Equals(memberSymbolInfo.Symbol, asyncWorkItemCurrent))
			return;

		// check for AsyncWorkItem.Current being used in a lambda passed as an argument to AsyncWorkItem.Start
		var invocation = syntax.FirstAncestorOrSelf<LambdaExpressionSyntax>()?.FirstAncestorOrSelf<ArgumentSyntax>()?.FirstAncestorOrSelf<InvocationExpressionSyntax>();
		if (invocation?.Expression is MemberAccessExpressionSyntax memberAccess)
		{
			if (memberAccess.Name.Identifier.Text == "Start")
			{
				symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess.Expression);
				if (SymbolEqualityComparer.Default.Equals(symbolInfo.Symbol, asyncWorkItem))
					return;
			}
		}

		context.ReportDiagnostic(Diagnostic.Create(s_rule, syntax.GetLocation()));
	}

	private static readonly DiagnosticDescriptor s_rule = new(
		id: DiagnosticId,
		title: "AsyncWorkItem.Current Usage",
		messageFormat: "AsyncWorkItem.Current must only be used in methods that return IEnumerable<AsyncAction>",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		helpLinkUri: "https://github.com/Faithlife/FaithlifeAnalyzers/wiki/FL0001");
}
