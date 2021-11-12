using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Faithlife.Analyzers
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class NumericConstantAssignmentAnalyzer : DiagnosticAnalyzer
	{
		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

			context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
				compilationStartAnalysisContext.RegisterSyntaxNodeAction(AnalyzeSyntax, SyntaxKind.FieldDeclaration));
		}

		public const string DiagnosticId = "FL0017";

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_rule);

		private static bool IsTimeRelatedExpression(ExpressionSyntax expression)
		{
			if (expression is LiteralExpressionSyntax literal)
				return literal.Token.Value is int intValue && s_commonTimeRelatedConstants.Contains(intValue);

			if (expression is BinaryExpressionSyntax binaryExpression && binaryExpression.IsKind(SyntaxKind.MultiplyExpression))
				return IsTimeRelatedExpression(binaryExpression.Left) && IsTimeRelatedExpression(binaryExpression.Right);

			return false;
		}

		private static void AnalyzeSyntax(SyntaxNodeAnalysisContext context)
		{
			var fieldDeclarationSyntax = (FieldDeclarationSyntax) context.Node;

			var isConstDeclaration = fieldDeclarationSyntax.Modifiers.Any(token=>token.IsKind(SyntaxKind.ConstKeyword));
			if (!isConstDeclaration)
				return;

			var declarations = fieldDeclarationSyntax.Declaration.Variables;
			foreach (var declaratorSyntax in declarations)
			{
				var initializerValue = declaratorSyntax.Initializer.Value;
				if (IsTimeRelatedExpression(initializerValue))
					context.ReportDiagnostic(Diagnostic.Create(s_rule, initializerValue.GetLocation()));
			}
		}

		private static readonly List<int> s_commonTimeRelatedConstants = new() { 365, 60, 31, 30, 28, 24, 7 };

		private static readonly DiagnosticDescriptor s_rule = new(
			id: DiagnosticId,
			title: "Avoid common date/time constants",
			messageFormat: "Avoid creating constants for date/time operations; consider using a library instead.",
			category: "Usage",
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{DiagnosticId}");
	}
}
