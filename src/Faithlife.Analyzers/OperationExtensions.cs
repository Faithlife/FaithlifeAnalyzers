using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Faithlife.Analyzers
{
	internal static class OperationExtensions
	{
		public static Location GetMethodNameLocation(this IInvocationOperation invocationOperation)
		{
			var invocationSyntax = (InvocationExpressionSyntax) invocationOperation.Syntax;
			switch (invocationSyntax.Expression)
			{
			case MemberAccessExpressionSyntax memberAccessExpression:
				return memberAccessExpression.Name.GetLocation();
			case ConditionalAccessExpressionSyntax conditionalAccessExpression:
				return conditionalAccessExpression.WhenNotNull.GetLocation();
			default:
				return invocationSyntax.GetLocation();
			}
		}
	}
}
