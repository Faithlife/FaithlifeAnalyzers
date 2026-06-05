using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Faithlife.Analyzers;

internal sealed class InvariantConvertMatch
{
	public InvariantConvertMatch(InvariantConvertKind kind, string suggestedMethodName, ExpressionSyntax valueExpression, ArgumentSyntax? outArgument)
	{
		Kind = kind;
		SuggestedMethodName = suggestedMethodName;
		ValueExpression = valueExpression;
		OutArgument = outArgument;
	}

	public InvariantConvertKind Kind { get; }

	public string SuggestedMethodName { get; }

	// For Parse and TryParse, the string argument being parsed; for ToString, the receiver whose value is being formatted.
	public ExpressionSyntax ValueExpression { get; }

	// For TryParse, the `out` argument; otherwise null.
	public ArgumentSyntax? OutArgument { get; }
}
