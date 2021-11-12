using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Faithlife.Analyzers
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class VerbatimStringAnalyzer : DiagnosticAnalyzer
	{
		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

			context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
				compilationStartAnalysisContext.RegisterSyntaxNodeAction(AnalyzeSyntax, SyntaxKind.StringLiteralExpression));
		}

		public const string DiagnosticId = "FL0016";

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_rule);

		private static void AnalyzeSyntax(SyntaxNodeAnalysisContext context)
		{
			var literalSyntax = (LiteralExpressionSyntax) context.Node;
			var text = literalSyntax.Token.Text ?? "";

			if (!text.StartsWith("@", StringComparison.Ordinal))
				return;

			if (charsWhereVerbatimIsntUnnecessary.Any(text.Contains))
				return;

			context.ReportDiagnostic(Diagnostic.Create(s_rule, literalSyntax.GetLocation()));
		}

		private const string charsWhereVerbatimIsntUnnecessary = "\n\t\\";

		private static readonly DiagnosticDescriptor s_rule = new(
			id: DiagnosticId,
			title: "Unnecessary use of verbatim string literal",
			messageFormat: "Avoid using verbatim string literals without special characters.",
			category: "Usage",
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{DiagnosticId}");
	}
}
