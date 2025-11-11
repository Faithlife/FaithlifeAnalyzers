using System.Collections.Immutable;
using System.Composition;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Faithlife.Analyzers;

/// <summary>
/// Code fix converting an interpolated string argument to a composite format with additional arguments.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(LoggerInterpolatedStringCodeFixProvider)), Shared]
public sealed class LoggerInterpolatedStringCodeFixProvider : CodeFixProvider
{
	public override ImmutableArray<string> FixableDiagnosticIds => [LoggerInterpolatedStringAnalyzer.DiagnosticId];

	public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		if (root is null)
			return;

		var diagnostic = context.Diagnostics.First();
		var node = root.FindNode(diagnostic.Location.SourceSpan);

		// The diagnostic is reported at the argument location, so we need to find the interpolated string
		// either directly (if the node is the interpolated string) or within the node's descendants
		var interpolated = node as InterpolatedStringExpressionSyntax ??
			node.DescendantNodesAndSelf().OfType<InterpolatedStringExpressionSyntax>().FirstOrDefault();

		if (interpolated is null)
			return;

		context.RegisterCodeFix(
			CodeAction.Create(
				title: "Convert to composite format string",
				createChangedDocument: c => ApplyFixAsync(context.Document, interpolated, c),
				equivalenceKey: "convert-to-composite-format"),
			diagnostic);
	}

	private static async Task<Document> ApplyFixAsync(Document document, InterpolatedStringExpressionSyntax interpolated, CancellationToken ct)
	{
		var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
		if (root is null)
			return document;

		var invocation = interpolated.FirstAncestorOrSelf<InvocationExpressionSyntax>();
		if (invocation is null)
			return document;

		var (formatLiteral, argumentExpressions) = BuildFormatLiteral(interpolated);

		// Replace first argument expression with a string literal.
		var originalArgs = invocation.ArgumentList.Arguments;
		var newFirstArg = originalArgs[0].WithExpression(
			SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(formatLiteral)));

		// Add new arguments for each interpolation hole.
		SeparatedSyntaxList<ArgumentSyntax> newArgList;
		if (argumentExpressions.Count == 0)
		{
			newArgList = SyntaxFactory.SeparatedList([newFirstArg]);
		}
		else
		{
			var list = originalArgs.ToList();
			list[0] = newFirstArg;
			list.AddRange(argumentExpressions.Select(SyntaxFactory.Argument));
			newArgList = SyntaxFactory.SeparatedList(list);
		}

		var newInvocation = invocation.WithArgumentList(invocation.ArgumentList.WithArguments(newArgList));
		var newRoot = root.ReplaceNode(invocation, newInvocation);
		return document.WithSyntaxRoot(newRoot);
	}

	private static (string Literal, List<ExpressionSyntax> Args) BuildFormatLiteral(InterpolatedStringExpressionSyntax interpolated)
	{
		var sb = new StringBuilder();
		var args = new List<ExpressionSyntax>();
		var index = 0;

		foreach (var content in interpolated.Contents)
		{
			switch (content)
			{
				case InterpolatedStringTextSyntax text:
					sb.Append(text.TextToken.ValueText);
					break;

				case InterpolationSyntax hole:
				{
					var alignment = hole.AlignmentClause?.ToString() ?? ""; // includes comma
					var format = hole.FormatClause?.ToString() ?? "";       // includes colon
					sb.Append('{')
					  .Append(index)
					  .Append(alignment)
					  .Append(format)
					  .Append('}');
					args.Add(hole.Expression.WithoutTrivia());
					index++;
					break;
				}
			}
		}

		return (sb.ToString(), args);
	}
}
