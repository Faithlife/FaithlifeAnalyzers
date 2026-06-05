using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Faithlife.Analyzers;

internal sealed class InvariantConvertMatch
{
	private InvariantConvertMatch(InvariantConvertMatchKind kind, InvocationExpressionSyntax invocation, string methodName, ExpressionSyntax? inputExpression,
		ExpressionSyntax? receiverExpression, SyntaxToken outVariableIdentifier, bool isNegated, bool canFix)
	{
		Kind = kind;
		Invocation = invocation;
		MethodName = methodName;
		InputExpression = inputExpression;
		ReceiverExpression = receiverExpression;
		OutVariableIdentifier = outVariableIdentifier;
		IsNegated = isNegated;
		CanFix = canFix;
	}

	public static InvariantConvertMatch CreateParse(InvocationExpressionSyntax invocation, string methodName, ExpressionSyntax inputExpression) =>
		new(InvariantConvertMatchKind.Parse, invocation, methodName, inputExpression, null, default, false, true);

	public static InvariantConvertMatch CreateTryParse(InvocationExpressionSyntax invocation, string methodName, ExpressionSyntax inputExpression,
		SyntaxToken outVariableIdentifier, bool isNegated) =>
		new(InvariantConvertMatchKind.TryParse, invocation, methodName, inputExpression, null, outVariableIdentifier, isNegated, true);

	public static InvariantConvertMatch CreateUnfixableTryParse(InvocationExpressionSyntax invocation) =>
		new(InvariantConvertMatchKind.TryParse, invocation, "", null, null, default, false, false);

	public static InvariantConvertMatch CreateToString(InvocationExpressionSyntax invocation, ExpressionSyntax receiverExpression) =>
		new(InvariantConvertMatchKind.ToString, invocation, "ToInvariantString", null, receiverExpression, default, false, true);

	public InvariantConvertMatchKind Kind { get; }

	public InvocationExpressionSyntax Invocation { get; }

	public string MethodName { get; }

	public ExpressionSyntax? InputExpression { get; }

	public ExpressionSyntax? ReceiverExpression { get; }

	public SyntaxToken OutVariableIdentifier { get; }

	public bool IsNegated { get; }

	public bool CanFix { get; }
}
