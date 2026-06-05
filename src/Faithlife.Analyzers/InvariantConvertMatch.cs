using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Faithlife.Analyzers;

internal sealed class InvariantConvertMatch
{
	private InvariantConvertMatch(InvariantConvertMatchKind kind, InvocationExpressionSyntax invocation, string methodName, ExpressionSyntax? inputExpression,
		ExpressionSyntax? receiverExpression, SyntaxToken outVariableIdentifier, bool isNegated)
	{
		Kind = kind;
		Invocation = invocation;
		MethodName = methodName;
		InputExpression = inputExpression;
		ReceiverExpression = receiverExpression;
		OutVariableIdentifier = outVariableIdentifier;
		IsNegated = isNegated;
	}

	public static InvariantConvertMatch CreateParse(InvocationExpressionSyntax invocation, string methodName, ExpressionSyntax inputExpression) =>
		new(InvariantConvertMatchKind.Parse, invocation, methodName, inputExpression, null, default, false);

	public static InvariantConvertMatch CreateTryParse(InvocationExpressionSyntax invocation, string methodName, ExpressionSyntax inputExpression,
		SyntaxToken outVariableIdentifier, bool isNegated) =>
		new(InvariantConvertMatchKind.TryParse, invocation, methodName, inputExpression, null, outVariableIdentifier, isNegated);

	public static InvariantConvertMatch CreateToString(InvocationExpressionSyntax invocation, ExpressionSyntax receiverExpression) =>
		new(InvariantConvertMatchKind.ToString, invocation, "ToInvariantString", null, receiverExpression, default, false);

	public InvariantConvertMatchKind Kind { get; }

	public InvocationExpressionSyntax Invocation { get; }

	public string MethodName { get; }

	public ExpressionSyntax? InputExpression { get; }

	public ExpressionSyntax? ReceiverExpression { get; }

	public SyntaxToken OutVariableIdentifier { get; }

	public bool IsNegated { get; }
}
