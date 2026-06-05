using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Faithlife.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InvariantConvertAnalyzer : DiagnosticAnalyzer
{
	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
		{
			if (!HasInvariantConvert(compilationStartAnalysisContext.Compilation))
				return;

			compilationStartAnalysisContext.RegisterSyntaxNodeAction(AnalyzeSyntax, SyntaxKind.InvocationExpression);
		});
	}

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

	internal static bool HasInvariantConvert(Compilation compilation) =>
		compilation.GetTypeByMetadataName(c_invariantConvertMetadataName) is { } invariantConvertType &&
		HasMethod(invariantConvertType, "ParseBoolean") &&
		HasMethod(invariantConvertType, "ParseDouble") &&
		HasMethod(invariantConvertType, "ParseInt32") &&
		HasMethod(invariantConvertType, "ParseInt64") &&
		HasMethod(invariantConvertType, "TryParseBoolean") &&
		HasMethod(invariantConvertType, "TryParseDouble") &&
		HasMethod(invariantConvertType, "TryParseInt32") &&
		HasMethod(invariantConvertType, "TryParseInt64") &&
		HasMethod(invariantConvertType, "ToInvariantString");

	internal static InvariantConvertMatch? TryCreateMatch(InvocationExpressionSyntax invocation, SemanticModel semanticModel, CancellationToken cancellationToken)
	{
		if (!HasInvariantConvert(semanticModel.Compilation))
			return null;

		if (semanticModel.GetSymbolInfo(invocation.Expression, cancellationToken).Symbol is not IMethodSymbol methodSymbol)
			return null;

		return TryCreateParseMatch(invocation, methodSymbol, semanticModel, cancellationToken) ??
			TryCreateTryParseMatch(invocation, methodSymbol, semanticModel, cancellationToken) ??
			TryCreateToStringMatch(invocation, methodSymbol, semanticModel, cancellationToken);
	}

	private static void AnalyzeSyntax(SyntaxNodeAnalysisContext context)
	{
		var invocation = (InvocationExpressionSyntax) context.Node;
		if (TryCreateMatch(invocation, context.SemanticModel, context.CancellationToken) is not null)
			context.ReportDiagnostic(Diagnostic.Create(s_rule, invocation.GetLocation()));
	}

	private static InvariantConvertMatch? TryCreateParseMatch(InvocationExpressionSyntax invocation, IMethodSymbol methodSymbol, SemanticModel semanticModel, CancellationToken cancellationToken)
	{
		if (!methodSymbol.IsStatic || methodSymbol.Name != "Parse")
			return null;

		if (GetConversion(methodSymbol.ContainingType.SpecialType) is not { } conversion)
			return null;

		var arguments = invocation.ArgumentList.Arguments;
		if (arguments.Count == 0 || !ParameterIsString(methodSymbol.Parameters[0]))
			return null;

		var inputArgument = GetArgument(arguments, methodSymbol, 0);
		if (inputArgument is null)
			return null;

		if (conversion.SpecialType == SpecialType.System_Boolean)
		{
			return methodSymbol.Parameters.Length == 1 ?
				InvariantConvertMatch.CreateParse(invocation, conversion.ParseMethodName, inputArgument.Expression) :
				null;
		}

		if (methodSymbol.Parameters.Length == 2)
		{
			if (GetArgument(arguments, methodSymbol, 1) is { } providerArgument &&
				IsInvariantCulture(providerArgument.Expression, semanticModel, cancellationToken))
			{
				return InvariantConvertMatch.CreateParse(invocation, conversion.ParseMethodName, inputArgument.Expression);
			}
		}
		else if (methodSymbol.Parameters.Length == 3)
		{
			if (GetArgument(arguments, methodSymbol, 1) is { } styleArgument &&
				GetArgument(arguments, methodSymbol, 2) is { } providerArgument &&
				IsAllowedNumberStyle(styleArgument.Expression, conversion, semanticModel, cancellationToken) &&
				IsInvariantCulture(providerArgument.Expression, semanticModel, cancellationToken))
			{
				return InvariantConvertMatch.CreateParse(invocation, conversion.ParseMethodName, inputArgument.Expression);
			}
		}

		return null;
	}

	private static InvariantConvertMatch? TryCreateTryParseMatch(InvocationExpressionSyntax invocation, IMethodSymbol methodSymbol, SemanticModel semanticModel, CancellationToken cancellationToken)
	{
		if (!methodSymbol.IsStatic || methodSymbol.Name != "TryParse")
			return null;

		if (GetConversion(methodSymbol.ContainingType.SpecialType) is not { } conversion)
			return null;

		var arguments = invocation.ArgumentList.Arguments;
		if (arguments.Count == 0 || !ParameterIsString(methodSymbol.Parameters[0]))
			return null;

		var inputArgument = GetArgument(arguments, methodSymbol, 0);
		var outputArgument = GetArgument(arguments, methodSymbol, methodSymbol.Parameters.Length - 1);
		if (inputArgument is null || outputArgument is null)
			return null;

		if (conversion.SpecialType == SpecialType.System_Boolean)
		{
			if (methodSymbol.Parameters.Length != 2)
				return null;

			return CreateTryParseMatch(invocation, conversion.TryParseMethodName, inputArgument.Expression, outputArgument);
		}

		if (methodSymbol.Parameters.Length == 3)
		{
			if (GetArgument(arguments, methodSymbol, 1) is { } providerArgument &&
				IsInvariantCulture(providerArgument.Expression, semanticModel, cancellationToken))
			{
				return CreateTryParseMatch(invocation, conversion.TryParseMethodName, inputArgument.Expression, outputArgument);
			}
		}
		else if (methodSymbol.Parameters.Length == 4)
		{
			if (GetArgument(arguments, methodSymbol, 1) is { } styleArgument &&
				GetArgument(arguments, methodSymbol, 2) is { } providerArgument &&
				IsAllowedNumberStyle(styleArgument.Expression, conversion, semanticModel, cancellationToken) &&
				IsInvariantCulture(providerArgument.Expression, semanticModel, cancellationToken))
			{
				return CreateTryParseMatch(invocation, conversion.TryParseMethodName, inputArgument.Expression, outputArgument);
			}
		}

		return null;
	}

	private static InvariantConvertMatch CreateTryParseMatch(InvocationExpressionSyntax invocation, string methodName, ExpressionSyntax inputExpression, ArgumentSyntax outputArgument)
	{
		if (TryGetOutVariable(outputArgument, out var outVariableIdentifier) &&
			TryGetSupportedTryParseCondition(invocation, out var isNegated, out var ifStatement) &&
			IsSafeTryParseScope(ifStatement, outVariableIdentifier, isNegated))
		{
			return InvariantConvertMatch.CreateTryParse(invocation, methodName, inputExpression, outVariableIdentifier, isNegated);
		}

		return InvariantConvertMatch.CreateUnfixableTryParse(invocation);
	}

	private static InvariantConvertMatch? TryCreateToStringMatch(InvocationExpressionSyntax invocation, IMethodSymbol methodSymbol, SemanticModel semanticModel, CancellationToken cancellationToken)
	{
		if (methodSymbol.IsStatic || methodSymbol.Name != "ToString" || methodSymbol.Parameters.Length != 1)
			return null;

		if (invocation.Expression is not MemberAccessExpressionSyntax memberAccessExpression)
			return null;

		if (GetConversion(methodSymbol.ContainingType.SpecialType) is null)
			return null;

		if (!IsInvariantCulture(invocation.ArgumentList.Arguments[0].Expression, semanticModel, cancellationToken))
			return null;

		return InvariantConvertMatch.CreateToString(invocation, memberAccessExpression.Expression);
	}

	private static bool ParameterIsString(IParameterSymbol parameter) =>
		parameter.Type.SpecialType == SpecialType.System_String;

	private static ArgumentSyntax? GetArgument(SeparatedSyntaxList<ArgumentSyntax> arguments, IMethodSymbol methodSymbol, int parameterIndex)
	{
		var parameter = methodSymbol.Parameters[parameterIndex];
		for (int i = 0; i < arguments.Count; i++)
		{
			var argument = arguments[i];
			if (argument.NameColon is null)
			{
				if (i == parameterIndex)
					return argument;
			}
			else if (argument.NameColon.Name.Identifier.ValueText == parameter.Name)
			{
				return argument;
			}
		}

		return null;
	}

	private static bool TryGetOutVariable(ArgumentSyntax argument, out SyntaxToken identifier)
	{
		identifier = default;

		if (!argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword))
			return false;

		if (argument.Expression is not DeclarationExpressionSyntax { Designation: SingleVariableDesignationSyntax designation })
			return false;

		identifier = designation.Identifier;
		return true;
	}

	private static bool TryGetSupportedTryParseCondition(InvocationExpressionSyntax invocation, out bool isNegated, out IfStatementSyntax ifStatement)
	{
		isNegated = false;
		ifStatement = null!;

		ExpressionSyntax conditionExpression = invocation;
		if (conditionExpression.Parent is ParenthesizedExpressionSyntax parenthesizedExpression)
			conditionExpression = parenthesizedExpression;

		if (conditionExpression.Parent is PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.LogicalNotExpression } logicalNotExpression)
		{
			isNegated = true;
			conditionExpression = logicalNotExpression;
		}

		if (conditionExpression.Parent is ParenthesizedExpressionSyntax parenthesizedCondition)
			conditionExpression = parenthesizedCondition;

		if (conditionExpression.Parent is IfStatementSyntax parentIfStatement && parentIfStatement.Condition == conditionExpression)
		{
			ifStatement = parentIfStatement;
			return true;
		}

		return false;
	}

	private static bool IsSafeTryParseScope(IfStatementSyntax ifStatement, SyntaxToken outVariableIdentifier, bool isNegated)
	{
		if (ifStatement.Else is { } elseClause && ContainsIdentifierReference(elseClause.Statement, outVariableIdentifier))
			return false;

		if (ContainsIdentifierReference(ifStatement.Statement, outVariableIdentifier))
		{
			if (isNegated)
				return false;
		}

		if (!ContainsReferenceAfterStatement(ifStatement, outVariableIdentifier))
			return true;

		return isNegated ?
			StatementAlwaysExits(ifStatement.Statement) :
			ifStatement.Else is not null && StatementAlwaysExits(ifStatement.Else.Statement);
	}

	private static bool ContainsReferenceAfterStatement(StatementSyntax statement, SyntaxToken identifier)
	{
		if (statement.Parent is not BlockSyntax block)
			return false;

		var foundStatement = false;
		foreach (var childStatement in block.Statements)
		{
			if (foundStatement)
			{
				if (ContainsIdentifierReference(childStatement, identifier))
					return true;
			}
			else if (childStatement == statement)
			{
				foundStatement = true;
			}
		}

		return false;
	}

	private static bool ContainsIdentifierReference(SyntaxNode node, SyntaxToken identifier)
	{
		foreach (var identifierName in node.DescendantNodes().OfType<IdentifierNameSyntax>())
		{
			if (identifierName.Identifier.ValueText == identifier.ValueText)
				return true;
		}

		return false;
	}

	private static bool StatementAlwaysExits(StatementSyntax statement)
	{
		if (statement is ReturnStatementSyntax || statement is ThrowStatementSyntax)
			return true;

		if (statement is BlockSyntax block && block.Statements.Count != 0)
			return StatementAlwaysExits(block.Statements.Last());

		return false;
	}

	private static bool IsInvariantCulture(ExpressionSyntax expression, SemanticModel semanticModel, CancellationToken cancellationToken) =>
		semanticModel.Compilation.GetTypeByMetadataName("System.Globalization.CultureInfo") is { } cultureInfoType &&
		semanticModel.GetSymbolInfo(expression, cancellationToken).Symbol is { Name: "InvariantCulture" } symbol &&
		SymbolEqualityComparer.Default.Equals(symbol.ContainingType, cultureInfoType);

	private static bool IsAllowedNumberStyle(ExpressionSyntax expression, InvariantConvertConversion conversion, SemanticModel semanticModel, CancellationToken cancellationToken) =>
		semanticModel.Compilation.GetTypeByMetadataName("System.Globalization.NumberStyles") is { } numberStylesType &&
		semanticModel.GetSymbolInfo(expression, cancellationToken).Symbol is { } symbol &&
		SymbolEqualityComparer.Default.Equals(symbol.ContainingType, numberStylesType) &&
		symbol.Name == conversion.NumberStyleName;

	private static InvariantConvertConversion? GetConversion(SpecialType specialType) =>
		specialType switch
		{
			SpecialType.System_Boolean => s_booleanConversion,
			SpecialType.System_Double => s_doubleConversion,
			SpecialType.System_Int32 => s_int32Conversion,
			SpecialType.System_Int64 => s_int64Conversion,
			_ => null,
		};

	private static bool HasMethod(INamedTypeSymbol type, string methodName)
	{
		foreach (var member in type.GetMembers(methodName))
		{
			if (member is IMethodSymbol { DeclaredAccessibility: Accessibility.Public, IsStatic: true })
				return true;
		}

		return false;
	}

	public const string DiagnosticId = "FL0026";

	private const string c_invariantConvertMetadataName = "Libronix.Utility.Invariant.InvariantConvert";

	private static readonly InvariantConvertConversion s_booleanConversion = new(SpecialType.System_Boolean, "ParseBoolean", "TryParseBoolean", "");
	private static readonly InvariantConvertConversion s_doubleConversion = new(SpecialType.System_Double, "ParseDouble", "TryParseDouble", "Float");
	private static readonly InvariantConvertConversion s_int32Conversion = new(SpecialType.System_Int32, "ParseInt32", "TryParseInt32", "Integer");
	private static readonly InvariantConvertConversion s_int64Conversion = new(SpecialType.System_Int64, "ParseInt64", "TryParseInt64", "Integer");

	private static readonly DiagnosticDescriptor s_rule = new(
		id: DiagnosticId,
		title: "Use InvariantConvert",
		messageFormat: "Use InvariantConvert for invariant culture conversions",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: true,
		helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/blob/-/docs/{DiagnosticId}.md");
}
