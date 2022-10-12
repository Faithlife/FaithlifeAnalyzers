using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Faithlife.Analyzers
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class UriToStringAnalyzer : DiagnosticAnalyzer
	{
		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

			context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
			{
				var uriType = compilationStartAnalysisContext.Compilation.GetTypeByMetadataName("System.Uri");
				if (uriType is null)
					return;

				compilationStartAnalysisContext.RegisterSyntaxNodeAction(c => AnalyzeSyntax(c, uriType), SyntaxKind.InvocationExpression);
			});
		}

		private static void AnalyzeSyntax(SyntaxNodeAnalysisContext context, INamedTypeSymbol uriType)
		{
			var invocation = (InvocationExpressionSyntax) context.Node;

			var method = context.SemanticModel.GetSymbolInfo(invocation.Expression).Symbol as IMethodSymbol;
			if (method?.Name != "ToString" || method.ContainingType != uriType)
				return;

			var location = invocation.Expression switch
			{
				MemberAccessExpressionSyntax memberAccess => memberAccess.Name.GetLocation(),
				MemberBindingExpressionSyntax memberBinding => memberBinding.Name.GetLocation(),
				_ => null,
			};

			context.ReportDiagnostic(Diagnostic.Create(s_rule, location));
		}

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_rule);

		public const string DiagnosticId = "FL0013";

		private static readonly DiagnosticDescriptor s_rule = new(
			id: DiagnosticId,
			title: "Uri ToString Usage",
			messageFormat: "Do not use Uri.ToString()",
			category: "Usage",
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{DiagnosticId}"
		);
	}
}
