using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Faithlife.Analyzers;

internal sealed class IfNotNullFixAllProvider : FixAllProvider
{
	private IfNotNullFixAllProvider()
	{
	}

	public static IfNotNullFixAllProvider Instance { get; } = new IfNotNullFixAllProvider();

	public override async Task<CodeAction?> GetFixAsync(FixAllContext fixAllContext)
	{
		IEnumerable<Diagnostic> allDiagnostics;
		IEnumerable<(Document Document, SyntaxTree SyntaxTree)> allSyntaxTrees;

		switch (fixAllContext.Scope)
		{
		case FixAllScope.Document:
			{
				allDiagnostics = await fixAllContext.GetDocumentDiagnosticsAsync(fixAllContext.Document).ConfigureAwait(false);
				allSyntaxTrees = [(fixAllContext.Document, await fixAllContext.Document.GetSyntaxTreeAsync(fixAllContext.CancellationToken).ConfigureAwait(false))];
				break;
			}

		case FixAllScope.Project:
			{
				allDiagnostics = await fixAllContext.GetAllDiagnosticsAsync(fixAllContext.Project).ConfigureAwait(false);
				allSyntaxTrees = await Task.WhenAll(
					fixAllContext.Project.Documents
						.Select(async document => (document, await document.GetSyntaxTreeAsync(fixAllContext.CancellationToken).ConfigureAwait(false))))
					.ConfigureAwait(false);

				break;
			}

		case FixAllScope.Solution:
			{
				var solutionDiagnostics = await Task.WhenAll(fixAllContext.Solution.Projects.Select(async project => await fixAllContext.GetAllDiagnosticsAsync(project).ConfigureAwait(false))).ConfigureAwait(false);
				allDiagnostics = solutionDiagnostics.SelectMany(x => x);
				allSyntaxTrees = await Task.WhenAll(fixAllContext.Solution.Projects
					.SelectMany(x => x.Documents)
					.Select(async document => (document, await document.GetSyntaxTreeAsync(fixAllContext.CancellationToken).ConfigureAwait(false))))
					.ConfigureAwait(false);

				break;
			}

		default:
			return null;
		}

		var documentsWithDiagnostics = allSyntaxTrees
			.GroupJoin(allDiagnostics, x => x.SyntaxTree, x => x.Location.SourceTree, (tuple, diagnostics) => (tuple.Document, Diagnostics: diagnostics.ToImmutableArray()))
			.Where(x => x.Diagnostics.Length != 0)
			.ToImmutableArray();

		// The fixes for individual documents can be calculated in parallel, but fixes within a document
		// need to be calculated serially, so fixes that introduce new variable declares don't choose
		// conflicting names.
		var updatedDocuments = await Task.WhenAll(
			documentsWithDiagnostics
				.Select(async x =>
					await Task.Run(
						async () => await GetDocumentFixAsync(fixAllContext, x.Document, x.Diagnostics).ConfigureAwait(false),
						fixAllContext.CancellationToken)
						.ConfigureAwait(false)))
			.ConfigureAwait(false);

		var updatedSolution = await updatedDocuments
			.Where(x => x is not null)
			.Aggregate(
				Task.FromResult(fixAllContext.Solution),
				async (solutionAsync, document) =>
					(await solutionAsync.ConfigureAwait(false))
						.WithDocumentText(
							document!.Id,
							await document.GetTextAsync(fixAllContext.CancellationToken).ConfigureAwait(false)))
			.ConfigureAwait(false);

		return CodeAction.Create("Update solution", token => Task.FromResult(updatedSolution));
	}

	private static async Task<Document?> GetDocumentFixAsync(FixAllContext fixAllContext, Document document, ImmutableArray<Diagnostic> initialDiagnostics)
	{
		var currentDocument = document;
		var diagnostics = initialDiagnostics;
		while (diagnostics.Length != 0)
		{
			CodeAction? firstFix = null;

			foreach (var diagnostic in diagnostics)
			{
				using (var innerCancellationTokenSource = new CancellationTokenSource())
				using (var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(fixAllContext.CancellationToken, innerCancellationTokenSource.Token))
				{
					void HandleCodeFix(CodeAction codeAction, ImmutableArray<Diagnostic> fixedDiagnostics)
					{
						firstFix = codeAction;
						innerCancellationTokenSource.Cancel();
					}

					await fixAllContext.CodeFixProvider.RegisterCodeFixesAsync(new CodeFixContext(currentDocument, diagnostic, HandleCodeFix, fixAllContext.CancellationToken)).ConfigureAwait(false);
				}

				if (firstFix is object)
					break;
			}

			if (firstFix is null)
				break;

			var operations = await firstFix.GetOperationsAsync(fixAllContext.CancellationToken).ConfigureAwait(false);

			var changedSolution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;

			currentDocument = changedSolution.GetDocument(document.Id);
			diagnostics = await fixAllContext.GetDocumentDiagnosticsAsync(currentDocument).ConfigureAwait(false);
		}

		if (currentDocument == document)
			return null;

		return currentDocument;
	}
}
