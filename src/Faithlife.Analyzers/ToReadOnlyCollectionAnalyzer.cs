using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Faithlife.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ToReadOnlyCollectionAnalyzer : DiagnosticAnalyzer
{
	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
		{
			var enumerableUtility = compilationStartAnalysisContext.Compilation.GetTypeByMetadataName("Libronix.Utility.EnumerableUtility");
			if (enumerableUtility != null)
			{
				compilationStartAnalysisContext.RegisterOperationBlockStartAction(context =>
				{
					if (context.OwningSymbol is IMethodSymbol methodSymbol && methodSymbol.MethodKind == MethodKind.Constructor)
						context.RegisterOperationAction(oc => AnalyzeOperation(oc, enumerableUtility), OperationKind.Invocation);
				});
			}
		});
	}

	public const string DiagnosticId = "FL0005";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_rule);

	private static void AnalyzeOperation(OperationAnalysisContext context, INamedTypeSymbol enumerableUtility)
	{
		var invocationOperation = (IInvocationOperation) context.Operation;
		var targetMethod = invocationOperation.TargetMethod;
		if (targetMethod.ContainingType == enumerableUtility && targetMethod.Name == "ToReadOnlyCollection")
		{
			var expressionStatement = invocationOperation.Syntax.FirstAncestorOrSelf<ExpressionStatementSyntax>();
			if (expressionStatement?.Expression is AssignmentExpressionSyntax assignmentExpression)
			{
				if (assignmentExpression.Left is IdentifierNameSyntax identifierName)
				{
					var semanticModel = context.Compilation.GetSemanticModel(identifierName.SyntaxTree);
					var symbolInfo = semanticModel.GetSymbolInfo(identifierName);
					var kind = symbolInfo.Symbol.Kind;
					if (kind == SymbolKind.Field || kind == SymbolKind.Property)
						context.ReportDiagnostic(Diagnostic.Create(s_rule, invocationOperation.GetMethodNameLocation()));
				}
			}
		}
	}

	private static readonly DiagnosticDescriptor s_rule = new DiagnosticDescriptor(
		id: DiagnosticId,
		title: "ToReadOnlyCollection in constructor",
		messageFormat: "Avoid ToReadOnlyCollection in constructors.",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{DiagnosticId}");
}
