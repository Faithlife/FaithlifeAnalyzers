using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Faithlife.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NullableHasValueConditionalAnalyzer : DiagnosticAnalyzer
{
	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterSyntaxNodeAction(AnalyzeSyntax, SyntaxKind.ConditionalExpression);
	}

	public const string DiagnosticId = "FL0021";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

	private static void AnalyzeSyntax(SyntaxNodeAnalysisContext context)
	{
		var conditional = (ConditionalExpressionSyntax) context.Node;

		// false branch has to be "null"
		if (!IsNull(conditional.WhenFalse))
			return;

		// condition has to end with .HasValue
		if (conditional.Condition is not MemberAccessExpressionSyntax { Name.Identifier.Text: "HasValue" } conditionAccess)
			return;

		// true branch has to start with nullable.Value
		if (!StartsWithValue(conditional.WhenTrue, conditionAccess.Expression))
			return;

		// the type of the nullable expression has to be Nullable<T>
		if (context.SemanticModel.GetTypeInfo(conditionAccess.Expression).Type is not INamedTypeSymbol nullableType ||
			!nullableType.ConstructedFrom.SpecialType.Equals(SpecialType.System_Nullable_T))
		{
			return;
		}

		context.ReportDiagnostic(Diagnostic.Create(s_rule, conditional.GetLocation()));
	}

	private static bool StartsWithValue(ExpressionSyntax expression, ExpressionSyntax nullableExpression) =>
		expression switch
		{
			MemberAccessExpressionSyntax { Name.Identifier.Text: "Value" } member when SyntaxFactory.AreEquivalent(member.Expression, nullableExpression) => true,
			MemberAccessExpressionSyntax member => StartsWithValue(member.Expression, nullableExpression),
			InvocationExpressionSyntax invocation => StartsWithValue(invocation.Expression, nullableExpression),
			_ => false,
		};

	private static bool IsNull(ExpressionSyntax expr) =>
		expr switch
		{
			LiteralExpressionSyntax { Token.ValueText: "null" } => true,
			CastExpressionSyntax cast => IsNull(cast.Expression),
			_ => false,
		};

	private static readonly DiagnosticDescriptor s_rule = new(
		id: DiagnosticId,
		title: "Prefer null-conditional operator over nullable.HasValue ? nullable.Value : null",
		messageFormat: "Use null propagation",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{DiagnosticId}");
}
