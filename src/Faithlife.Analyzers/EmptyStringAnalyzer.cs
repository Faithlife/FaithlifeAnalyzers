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
	public sealed class EmptyStringAnalyzer : DiagnosticAnalyzer
	{
		public override void Initialize(AnalysisContext context)
		{
			context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
			{
				var stringClass = compilationStartAnalysisContext.Compilation.GetTypeByMetadataName("System.String");
				if (stringClass == null)
					return;

				var emptyString = stringClass.GetMembers("Empty");
				if (emptyString.Length != 1)
					return;

				compilationStartAnalysisContext.RegisterSyntaxNodeAction(c => AnalyzeSyntax(c, emptyString[0]), SyntaxKind.SimpleMemberAccessExpression);
			});
		}

		public const string DiagnosticId = "FL0009";

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_rule);

		private static void AnalyzeSyntax(SyntaxNodeAnalysisContext context, ISymbol emptyString)
		{
			var syntax = (MemberAccessExpressionSyntax)context.Node;

			var symbolInfo = context.SemanticModel.GetSymbolInfo(syntax.Name);
			if (symbolInfo.Symbol == null || !symbolInfo.Symbol.Equals(emptyString))
				return;

			context.ReportDiagnostic(Diagnostic.Create(s_rule, syntax.GetLocation()));
		}

		static readonly DiagnosticDescriptor s_rule = new DiagnosticDescriptor(
			id: DiagnosticId,
			title: "Prefer \"\" over string.Empty",
			messageFormat: "Prefer \"\" over string.Empty.",
			category: "Usage",
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{DiagnosticId}");
	}
}
