using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Faithlife.Analyzers
{
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(IfNotNullCodeFixProvider)), Shared]
	public sealed class IfNotNullCodeFixProvider : CodeFixProvider
	{
		public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(IfNotNullAnalyzer.DiagnosticId);

		public sealed override FixAllProvider GetFixAllProvider() => IfNotNullFixAllProvider.Instance;

		public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

			var ifNotNullExtensionMethodType = semanticModel.Compilation.GetTypeByMetadataName("Libronix.Utility.IfNotNull.IfNotNullExtensionMethod");
			if (ifNotNullExtensionMethodType is null)
				return;

			var ifNotNullMethods = ifNotNullExtensionMethodType.GetMembers("IfNotNull");
			if (ifNotNullMethods.Length == 0)
				return;

			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

			var diagnostic = context.Diagnostics.First();
			var diagnosticSpan = diagnostic.Location.SourceSpan;

			var diagnosticNode = root.FindNode(diagnosticSpan);

			var ifNotNullInvocation = diagnosticNode.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault();
			if (ifNotNullInvocation is null)
				return;

			var methodSymbol = semanticModel.GetSymbolInfo(ifNotNullInvocation).Symbol as IMethodSymbol;

			// The location of each of the arguments changes based on whether the method is invoked as an extension method.
			var targetExpression = methodSymbol.IsStatic ?
				ifNotNullInvocation.ArgumentList.Arguments[0].Expression :
				((MemberAccessExpressionSyntax) ifNotNullInvocation.Expression).Expression;

			var delegateExpression = methodSymbol.IsStatic ?
				ifNotNullInvocation.ArgumentList.Arguments[1].Expression :
				ifNotNullInvocation.ArgumentList.Arguments[0].Expression;

			var defaultArgumentIndex = methodSymbol.IsStatic ? 2 : 1;
			var defaultValueExpression = ifNotNullInvocation.ArgumentList.Arguments.Count > defaultArgumentIndex ?
				ifNotNullInvocation.ArgumentList.Arguments[defaultArgumentIndex].Expression :
				default;

			var outputTypeArgument = methodSymbol.Arity == 2 ? methodSymbol.TypeArguments[1] : default;

			// Handle default value generators.
			if (methodSymbol.Parameters.Length > defaultArgumentIndex &&
				(methodSymbol.Arity == 1 || methodSymbol.Parameters[defaultArgumentIndex].Type != outputTypeArgument))
			{
				if (defaultValueExpression is LambdaExpressionSyntax lambda)
				{
					// I can't imagine that anyone is using IfNotNull in async contexts, but
					// better to be safe than sorry.
					if (!lambda.AsyncKeyword.IsKind(SyntaxKind.None))
						return;

					// Only handle expression-bodied lambdas. A lambda with a block body would need to
					// be converted to a local method, and that's probably too much effort to bother with
					// relative to the number of them I would expect to see.
					if (!(lambda.Body is ExpressionSyntax defaultLambdaExpression))
						return;

					// The point of allowing lambas to be specified was so that the value could be lazily evaluated.
					// Now that we'll be using language constructs that are lazily evaluated, so we can just unwrap
					// the lambda.
					defaultValueExpression = defaultLambdaExpression;
				}
				else if (defaultValueExpression is AnonymousFunctionExpressionSyntax)
				{
					// If it's not a lambda, but it *is* an anonymous function, then it's a C# 2.0-style
					// anonymous delegate. Who would do such a thing?
					return;
				}
				else
				{
					// If it's not a lambda and it's not any other kind of anonymous function,
					// then it must be a reference to a delegate. We can transform this into
					// an invocation of the delegate.
					defaultValueExpression = InvocationExpression(defaultValueExpression);
				}
			}

			var outputTypeIsNullable = methodSymbol.Arity == 2 &&
				(outputTypeArgument.IsReferenceType ||
					((outputTypeArgument as INamedTypeSymbol)?.ConstructedFrom?.SpecialType.HasFlag(SpecialType.System_Nullable_T) ?? false));

			if (methodSymbol.Arity == 2)
			{
				// Explicitly supplying default(SomeType) or null is identical to not supplying anything.
				// However, the presence of a defaultValueExpression will prevent us from using
				// the most concise formulation for reference types. Clearing this out allows the other
				// optimizations to take place.
				//
				// On the other hand, value types always need a default value expression. This is because
				// the null-conditional operator always forces a result to be nullable (either a reference type
				// or Nullable<T>), while IfNotNull does not. In order to create a compatible replacement,
				// a default value expression must be used in order to ensure that the resulting expression
				// evaluates to the correct type.
				if (outputTypeIsNullable &&
					(defaultValueExpression is DefaultExpressionSyntax ||
						 (defaultValueExpression is LiteralExpressionSyntax defaultLiteral && defaultLiteral.IsKind(SyntaxKind.NullLiteralExpression))))
				{
					defaultValueExpression = null;
				}
				else if (defaultValueExpression is null && !outputTypeIsNullable)
				{
					if (!outputTypeArgument.CanBeReferencedByName)
						return;

					defaultValueExpression = DefaultExpression(GetTypeSyntax(outputTypeArgument));
				}
			}

			var lambdaExpression = delegateExpression as LambdaExpressionSyntax;
			if (lambdaExpression is null)
			{
				// Don't even bother trying to handle C# 2.0-style anonymous delegates.
				if (delegateExpression is AnonymousFunctionExpressionSyntax)
					return;

				// If this isn't an anonymous function, then it must be a reference to a
				// delegate. We can transform this into a lambda expression that invokes
				// the delegate.

				var parameterIdentifier = Identifier("value");
				lambdaExpression = SimpleLambdaExpression(
					Parameter(parameterIdentifier),
					InvocationExpression(
						SyntaxUtility.SimplifiableParentheses(delegateExpression),
						ArgumentList(
							SingletonSeparatedList(
								Argument(
									IdentifierName(parameterIdentifier))))));
			}
			else if (!lambdaExpression.AsyncKeyword.IsKind(SyntaxKind.None))
			{
				return;
			}

			var parameterList = GetLambdaParameters(lambdaExpression);
			if (parameterList.Length != 1)
				return;

			var lambdaExpressionBody = lambdaExpression.Body as ExpressionSyntax;
			// To properly handle block-bodied lambda expressions, we'll need to hoist
			// the body of the lambda to a local function and call it. For now, skip this.
			if (lambdaExpressionBody is null)
				return;

			// There are several factors that might prohibit us from using the conditional access operator.
			// If we discover any of them, we'll mark this as false and fall back to more verbose patterns.
			var canUseConditionalOperator = true;

			// A void IfNotNull invocation with a default expression cannot be converted
			// to use the conditional access operator because there would be nowhere to
			// "hang" the default expression.
			if (methodSymbol.Arity == 1 && defaultValueExpression is object)
				canUseConditionalOperator = false;

			var lambdaParameterIdentifier = parameterList[0].Identifier;

			if (lambdaExpressionBody.DescendantNodes()
				.OfType<IdentifierNameSyntax>()
				.Where(x => AreEquivalent(x.Identifier, lambdaParameterIdentifier))
				.Take(2)
				.Count() == 2)
			{
				canUseConditionalOperator = false;
			}

			// This one is a bit more subtle: a default expression will *usually* prevent us
			// from using the conditional access operator because the default should only
			// be used if the *target* is null, and using a null-coalescing operator might
			// cause the default to be used if the expression results in null.

			// The one exception to this is that if the TOutput argument is a value type:
			// in this case, the conditional access operator will only evaluate to null if the
			// target is null. This means that we can safely append a null-coalescing operator
			// to get the desired value.
			if (defaultValueExpression != null && outputTypeIsNullable)
				canUseConditionalOperator = false;

			// The conditional access operator does not work for delegate invocations, so
			// this case needs to be tweaked a bit. A lambda in the form
			//
			// x => x(argument)
			//
			// should be transformed to
			//
			// x => x.Invoke(argument)
			//
			// At which point, the remaining portion of the method will be able to handle
			// it appropriately.
			if (canUseConditionalOperator &&
				lambdaExpressionBody is InvocationExpressionSyntax toplevelInvocation &&
				toplevelInvocation.Expression is IdentifierNameSyntax toplevelIdentifier &&
				AreEquivalent(toplevelIdentifier.Identifier, lambdaParameterIdentifier))
			{
				lambdaExpressionBody = InvocationExpression(
					SyntaxUtility.ReplaceIdentifier(ParseExpression("target.Invoke"), "target", IdentifierName(lambdaParameterIdentifier)),
					toplevelInvocation.ArgumentList);
			}

			// Some usages of IfNotNull explicitly cast the result to Nullable<T>, which means
			// they can use the null-conditional operator without the cast or a null-conditional operator.
			var mainLambdaExpressionBody = lambdaExpressionBody;
			if (outputTypeIsNullable && !outputTypeArgument.IsReferenceType)
			{
				if (lambdaExpressionBody is CastExpressionSyntax cast)
					mainLambdaExpressionBody = cast.Expression;
				else if (lambdaExpressionBody is BinaryExpressionSyntax binaryExpression && binaryExpression.OperatorToken.IsKind(SyntaxKind.AsKeyword))
					mainLambdaExpressionBody = binaryExpression.Left;
			}

			if (canUseConditionalOperator && GetLeftmostDescendant(mainLambdaExpressionBody) is IdentifierNameSyntax leftmostExpression)
			{
				if (AreEquivalent(leftmostExpression.Identifier, lambdaParameterIdentifier))
				{
					ConditionalAccessExpressionSyntax conditionalAccess;
					if (leftmostExpression.Parent is MemberAccessExpressionSyntax leftMostMemberAccess)
					{
						conditionalAccess = ConditionalAccessExpression(
							targetExpression,
							mainLambdaExpressionBody.ReplaceNode(
								leftMostMemberAccess,
								MemberBindingExpression(leftMostMemberAccess.Name)));
					}
					else if (leftmostExpression.Parent is ElementAccessExpressionSyntax leftmostElementAccess)
					{
						conditionalAccess = ConditionalAccessExpression(
							targetExpression,
							mainLambdaExpressionBody.ReplaceNode(
								leftmostElementAccess,
								ElementBindingExpression(leftmostElementAccess.ArgumentList)));
					}
					else
					{
						conditionalAccess = default;
					}

					if (conditionalAccess != null)
					{
						ExpressionSyntax finalExpression = conditionalAccess;
						if (defaultValueExpression is object)
						{
							finalExpression = SyntaxUtility.ReplaceIdentifiers("fixedExpression ?? defaultExpression",
								("fixedExpression", finalExpression),
								("defaultExpression", defaultValueExpression));
						}

						context.RegisterCodeFix(
							CodeAction.Create(
								title: "Use conditional access operator",
								createChangedDocument: token => ReplaceValueAsync(
									context.Document,
									ifNotNullInvocation,
									SyntaxUtility.SimplifiableParentheses(finalExpression),
									token),
								c_fixName),
							diagnostic);

						return;
					}
				}
			}

			if (!methodSymbol.TypeArguments[0].CanBeReferencedByName)
				return;

			// It's possible that hoisting the lambda's declaration into the parent context will cause naming conflicts.
			// When this happens, we'll generate a new name that is unique to the context.
			var originalName = lambdaParameterIdentifier.Text;
			var hoistableIdentifier = SyntaxUtility.GetHoistableIdentifier(originalName, ifNotNullInvocation, parameterList[0]);
			if (!AreEquivalent(lambdaParameterIdentifier, hoistableIdentifier))
				lambdaExpressionBody = SyntaxUtility.ReplaceIdentifier(lambdaExpressionBody, originalName, IdentifierName(hoistableIdentifier));

			var conditionExpression = IsPatternExpression(targetExpression, DeclarationPattern(GetTypeSyntax(methodSymbol.TypeArguments[0]), SingleVariableDesignation(hoistableIdentifier)));

			// From this point on, the handling of void IfNotNull invocations is totally different from the others.
			// If we couldn't use the conditional access operator, a void invocation will need to be converted to
			// an if/else block, but this is not possible in all contexts.
			if (methodSymbol.Arity == 1)
			{
				// There might be other contexts in which we could convert a void IfNotNull invocation to an
				// if statement, but this is the only one that I'm confident about.
				if (ifNotNullInvocation.Parent is ExpressionStatementSyntax)
				{
					var ifStatement = IfStatement(
						conditionExpression,
						CreateStatement(lambdaExpressionBody));

					if (defaultValueExpression is object)
						ifStatement = ifStatement.WithElse(ElseClause(CreateStatement(defaultValueExpression)));

					context.RegisterCodeFix(
						CodeAction.Create(
							title: "Use pattern matching",
							createChangedDocument: token => ReplaceValueAsync(
								context.Document,
								ifNotNullInvocation.Parent,
								ifStatement,
								token),
							c_fixName),
						diagnostic);
				}

				return;
			}

			ExpressionSyntax replacementTarget;
			ExpressionSyntax replacementExpression;

			if (defaultValueExpression is null &&
				ifNotNullInvocation.Parent is BinaryExpressionSyntax parentExpression &&
				parentExpression.IsKind(SyntaxKind.CoalesceExpression) &&
				parentExpression.Left == ifNotNullInvocation &&
				(lambdaExpressionBody is AnonymousObjectCreationExpressionSyntax || lambdaExpressionBody is ObjectCreationExpressionSyntax))
			{
				// If we're certain that the lambda will return a non-null value (which is definitely the case with object creation expressions),
				// then we can attempt to replace a slightly larger block of code. This allows expressions involving anonymous types to work
				// in a few more contexts.
				replacementTarget = parentExpression;
				replacementExpression = ConditionalExpression(
					conditionExpression,
					lambdaExpressionBody,
					parentExpression.Right);
			}
			else if (defaultValueExpression is object || outputTypeArgument.CanBeReferencedByName)
			{
				replacementTarget = ifNotNullInvocation;
				replacementExpression = ConditionalExpression(
					conditionExpression,
					SyntaxUtility.SimplifiableParentheses(lambdaExpressionBody),
					defaultValueExpression ?? DefaultExpression(GetTypeSyntax(outputTypeArgument)));
			}
			else
			{
				// If we can't name the type, we can't create a proper default expression, so there's nothing left that can be done.
				return;
			}

			context.RegisterCodeFix(
				CodeAction.Create(
					title: "Use pattern matching",
					createChangedDocument: token => ReplaceValueAsync(
						context.Document,
						replacementTarget,
						SyntaxUtility.SimplifiableParentheses(replacementExpression),
						token),
					c_fixName),
				diagnostic);
		}

		private static StatementSyntax CreateStatement(ExpressionSyntax expression)
		{
			if (expression is ThrowExpressionSyntax throwExpression)
				return ThrowStatement(throwExpression.Expression);

			return ExpressionStatement(expression);
		}

		private static ImmutableArray<ParameterSyntax> GetLambdaParameters(LambdaExpressionSyntax lambda)
		{
			if (lambda is SimpleLambdaExpressionSyntax simpleLambda)
				return ImmutableArray.Create(simpleLambda.Parameter);

			if (lambda is ParenthesizedLambdaExpressionSyntax parenthesizedLambda)
				return parenthesizedLambda.ParameterList.Parameters.ToImmutableArray();

			return ImmutableArray<ParameterSyntax>.Empty;
		}

		private static ExpressionSyntax GetLeftmostDescendant(ExpressionSyntax expression)
		{
			ExpressionSyntax GetLeftmostChild(ExpressionSyntax x)
			{
				switch (x)
				{
					case InvocationExpressionSyntax invocation:
						return invocation.Expression;

					case MemberAccessExpressionSyntax memberAccess:
						return memberAccess.Expression;

					case ConditionalAccessExpressionSyntax conditionalAccess:
						return conditionalAccess.Expression;

					case ElementAccessExpressionSyntax elementAccess:
						return elementAccess.Expression;

					default:
						return null;
				}
			}

			var currentExpression = expression;
			while (true)
			{
				var nextChild = GetLeftmostChild(currentExpression);
				if (nextChild is null)
					break;
				currentExpression = nextChild;
			}

			return currentExpression;
		}

		private static async Task<Document> ReplaceValueAsync(Document document, SyntaxNode replacementTarget, SyntaxNode replacementNode, CancellationToken cancellationToken)
		{
			var root = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))
				.ReplaceNode(replacementTarget, replacementNode);

			var ifNotNullUsingDirective = root.DescendantNodes()
				.OfType<UsingDirectiveSyntax>()
				.FirstOrDefault(x => AreEquivalent(x.Name, s_ifNotNullNamespace));

			if (ifNotNullUsingDirective != null)
				root = root.ReplaceNode(ifNotNullUsingDirective, ifNotNullUsingDirective.WithAdditionalAnnotations(Simplifier.Annotation));

			return await Simplifier.ReduceAsync(
				await Simplifier.ReduceAsync(document.WithSyntaxRoot(root), cancellationToken: cancellationToken).ConfigureAwait(false),
				cancellationToken: cancellationToken).ConfigureAwait(false);
		}

		private static TypeSyntax GetTypeSyntax(ITypeSymbol typeName) =>
			SyntaxUtility.ParseSimplifiableTypeName(typeName.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

		private static readonly NameSyntax s_ifNotNullNamespace = ParseName("Libronix.Utility.IfNotNull");

		private const string c_fixName = "use-modern-language-features";
	}
}
