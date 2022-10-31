using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Faithlife.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AvailableWorkStateAnalyzer : DiagnosticAnalyzer
{
	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
		{
			var iworkState = compilationStartAnalysisContext.Compilation.GetTypeByMetadataName("Libronix.Utility.Threading.IWorkState");
			if (iworkState is null)
				return;

			var workStateClass = compilationStartAnalysisContext.Compilation.GetTypeByMetadataName("Libronix.Utility.Threading.WorkState");
			if (workStateClass is null)
				return;

			var workStateNone = workStateClass.GetMembers("None");
			if (workStateNone.Length != 1)
				return;

			var workStateToDo = workStateClass.GetMembers("ToDo");
			if (workStateToDo.Length != 1)
				return;

			compilationStartAnalysisContext.RegisterOperationAction(x => AnalyzeOperation(x, iworkState, workStateNone[0], workStateToDo[0]), OperationKind.PropertyReference);
		});
	}

	public const string DiagnosticId = "FL0008";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_rule);

	private static void AnalyzeOperation(OperationAnalysisContext context, INamedTypeSymbol iworkState, ISymbol workStateNone, ISymbol workStateToDo)
	{
		var propertyReferenceOperation = (IPropertyReferenceOperation) context.Operation;
		if (!SymbolEqualityComparer.Default.Equals(propertyReferenceOperation.Property, workStateNone) && !SymbolEqualityComparer.Default.Equals(propertyReferenceOperation.Property, workStateToDo))
			return;

		var syntax = (MemberAccessExpressionSyntax) propertyReferenceOperation.Syntax;

		var containingMethod = syntax.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
		if (containingMethod == null)
			return;

		var semanticModel = context.Operation.SemanticModel;

		var asyncAction = context.Compilation.GetTypeByMetadataName("Libronix.Utility.Threading.AsyncAction");
		var ienumerable = context.Compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1");
		var returnTypeSymbol = semanticModel.GetSymbolInfo(containingMethod.ReturnType).Symbol as INamedTypeSymbol;
		var returnsIEnumerableAsyncAction = SymbolEqualityComparer.Default.Equals(ienumerable, returnTypeSymbol?.ConstructedFrom) && SymbolEqualityComparer.Default.Equals(asyncAction, returnTypeSymbol.TypeArguments[0]);
		if (returnsIEnumerableAsyncAction)
		{
			context.ReportDiagnostic(Diagnostic.Create(s_rule, syntax.GetLocation()));
			return;
		}

		var cancellationToken = context.Compilation.GetTypeByMetadataName("System.Threading.CancellationToken");
		var asyncMethodContext = context.Compilation.GetTypeByMetadataName("Libronix.Utility.Threading.AsyncMethodContext");
		var hasWorkStateParameters = containingMethod.ParameterList.Parameters.Any(parameter =>
		{
			var parameterSymbolInfo = semanticModel.GetSymbolInfo(parameter.Type);
			if (parameterSymbolInfo.Symbol is null)
				return false;

			if (SymbolEqualityComparer.Default.Equals(parameterSymbolInfo.Symbol, iworkState))
				return true;

			var namedTypeSymbol = parameterSymbolInfo.Symbol as INamedTypeSymbol;
			if (namedTypeSymbol is null)
				return false;

			if (SymbolEqualityComparer.Default.Equals(namedTypeSymbol, iworkState) || SymbolEqualityComparer.Default.Equals(namedTypeSymbol, cancellationToken) || SymbolEqualityComparer.Default.Equals(namedTypeSymbol, asyncMethodContext))
				return true;

			return namedTypeSymbol.AllInterfaces.Any(x => SymbolEqualityComparer.Default.Equals(x, iworkState));
		});

		if (!hasWorkStateParameters)
			return;

		context.ReportDiagnostic(Diagnostic.Create(s_rule, propertyReferenceOperation.Syntax.GetLocation()));
	}

	private static readonly DiagnosticDescriptor s_rule = new DiagnosticDescriptor(
		id: DiagnosticId,
		title: "WorkState.None and WorkState.ToDo Usage",
		messageFormat: "WorkState.None and WorkState.ToDo must not be used when an IWorkState is available",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		helpLinkUri: "https://github.com/Faithlife/FaithlifeAnalyzers/wiki/FL0008");
}
