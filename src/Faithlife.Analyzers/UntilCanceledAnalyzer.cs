using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Faithlife.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UntilCanceledAnalyzer : DiagnosticAnalyzer
{
	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
		{
			var asyncEnumerableUtility = compilationStartAnalysisContext.Compilation.GetTypeByMetadataName("Libronix.Utility.Threading.AsyncEnumerableUtility");
			if (asyncEnumerableUtility is null)
				return;

			var asyncAction = compilationStartAnalysisContext.Compilation.GetTypeByMetadataName("Libronix.Utility.Threading.AsyncAction");
			if (asyncAction is null)
				return;

			compilationStartAnalysisContext.RegisterSyntaxNodeAction(c => AnalyzeSyntax(c, asyncEnumerableUtility, asyncAction), SyntaxKind.InvocationExpression);
		});
	}

	public const string DiagnosticId = "FL0003";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_rule);

	private static void AnalyzeSyntax(SyntaxNodeAnalysisContext context, INamedTypeSymbol asyncEnumerableUtility, INamedTypeSymbol asyncAction)
	{
		try
		{
			var invocation = (InvocationExpressionSyntax) context.Node;

			var name = invocation.Expression switch
			{
				MemberAccessExpressionSyntax memberAccess => memberAccess.Name,
				MemberBindingExpressionSyntax memberBinding => memberBinding.Name,
				_ => null,
			};

			if (name?.Identifier.Text != "UntilCanceled")
				return;

			var method = context.SemanticModel.GetSymbolInfo(invocation.Expression).Symbol as IMethodSymbol;
			if (!SymbolEqualityComparer.Default.Equals(method?.ContainingType, asyncEnumerableUtility))
				return;

			if (invocation.ArgumentList.Arguments.Count != 0)
				return;

			var containingMethod = invocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
			if (containingMethod is null)
				return;

			var ienumerable = context.SemanticModel.Compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1");
			var returnTypeSymbol = ModelExtensions.GetSymbolInfo(context.SemanticModel, containingMethod.ReturnType).Symbol as INamedTypeSymbol;
			if (SymbolEqualityComparer.Default.Equals(returnTypeSymbol?.ConstructedFrom, ienumerable) && SymbolEqualityComparer.Default.Equals(returnTypeSymbol?.TypeArguments[0], asyncAction))
				return;

			context.ReportDiagnostic(Diagnostic.Create(s_rule, invocation.ArgumentList.GetLocation()));
		}
		catch (NullReferenceException)
		{
			// A NullReferenceException happens inconsistently on the build server, breaking a small percentage of builds.
			// We have been unable to track it down, so are just ignoring it (and are trusting that the analyzer will work
			// to catch bugs on developers' systems.)
		}
	}

	private static readonly DiagnosticDescriptor s_rule = new DiagnosticDescriptor(
		id: DiagnosticId,
		title: "UntilCanceled() Usage",
		messageFormat: "UntilCanceled() may only be used in methods that return IEnumerable<AsyncAction>",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{DiagnosticId}");
}
