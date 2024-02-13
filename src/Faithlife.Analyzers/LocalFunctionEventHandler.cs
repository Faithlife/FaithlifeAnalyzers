using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Faithlife.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class LocalFunctionEventHandler : DiagnosticAnalyzer
{
	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SubtractAssignmentExpression);
	}

	private void Analyze(SyntaxNodeAnalysisContext context)
	{
		var containingMethod = TryFindEnclosingMethod(context.Node);
		if (containingMethod == null)
			return;

		var assignment = (AssignmentExpressionSyntax) context.Node;
		if (context.SemanticModel.GetSymbolInfo(assignment.Left).Symbol is not IEventSymbol eventSymbol)
			return;
		if (context.SemanticModel.GetSymbolInfo(assignment.Right).Symbol is not IMethodSymbol methodGroup)
			return;

		if (methodGroup.MethodKind == MethodKind.AnonymousFunction)
		{
			context.ReportDiagnostic(Diagnostic.Create(s_lambdaRule, assignment.GetLocation()));
			return;
		}

		if (methodGroup is not { MethodKind: MethodKind.LocalFunction, IsStatic: false })
			return;

		var matchingSubscription = FindMatchingSubscription(context.SemanticModel, containingMethod, assignment, eventSymbol, methodGroup);
		if (matchingSubscription == null)
			context.ReportDiagnostic(Diagnostic.Create(s_localFunctionRule, assignment.GetLocation()));

		static MethodDeclarationSyntax? TryFindEnclosingMethod(SyntaxNode? node)
		{
			while (node != null)
			{
				if (node is MethodDeclarationSyntax method)
					return method;
				node = node.Parent;
			}
			return null;
		}

		static AssignmentExpressionSyntax? FindMatchingSubscription(SemanticModel model, MethodDeclarationSyntax method, AssignmentExpressionSyntax stop, IEventSymbol eventSymbol, IMethodSymbol methodSymbol)
		{
			foreach (var assignmentExpression in AllChildren(method).OfType<AssignmentExpressionSyntax>())
			{
				if (assignmentExpression == stop)
					break;
				if (assignmentExpression.Kind() != SyntaxKind.AddAssignmentExpression)
					continue;
				if (!SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(assignmentExpression.Left).Symbol, eventSymbol))
					continue;
				if (!SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(assignmentExpression.Right).Symbol, methodSymbol))
					continue;
				return assignmentExpression;
			}

			return null;

			static IEnumerable<SyntaxNode> AllChildren(SyntaxNode node)
			{
				foreach (var child in node.ChildNodes())
				{
					yield return child;
					foreach (var grandchild in AllChildren(child))
						yield return grandchild;
				}
			}
		}
	}

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_localFunctionRule, s_lambdaRule);

	public const string LocalFunctionDiagnosticId = "FL0019";
	public const string LambdaDiagnosticId = "FL0020";

	private static readonly DiagnosticDescriptor s_localFunctionRule = new(
		id: LocalFunctionDiagnosticId,
		title: "Local Functions as Event Handlers",
		messageFormat: "Local function event handler.",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{LocalFunctionDiagnosticId}"
	);

	private static readonly DiagnosticDescriptor s_lambdaRule = new(
		id: LambdaDiagnosticId,
		title: "Lambda Expressions as Event Handlers",
		messageFormat: "Lambda expression event handler.",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{LambdaDiagnosticId}"
	);
}
