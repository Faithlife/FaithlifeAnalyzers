using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace Faithlife.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RedundantCancellationCheckAnalyzer : DiagnosticAnalyzer
{
	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(startContext =>
		{
			var contextType = startContext.Compilation.GetTypeByMetadataName("Libronix.Utility.Threading.AsyncMethodContext");
			var switchTokenType = startContext.Compilation.GetTypeByMetadataName("Libronix.Utility.Threading.SwitchToken");
			if (contextType is null || switchTokenType is null ||
				!SymbolEqualityComparer.Default.Equals(contextType.ContainingAssembly, switchTokenType.ContainingAssembly))
				return;

			var switchMethod = contextType.GetMembers("Switch").OfType<IMethodSymbol>().FirstOrDefault(x =>
				!x.IsStatic && x.Arity == 0 && x.ReturnsVoid && x.Parameters.Length == 1 &&
				x.Parameters[0].RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(x.Parameters[0].Type, switchTokenType));
			var checkMethod = contextType.GetMembers("ThrowIfCanceled").OfType<IMethodSymbol>().FirstOrDefault(x =>
				!x.IsStatic && x.Arity == 0 && x.ReturnsVoid && x.Parameters.Length == 0);
			if (switchMethod is null || checkMethod is null)
				return;

			startContext.RegisterSyntaxNodeAction(c => AnalyzeStatement(c, switchMethod, checkMethod), SyntaxKind.ExpressionStatement);
		});
	}

	public const string DiagnosticId = "FL0028";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

	private static void AnalyzeStatement(SyntaxNodeAnalysisContext context, IMethodSymbol switchMethod, IMethodSymbol checkMethod)
	{
		var statement = (ExpressionStatementSyntax) context.Node;
		if (statement.Expression is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "ThrowIfCanceled" } } ||
			statement.ContainsDiagnostics ||
			GetPreviousStatement(statement) is not ExpressionStatementSyntax previous || previous.ContainsDiagnostics)
			return;

		var model = context.SemanticModel;
		if (model.GetOperation(statement.Expression, context.CancellationToken) is not IInvocationOperation check ||
			!SymbolEqualityComparer.Default.Equals(check.TargetMethod, checkMethod) ||
			model.GetOperation(previous.Expression, context.CancellationToken) is not IInvocationOperation switchCall ||
			!SymbolEqualityComparer.Default.Equals(switchCall.TargetMethod, switchMethod))
			return;

		var receiver = GetReceiverSymbol(check.Instance);
		if (receiver is null || !SymbolEqualityComparer.Default.Equals(receiver, GetReceiverSymbol(switchCall.Instance)))
			return;

		var span = TextSpan.FromBounds(previous.SpanStart, statement.Span.End);
		if (statement.SyntaxTree.GetRoot(context.CancellationToken).DescendantTrivia(span).Any(x => x.IsDirective))
			return;

		// Switch retains its receiver before awaiting its argument; the next statement reads the binding again.
		var flow = model.AnalyzeDataFlow(previous, statement);
		if (flow is not { Succeeded: true } ||
			flow.Captured.Contains(receiver, SymbolEqualityComparer.Default) ||
			flow.WrittenInside.Contains(receiver, SymbolEqualityComparer.Default) ||
			!SymbolEqualityComparer.Default.Equals(receiver.ContainingSymbol, model.GetEnclosingSymbol(statement.SpanStart, context.CancellationToken)) ||
			HasWriteOrAlias(context, receiver))
			return;

		context.ReportDiagnostic(Diagnostic.Create(s_rule, statement.Expression.GetLocation()));
	}

	private static StatementSyntax? GetPreviousStatement(ExpressionStatementSyntax statement)
	{
		if (statement.Parent is GlobalStatementSyntax { Parent: CompilationUnitSyntax unit } global)
		{
			var memberIndex = unit.Members.IndexOf(global);
			return memberIndex > 0 && unit.Members[memberIndex - 1] is GlobalStatementSyntax previous ? previous.Statement : null;
		}

		var statements = statement.Parent switch
		{
			BlockSyntax block => block.Statements,
			SwitchSectionSyntax section => section.Statements,
			_ => default,
		};
		var index = statements.IndexOf(statement);
		return index > 0 ? statements[index - 1] : null;
	}

	private static ISymbol? GetReceiverSymbol(IOperation? operation)
	{
		while (true)
		{
			switch (operation)
			{
				case IParenthesizedOperation parentheses:
					operation = parentheses.Operand;
					break;
				case IConversionOperation { IsImplicit: true } conversion:
					operation = conversion.Operand;
					break;
				case ILocalReferenceOperation { Local.RefKind: RefKind.None } local:
					return local.Local;
				case IParameterReferenceOperation { Parameter.RefKind: RefKind.None } parameter:
					return parameter.Parameter;
				default:
					return null;
			}
		}
	}

	private static bool HasWriteOrAlias(SyntaxNodeAnalysisContext context, ISymbol receiver)
	{
		var scope = receiver.ContainingSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(context.CancellationToken) ??
			context.Node.SyntaxTree.GetRoot(context.CancellationToken);
		foreach (var reference in scope.DescendantNodes().OfType<IdentifierNameSyntax>())
		{
			if (reference.Identifier.ValueText != receiver.Name ||
				!SymbolEqualityComparer.Default.Equals(context.SemanticModel.GetSymbolInfo(reference, context.CancellationToken).Symbol, receiver))
				continue;

			foreach (var ancestor in reference.Ancestors())
			{
				if (ancestor is RefExpressionSyntax or ArgumentSyntax { RefKindKeyword.RawKind: not 0 } ||
					ancestor is AssignmentExpressionSyntax assignment && assignment.Left.Span.Contains(reference.Span))
					return true;

				if (ancestor is StatementSyntax)
					break;
			}
		}

		return false;
	}

	private static readonly DiagnosticDescriptor s_rule = new(
		id: DiagnosticId,
		title: "Avoid repeating the cancellation check immediately after Switch",
		messageFormat: "Switch already checks cancellation on this context; avoid an immediate repeated ThrowIfCanceled call",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "AsyncMethodContext.Switch checks cancellation after the await resumes. Avoid a second checkpoint immediately afterward unless its distinct cancellation timing is intentional.",
		helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/blob/-/docs/{DiagnosticId}.md");
}
