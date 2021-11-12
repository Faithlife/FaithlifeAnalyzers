using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Faithlife.Analyzers
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class NullTestTernariesAnalyzer : DiagnosticAnalyzer
	{
		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

			context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
			{
				compilationStartAnalysisContext.RegisterSyntaxNodeAction(c => AnalyzeSyntax(c), SyntaxKind.ConditionalExpression);
			});
		}

		private static void AnalyzeSyntax(SyntaxNodeAnalysisContext context)
		{
			var expression = (ConditionalExpressionSyntax) context.Node;
			var falseValue = expression.WhenFalse;
			var trueValue = expression.WhenTrue;

			if (expression.Condition is BinaryExpressionSyntax binaryExpression)
			{
				var lExpression = binaryExpression.Left;
				var rExpression = binaryExpression.Right;

				if ((falseValue.Kind() == SyntaxKind.NullLiteralExpression || trueValue.Kind() == SyntaxKind.NullLiteralExpression) && (lExpression.Kind() == SyntaxKind.NullLiteralExpression || rExpression.Kind() == SyntaxKind.NullLiteralExpression))
				{
					context.ReportDiagnostic(Diagnostic.Create(s_rule, expression.GetLocation()));
				}
			}
			else if (expression.Condition is MemberAccessExpressionSyntax memberAccessExpression)
			{
				if (memberAccessExpression.Kind() == SyntaxKind.SimpleMemberAccessExpression && memberAccessExpression.Name.Identifier.Text == "HasValue")
				{
					context.ReportDiagnostic(Diagnostic.Create(s_rule, expression.GetLocation()));
				}
			}
		}

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_rule);

		public const string DiagnosticId = "FL0015";

		private static readonly DiagnosticDescriptor s_rule = new(
			id: DiagnosticId,
			title: "Null Checking Ternaries Usage",
			messageFormat: "Prefer null conditional operators over ternaries explicitly checking for null",
			category: "Usage",
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{DiagnosticId}"
		);
	}
}
