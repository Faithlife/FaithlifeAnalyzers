using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests;

[TestFixture]
internal sealed class SelfAnalysisTests
{
	[Test]
	public async Task CheckFaithlifeAnalyzers()
	{
		const string projectName = "Faithlife.Analyzers";
		var projectId = ProjectId.CreateNewId(debugName: projectName);

		var workspace = new AdhocWorkspace();
		var solution = workspace
			.CurrentSolution
			.AddProject(projectId, projectName, projectName, LanguageNames.CSharp)
			.WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
			.AddMetadataReferences(projectId, s_metadataReferences);

		foreach (var source in Directory.GetFiles("../../../../../src/Faithlife.Analyzers", "*.cs").Concat(Directory.GetFiles("../../../../../tests/Faithlife.Analyzers.Tests", "*.cs")))
		{
			var fileName = Path.GetFileName(source);
			var documentId = DocumentId.CreateNewId(projectId, debugName: fileName);
			solution = solution.AddDocument(documentId, fileName, SourceText.From(File.OpenRead(source)), filePath: source);
		}

		var project = solution.GetProject(projectId)!;
		var compilation = await project.GetCompilationAsync().ConfigureAwait(false);
		var compilationWithAnalyzers = compilation!.WithAnalyzers([.. typeof(CurrentAsyncWorkItemAnalyzer)
			.Assembly
			.GetTypes()
			.Where(x => x.BaseType == typeof(DiagnosticAnalyzer))
			.Select(Activator.CreateInstance)
			.Cast<DiagnosticAnalyzer>()]);

		var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
		Assert.That(diagnostics, Is.Empty);
	}

	private static readonly IReadOnlyList<string> s_assemblyReferences =
	[
		"System.Collections",
		"System.Collections.Immutable",
		"System.Composition.AttributedModel",
		"System.Linq",
		"System.Private.CoreLib",
		"System.Runtime",
		"Microsoft.CodeAnalysis",
		"Microsoft.CodeAnalysis.CSharp",
		"Microsoft.CodeAnalysis.Workspaces",
	];

	private static readonly IReadOnlyList<MetadataReference> s_metadataReferences = s_assemblyReferences
		.Select(x => (MetadataReference) MetadataReference.CreateFromFile(Assembly.Load(x).Location)).ToList();
}
