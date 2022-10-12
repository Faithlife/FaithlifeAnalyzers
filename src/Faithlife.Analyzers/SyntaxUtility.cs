using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Faithlife.Analyzers;

internal static class SyntaxUtility
{
	/// <summary>
	/// Generates an identifier name that has a high likelihood of not causing any naming conflicts.
	/// </summary>
	/// <param name="desiredName">The default name for the identifier.</param>
	/// <param name="declarationLocation">A syntax location within the member in which the identifier will be declared.</param>
	/// <param name="originalDeclaration">
	/// An optional syntax node designating an existing variable declaration.
	/// If specified, this node will be ignored when checking for uniqueness.
	/// </param>
	/// <returns>A <see cref="Microsoft.CodeAnalysis.SyntaxToken">SyntaxToken</see> for the new identifier.</returns>
	/// <remarks>
	/// Currently, this method takes a conservative approach to declarations within the member and pays no attention at all
	/// to identifiers declared outside the scope of the member.
	/// </remarks>
	public static SyntaxToken GetHoistableIdentifier(string desiredName, SyntaxNode declarationLocation, SyntaxNode? originalDeclaration = null)
	{
		if (desiredName is null)
			throw new ArgumentNullException(nameof(desiredName));
		if (declarationLocation is null)
			throw new ArgumentNullException(nameof(declarationLocation));

		var containingMember = declarationLocation.FirstAncestorOrSelf<MemberDeclarationSyntax>();
		if (containingMember is null)
			throw new InvalidOperationException("Cannot declare a variable at this scope.");

		// Currently, this makes no attempt to be clever about scoping rules.
		var unavailableNames = new HashSet<string>(
			containingMember
				.DescendantNodes()
				.Select(node =>
				{
					if (node == originalDeclaration)
						return default;
					if (node is ParameterSyntax parameter)
						return parameter.Identifier;
					if (node is TypeParameterSyntax typeParameter)
						return typeParameter.Identifier;
					if (node is VariableDeclaratorSyntax variableDeclarator)
						return variableDeclarator.Identifier;
					if (node is SingleVariableDesignationSyntax singleVariableDesignation)
						return singleVariableDesignation.Identifier;
					if (node is FromClauseSyntax fromClause)
						return fromClause.Identifier;
					if (node is LetClauseSyntax letClause)
						return letClause.Identifier;
					if (node is QueryContinuationSyntax queryContinuation)
						return queryContinuation.Identifier;

					return default;
				})
				.Select(x => x.Text)
				.Where(x => x?.StartsWith(desiredName, StringComparison.Ordinal) ?? false),
			StringComparer.Ordinal);

		// This will also make "value" unavailable within the get accessor.
		if (containingMember is BasePropertyDeclarationSyntax)
			unavailableNames.Add("value");

		var suffix = 0;
		var candidateName = desiredName;
		while (unavailableNames.Contains(candidateName))
			candidateName = desiredName + (++suffix).ToString(CultureInfo.InvariantCulture);

		return Identifier(candidateName);
	}

	public static ExpressionSyntax ReplaceIdentifiers(string expression, params (string OriginalIdentifierName, ExpressionSyntax Replacement)[] identifiers) =>
		SimplifiableParentheses(ReplaceIdentifiers(ParseExpression(expression), identifiers));

	public static ExpressionSyntax ReplaceIdentifiers(ExpressionSyntax expression, params (string OriginalIdentifierName, ExpressionSyntax Replacement)[] identifiers) =>
		identifiers.Aggregate(
			expression,
			(currentExpression, identifier) => ReplaceIdentifier(currentExpression, identifier.OriginalIdentifierName, identifier.Replacement));

	public static ExpressionSyntax ReplaceIdentifier(ExpressionSyntax expression, string originalIdentifierName, ExpressionSyntax replacement)
	{
		var targetNodes = expression.DescendantNodes()
			.OfType<IdentifierNameSyntax>()
			.Where(x => x.Identifier.Text == originalIdentifierName)
			.ToList();

		if (targetNodes.Count == 0)
			return expression;

		return expression.ReplaceNodes(targetNodes, (original, updated) => replacement.WithTriviaFrom(original));
	}

	public static ParenthesizedExpressionSyntax SimplifiableParentheses(ExpressionSyntax expression) =>
		ParenthesizedExpression(expression).WithAdditionalAnnotations(Simplifier.Annotation);

	public static TypeSyntax ParseSimplifiableTypeName(string name) =>
		ParseTypeName(name).WithAdditionalAnnotations(Simplifier.Annotation);
}
