using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Faithlife.Analyzers;

internal static class OperationExtensions
{
	public static Location GetMethodNameLocation(this IInvocationOperation invocationOperation)
	{
		switch (invocationOperation.Syntax)
		{
		case InvocationExpressionSyntax invocation:
			switch (invocation.Expression)
			{
			case MemberAccessExpressionSyntax memberAccessExpression:
				return memberAccessExpression.Name.GetLocation();
			case MemberBindingExpressionSyntax memberBinding:
				return memberBinding.Name.GetLocation();
			}
			break;

		case OrderingSyntax ordering:
			return ((OrderByClauseSyntax) ordering.Parent).OrderByKeyword.GetLocation();
		}

		return invocationOperation.Syntax.GetLocation();
	}
}
