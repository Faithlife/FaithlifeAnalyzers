using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests;

[TestFixture]
internal sealed class RedundantCancellationCheckTests : CodeFixVerifier
{
	[TestCase("ToThreadPool()")]
	[TestCase("ToThreadPoolQueue()")]
	[TestCase("ToThreadPoolAfterDelay(TimeSpan.Zero)")]
	[TestCase("ToSyncContext()")]
	[TestCase("ToSyncContextWithPriority(ExecutionPriority.Normal)")]
	public void ReportsAdjacentCheck(string awaitable)
	{
		var source = CreateSource($"""
			context.Switch(await context.{awaitable});
			context.ThrowIfCanceled();
			""");
		VerifyCSharpDiagnostic(source, CreateDiagnostic(source, "context.ThrowIfCanceled()"));
	}

	[TestCase("""
		var local = context;
		local.Switch(await local.ToSyncContext());
		local.ThrowIfCanceled();
		""", "local.ThrowIfCanceled()")]
	[TestCase("""
		(context).Switch(await context.ToSyncContext());
		// No executable work between the checkpoints.

		(context).ThrowIfCanceled();
		""", "(context).ThrowIfCanceled()")]
	[TestCase("""
		context.Switch(await other.ToThreadPool());
		context.ThrowIfCanceled();
		""", "context.ThrowIfCanceled()")]
	[TestCase("""
		context.Switch(token);
		context.ThrowIfCanceled();
		""", "context.ThrowIfCanceled()")]
	[TestCase("""
		await RunNestedAsync(context);
		static async Task RunNestedAsync(ContextAlias nested)
		{
			nested.Switch(await nested.ToThreadPool());
			nested.ThrowIfCanceled();
		}
		""", "nested.ThrowIfCanceled()")]
	[TestCase("""
		switch (0)
		{
		case 0:
			context.Switch(await context.ToThreadPool());
			context.ThrowIfCanceled();
			break;
		}
		""", "context.ThrowIfCanceled()")]
	[TestCase("""
		Func<AsyncMethodContext, Task> run = async nested =>
		{
			nested.Switch(await nested.ToThreadPool());
			nested.ThrowIfCanceled();
		};
		await run(context);
		""", "nested.ThrowIfCanceled()")]
	public void ReportsStableBindings(string body, string expression)
	{
		var source = CreateSource(body);
		VerifyCSharpDiagnostic(source, CreateDiagnostic(source, expression));
	}

	[Test]
	public void ReportsTopLevelStatements()
	{
		var source = $$"""
			{{c_usings}}
			var context = new AsyncMethodContext();
			context.Switch(await context.ToThreadPool());
			context.ThrowIfCanceled();
			{{CreateApi(c_contextMembers)}}
			""";
		var document = CreateDocument(source);
		var project = document.Project.WithCompilationOptions(new CSharpCompilationOptions(OutputKind.ConsoleApplication));
		var diagnostics = GetSortedDiagnosticsFromDocuments(new RedundantCancellationCheckAnalyzer(), [project.GetDocument(document.Id)!]);
		Assert.That(diagnostics, Has.Length.EqualTo(1));
		AssertDiagnostic(diagnostics[0], "context.ThrowIfCanceled()");
	}

	[Test]
	public void ReportsExactCheckSpanAndDescriptor()
	{
		var document = CreateDocument(CreateSource("""
			context.Switch(await context.ToThreadPool());
			context.ThrowIfCanceled();
			"""));
		var diagnostics = GetSortedDiagnosticsFromDocuments(new RedundantCancellationCheckAnalyzer(), [document]);
		Assert.That(diagnostics, Has.Length.EqualTo(1));
		AssertDiagnostic(diagnostics[0], "context.ThrowIfCanceled()");
		Assert.That(diagnostics[0].Descriptor.IsEnabledByDefault, Is.True);
		Assert.That(diagnostics[0].Descriptor.HelpLinkUri, Is.EqualTo("https://github.com/Faithlife/FaithlifeAnalyzers/blob/-/docs/FL0028.md"));
	}

	[TestCase("""
		context.Switch(await context.ToSyncContext());
		GC.KeepAlive(context);
		context.ThrowIfCanceled();
		""")]
	[TestCase("""
		context.Switch(await context.ToSyncContext());
		await Task.Yield();
		context.ThrowIfCanceled();
		""")]
	[TestCase("""
		context.Switch(await context.ToSyncContext());
		var local = context;
		context.ThrowIfCanceled();
		GC.KeepAlive(local);
		""")]
	[TestCase("""
		context.Switch(await context.ToSyncContext());
		if (true)
			context.ThrowIfCanceled();
		""")]
	[TestCase("""
		{
			context.Switch(await context.ToSyncContext());
		}
		context.ThrowIfCanceled();
		""")]
	[TestCase("""
		context.Switch(await context.ToSyncContext());
		context = other;
		context.ThrowIfCanceled();
		""")]
	[TestCase("""
		context.Switch(await context.ToSyncContext());
		other.ThrowIfCanceled();
		""")]
	[TestCase("""
		var alias = context;
		context.Switch(await context.ToSyncContext());
		alias.ThrowIfCanceled();
		""")]
	[TestCase("""
		context.Switch(await context.ToSyncContext());
		{
			var local = other;
			local.ThrowIfCanceled();
		}
		""")]
	[TestCase("""
		context.Switch(await context.ToSyncContext());
		#if DEBUG
		#endif
		context.ThrowIfCanceled();
		""")]
	[TestCase("""
		context.Switch(await context.ToSyncContext());
		goto check;
		check:
		context.ThrowIfCanceled();
		""")]
	[TestCase("""
		context.ThrowIfCanceled();
		context.Switch(await context.ToThreadPool());
		""")]
	[TestCase("""
		context.Switch(await context.ToThreadPool());
		;
		context.ThrowIfCanceled();
		""")]
	public void IgnoresNonAdjacentOrDifferentBindings(string body) => VerifyCSharpDiagnostic(CreateSource(body));

	[TestCase("""
		context.Switch(await (context = other).ToThreadPool());
		context.ThrowIfCanceled();
		""")]
	[TestCase("""
		context = other;
		context.Switch(await context.ToThreadPool());
		context.ThrowIfCanceled();
		""")]
	[TestCase("""
		Action change = () => context = other;
		context.Switch(await context.ToThreadPool());
		context.ThrowIfCanceled();
		GC.KeepAlive(change);
		""")]
	[TestCase("""
		void Change() => context = other;
		context.Switch(await context.ToThreadPool());
		context.ThrowIfCanceled();
		Change();
		""")]
	[TestCase("""
		Func<Task> run = async () =>
		{
			context.Switch(await context.ToThreadPool());
			context.ThrowIfCanceled();
		};
		await run();
		""")]
	[TestCase("""
		context.Switch(await context.ToThreadPool());
		context.ThrowIfCanceled();
		void Change() => context = other;
		Change();
		""")]
	[TestCase("""
		Action read = () => GC.KeepAlive(context);
		context.Switch(await context.ToThreadPool());
		context.ThrowIfCanceled();
		GC.KeepAlive(read);
		""")]
	[TestCase("""
		context.Switch(await context.ToThreadPool());
		context.ThrowIfCanceled();
		context = other;
		GC.KeepAlive(context);
		""")]
	[TestCase("""
		var local = context;
		local = other;
		local.Switch(await local.ToThreadPool());
		local.ThrowIfCanceled();
		""")]
	public void IgnoresMutableOrCapturedBindings(string body) => VerifyCSharpDiagnostic(CreateSource(body));

	[TestCase("m_context")]
	[TestCase("CurrentContext")]
	[TestCase("GetContext()")]
	[TestCase("Contexts[0]")]
	[TestCase("DynamicContext")]
	[TestCase("((dynamic) context)")]
	public void IgnoresUnstableReceivers(string receiver)
	{
		var document = CreateDocument(CreateSource($"""
			{receiver}.Switch(await context.ToThreadPool());
			{receiver}.ThrowIfCanceled();
			""", """
			private static AsyncMethodContext m_context = new AsyncMethodContext();
			private static AsyncMethodContext CurrentContext => m_context;
			private static AsyncMethodContext GetContext() => m_context;
			private static AsyncMethodContext[] Contexts => new[] { m_context };
			private static dynamic DynamicContext => m_context;
			"""));
		var project = document.Project.AddMetadataReferences(
		[
			MetadataReference.CreateFromFile(typeof(Microsoft.CSharp.RuntimeBinder.Binder).Assembly.Location),
			MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.CallSite).Assembly.Location),
		]);
		Assert.That(GetSortedDiagnosticsFromDocuments(new RedundantCancellationCheckAnalyzer(), [project.GetDocument(document.Id)!]), Is.Empty);
	}

	[TestCase("context?.Switch(await context.ToThreadPool()); context?.ThrowIfCanceled();")]
	[TestCase("context.Switch(await context.ToThreadPool()); context?.ThrowIfCanceled();")]
	public void IgnoresConditionalAccess(string body) => VerifyCSharpDiagnostic(CreateSource(body));

	[TestCase("ref AsyncMethodContext context", "")]
	[TestCase("in AsyncMethodContext context", "")]
	[TestCase("out AsyncMethodContext context", "context = new AsyncMethodContext();")]
	[TestCase("AsyncMethodContext context", "ref var alias = ref context; GC.KeepAlive(alias);")]
	[TestCase("AsyncMethodContext context", "Save(ref context);")]
	[TestCase("AsyncMethodContext context", "Observe(in context);")]
	public void IgnoresRefBindings(string parameter, string setup)
	{
		VerifyCSharpDiagnostic($$"""
			{{c_usings}}
			internal static class Example
			{
				private static void Run({{parameter}}, SwitchToken token)
				{
					{{setup}}
					context.Switch(token);
					context.ThrowIfCanceled();
				}
				private static void Save(ref AsyncMethodContext context) { }
				private static void Observe(in AsyncMethodContext context) { }
			}
			{{CreateApi(c_contextMembers)}}
			""");
	}

	[Test]
	public void IgnoresRefLocalReceiver()
	{
		VerifyCSharpDiagnostic($$"""
			{{c_usings}}
			internal static class Example
			{
				private static void Run(AsyncMethodContext context, SwitchToken token)
				{
					ref var alias = ref context;
					alias.Switch(token);
					alias.ThrowIfCanceled();
				}
			}
			{{CreateApi(c_contextMembers)}}
			""");
	}

	[Test]
	public void HonorsSuppression() => VerifyCSharpDiagnostic(CreateSource("""
		#pragma warning disable FL0028
		context.Switch(await context.ToSyncContext());
		context.ThrowIfCanceled();
		"""));

	[TestCase("Example.g.cs", "")]
	[TestCase("Example.cs", "// <auto-generated/>\n")]
	public void IgnoresGeneratedCode(string path, string header)
	{
		var document = CreateDocument(header + CreateSource("""
			context.Switch(await context.ToThreadPool());
			context.ThrowIfCanceled();
			""")).WithFilePath(path);
		Assert.That(GetSortedDiagnosticsFromDocuments(new RedundantCancellationCheckAnalyzer(), [document]), Is.Empty);
	}

	[Test]
	public void IgnoresLookalikeMethods()
	{
		VerifyCSharpDiagnostic(CreateSource("""
			var lookalike = new OtherContext();
			lookalike.Switch(token);
			lookalike.ThrowIfCanceled();
			""", """
			private sealed class OtherContext
			{
				public void Switch(SwitchToken token) { }
				public void ThrowIfCanceled() { }
			}
			"""));
	}

	[TestCase("context.Switch(new object()); context.ThrowIfCanceled();")]
	[TestCase("context.Switch(token); context.ThrowIfCanceled(0);")]
	public void IgnoresLookalikeOverloads(string body)
	{
		VerifyCSharpDiagnostic(CreateSource(body, contextMembers: c_contextMembers + """

			public void Switch(object token) { }
			public void ThrowIfCanceled(int value) { }
			"""));
	}

	[TestCase("public void Switch(object token) { } public void ThrowIfCanceled() { }", "context.Switch(token); context.ThrowIfCanceled();")]
	[TestCase("public void Switch(SwitchToken token) { } public void ThrowIfCanceled(int value) { }", "context.Switch(token); context.ThrowIfCanceled(0);")]
	[TestCase("public static void Switch(SwitchToken token) { } public void ThrowIfCanceled() { }", "AsyncMethodContext.Switch(token); context.ThrowIfCanceled();")]
	[TestCase("public void Switch(ref SwitchToken token) { } public void ThrowIfCanceled() { }", "context.Switch(ref token); context.ThrowIfCanceled();")]
	[TestCase("public int Switch(SwitchToken token) => 0; public void ThrowIfCanceled() { }", "context.Switch(token); context.ThrowIfCanceled();")]
	[TestCase("public void Switch(SwitchToken token) { } public static void ThrowIfCanceled() { }", "context.Switch(token); AsyncMethodContext.ThrowIfCanceled();")]
	[TestCase("public void Switch(SwitchToken token) { } public int ThrowIfCanceled() => 0;", "context.Switch(token); context.ThrowIfCanceled();")]
	[TestCase("public void Switch<T>(SwitchToken token) { } public void ThrowIfCanceled() { }", "context.Switch<int>(token); context.ThrowIfCanceled();")]
	[TestCase("public void Switch(SwitchToken token) { } public void ThrowIfCanceled<T>() { }", "context.Switch(token); context.ThrowIfCanceled<int>();")]
	public void IgnoresIncompatibleApi(string members, string body) => VerifyCSharpDiagnostic(CreateSource(body, contextMembers: members));

	[Test]
	public void IgnoresMissingApi() => VerifyCSharpDiagnostic("internal sealed class Example { }");

	[TestCase("SwitchToken")]
	[TestCase("AsyncMethodContext")]
	public void IgnoresMissingApiType(string typeName) => VerifyCSharpDiagnostic($$"""
		namespace Libronix.Utility.Threading
		{
			public sealed class {{typeName}} { }
		}
		""");

	[Test]
	public async Task IgnoresAmbiguousApiTypes()
	{
		var first = await CreateReferenceAsync("First.Api").ConfigureAwait(false);
		var second = await CreateReferenceAsync("Second.Api").ConfigureAwait(false);
		var document = CreateDocument("internal sealed class Example { }");
		var project = document.Project.AddMetadataReferences([first, second]);
		Assert.That(GetSortedDiagnosticsFromDocuments(new RedundantCancellationCheckAnalyzer(), [project.GetDocument(document.Id)!]), Is.Empty);
	}

	[TestCase("context.Switch(await context.ToThreadPool(")]
	[TestCase("context.Switch(await context.ToThreadPool()); context.ThrowIfCanceled(")]
	public async Task HandlesIncompleteCode(string body)
	{
		var compilation = await CreateDocument(CreateSource(body)).Project.GetCompilationAsync().ConfigureAwait(false);
		Assert.That(compilation, Is.Not.Null);
		var diagnostics = await compilation!.WithAnalyzers([new RedundantCancellationCheckAnalyzer()]).GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
		Assert.That(diagnostics, Is.Empty);
	}

	[TestCase("context.Switch(token);\ncontext.ThrowIfCanceled();", "context.Switch(token);")]
	[TestCase("context.Switch(await other.ToThreadPool());\ncontext.ThrowIfCanceled();", "context.Switch(await other.ToThreadPool());")]
	[TestCase("context.Switch(token); context.ThrowIfCanceled(); GC.KeepAlive(context);", "context.Switch(token); GC.KeepAlive(context);")]
	[TestCase("context.Switch(token);\n(context).ThrowIfCanceled();", "context.Switch(token);")]
	[TestCase("var local = context;\nlocal.Switch(token);\nlocal.ThrowIfCanceled();", "var local = context;\nlocal.Switch(token);")]
	public void CodeFixRemovesOnlyRedundantStatement(string body, string expectedBody)
	{
		VerifyCSharpFix(CreateSource(body), CreateSource(expectedBody));
	}

	[TestCase("\n")]
	[TestCase("\r\n")]
	public void CodeFixPreservesLineEndings(string lineEnding)
	{
		var source = CreateSource("context.Switch(token);\ncontext.ThrowIfCanceled();").ReplaceLineEndings(lineEnding);
		var expected = CreateSource("context.Switch(token);").ReplaceLineEndings(lineEnding);
		VerifyCSharpFix(source, expected);
	}

	[TestCase("""
		context.Switch(token);
		// Keep the leading comment.
		context.ThrowIfCanceled(); // Keep the trailing comment.
		""", "// Keep the leading comment.", "// Keep the trailing comment.")]
	[TestCase("""
		context.Switch(token); // Keep the switch comment.
		context /* Keep the receiver comment. */.ThrowIfCanceled(/* Keep the argument comment. */);
		""", "// Keep the switch comment.", "/* Keep the receiver comment. */", "/* Keep the argument comment. */")]
	[TestCase("""
		context.Switch(token);
		context // Keep the internal line comment.
			.ThrowIfCanceled();
		""", "// Keep the internal line comment.")]
	[TestCase("""
		#region Keep the region
		context.Switch(token);
		context.ThrowIfCanceled();
		#endregion
		""", "#region Keep the region", "#endregion")]
	[TestCase("""
		#if true
		context.Switch(token);
		context.ThrowIfCanceled();
		#endif
		#pragma warning disable CS0168
		""", "#if true", "#endif", "#pragma warning disable CS0168")]
	public async Task CodeFixPreservesCommentsAndDirectives(string body, params string[] preservedText)
	{
		var document = await ApplyCodeFixAsync(CreateDocument(CreateSource(body))).ConfigureAwait(false);
		var text = (await document.GetTextAsync().ConfigureAwait(false)).ToString();
		foreach (var value in preservedText)
			Assert.That(text.Split(value, StringSplitOptions.None), Has.Length.EqualTo(2), value);
		Assert.That(text, Does.Not.Contain(".ThrowIfCanceled("));
		Assert.That(GetSortedDiagnosticsFromDocuments(new RedundantCancellationCheckAnalyzer(), [document]), Is.Empty);
	}

	[TestCase("""
		switch (0)
		{
		case 0:
			context.Switch(token);
			context.ThrowIfCanceled();
			break;
		}
		""")]
	[TestCase("""
		void Nested(AsyncMethodContext nested)
		{
			nested.Switch(token);
			nested.ThrowIfCanceled();
		}
		Nested(context);
		""")]
	public async Task CodeFixHandlesNestedStatementLists(string body)
	{
		var document = await ApplyCodeFixAsync(CreateDocument(CreateSource(body))).ConfigureAwait(false);
		var text = (await document.GetTextAsync().ConfigureAwait(false)).ToString();
		Assert.That(text, Does.Not.Contain(".ThrowIfCanceled("));
		Assert.That(text, Does.Contain(".Switch(token)"));
		Assert.That(GetSortedDiagnosticsFromDocuments(new RedundantCancellationCheckAnalyzer(), [document]), Is.Empty);
	}

	[Test]
	public async Task CodeFixRemovesTopLevelStatement()
	{
		var source = $$"""
			{{c_usings}}
			var context = new AsyncMethodContext();
			context.Switch(await context.ToThreadPool());
			context.ThrowIfCanceled();
			{{CreateApi(c_contextMembers)}}
			""";
		var original = CreateDocument(source);
		var project = original.Project.WithCompilationOptions(new CSharpCompilationOptions(OutputKind.ConsoleApplication));
		var document = await ApplyCodeFixAsync(project.GetDocument(original.Id)!).ConfigureAwait(false);
		Assert.That((await document.GetTextAsync().ConfigureAwait(false)).ToString(), Is.EqualTo(source.Replace("context.ThrowIfCanceled();\n", "", StringComparison.Ordinal)));
		Assert.That(GetSortedDiagnosticsFromDocuments(new RedundantCancellationCheckAnalyzer(), [document]), Is.Empty);
	}

	[TestCase(false)]
	[TestCase(true)]
	public async Task CodeFixRemovesLastTopLevelStatement(bool finalNewLine)
	{
		var source = c_usings + "\nvar context = new AsyncMethodContext();\ncontext.Switch(await context.ToThreadPool());\ncontext.ThrowIfCanceled();" + (finalNewLine ? "\n" : "");
		var original = CreateDocument(source);
		var api = original.Project.AddDocument("Api.cs", SourceText.From(c_usings + Environment.NewLine + CreateApi(c_contextMembers)));
		var project = api.Project.WithCompilationOptions(new CSharpCompilationOptions(OutputKind.ConsoleApplication));
		var document = await ApplyCodeFixAsync(project.GetDocument(original.Id)!).ConfigureAwait(false);
		var expected = source.Replace("context.ThrowIfCanceled();" + (finalNewLine ? "\n" : ""), "", StringComparison.Ordinal);
		Assert.That((await document.GetTextAsync().ConfigureAwait(false)).ToString(), Is.EqualTo(expected));
		Assert.That(GetSortedDiagnosticsFromDocuments(new RedundantCancellationCheckAnalyzer(), [document]), Is.Empty);
	}

	[TestCase(FixAllScope.Document)]
	[TestCase(FixAllScope.Project)]
	[TestCase(FixAllScope.Solution)]
	public async Task CodeFixSupportsFixAll(FixAllScope scope)
	{
		const string body = """
			context.Switch(token);
			context.ThrowIfCanceled(); // Keep the first checkpoint comment.
			context.Switch(token);
			context.ThrowIfCanceled(); // Keep the second checkpoint comment.
			""";
		var original = CreateDocument(CreateSource(body));
		var second = original.Project.AddDocument("Second.cs", SourceText.From(CreateSource(body)
			.Replace("class Example", "class SecondExample", StringComparison.Ordinal)
			.Replace(CreateApi(c_contextMembers), "", StringComparison.Ordinal)));
		var otherProjectId = ProjectId.CreateNewId();
		var otherDocumentId = DocumentId.CreateNewId(otherProjectId);
		var solution = second.Project.Solution
			.AddProject(otherProjectId, "OtherProject", "OtherProject", LanguageNames.CSharp)
			.WithProjectCompilationOptions(otherProjectId, original.Project.CompilationOptions!)
			.AddMetadataReferences(otherProjectId, original.Project.MetadataReferences)
			.AddDocument(otherDocumentId, "Third.cs", SourceText.From(CreateSource(body)));
		var documents = new[] { solution.GetDocument(original.Id)!, solution.GetDocument(second.Id)!, solution.GetDocument(otherDocumentId)! };
		var diagnostics = documents.ToDictionary(x => x.Id, x => GetSortedDiagnosticsFromDocuments(new RedundantCancellationCheckAnalyzer(), [x]).ToImmutableArray());
		Assert.That(diagnostics.Values.All(x => x.Length == 2), Is.True);
		var provider = new RedundantCancellationCheckCodeFixProvider();
		var context = new FixAllContext(documents[0], provider, scope, "remove-redundant-cancellation-check", provider.FixableDiagnosticIds,
			new FixAllDiagnostics(diagnostics), CancellationToken.None);
		var action = await provider.GetFixAllProvider().GetFixAsync(context).ConfigureAwait(false);
		Assert.That(action, Is.Not.Null);
		var changedSolution = (await action!.GetOperationsAsync(CancellationToken.None).ConfigureAwait(false)).OfType<ApplyChangesOperation>().Single().ChangedSolution;
		for (var index = 0; index < documents.Length; index++)
		{
			var document = changedSolution.GetDocument(documents[index].Id)!;
			var shouldFix = index == 0 || scope == FixAllScope.Solution || index == 1 && scope == FixAllScope.Project;
			Assert.That(GetSortedDiagnosticsFromDocuments(new RedundantCancellationCheckAnalyzer(), [document]), Has.Length.EqualTo(shouldFix ? 0 : 2));
			var text = (await document.GetTextAsync().ConfigureAwait(false)).ToString();
			Assert.That(text.Split(".Switch(token);", StringSplitOptions.None), Has.Length.EqualTo(3));
			Assert.That(text.Split(".ThrowIfCanceled();", StringSplitOptions.None), Has.Length.EqualTo(shouldFix ? 1 : 3));
			Assert.That(text.Split("// Keep the first checkpoint comment.", StringSplitOptions.None), Has.Length.EqualTo(2));
			Assert.That(text.Split("// Keep the second checkpoint comment.", StringSplitOptions.None), Has.Length.EqualTo(2));
		}
	}

	[TestCase("\n")]
	[TestCase("\r\n")]
	public async Task CodeFixPreservesCommentLineEndings(string lineEnding)
	{
		var source = CreateSource("""
			context.Switch(token);
			// Keep the leading comment.
			context /* Keep the internal comment. */.ThrowIfCanceled(); // Keep the trailing comment.
			""").ReplaceLineEndings(lineEnding);
		var document = await ApplyCodeFixAsync(CreateDocument(source)).ConfigureAwait(false);
		var text = (await document.GetTextAsync().ConfigureAwait(false)).ToString();
		Assert.That(text, Does.Contain("// Keep the leading comment."));
		Assert.That(text, Does.Contain("/* Keep the internal comment. */"));
		Assert.That(text, Does.Contain("// Keep the trailing comment."));
		Assert.That(text, Does.Not.Contain(".ThrowIfCanceled("));
		Assert.That(text.Replace(lineEnding, "", StringComparison.Ordinal), Does.Not.Contain("\r").And.Not.Contain("\n"));
		Assert.That(GetSortedDiagnosticsFromDocuments(new RedundantCancellationCheckAnalyzer(), [document]), Is.Empty);
	}

	protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new RedundantCancellationCheckAnalyzer();

	protected override CodeFixProvider GetCSharpCodeFixProvider() => new RedundantCancellationCheckCodeFixProvider();

	private static async Task<Document> ApplyCodeFixAsync(Document document)
	{
		var diagnostics = GetSortedDiagnosticsFromDocuments(new RedundantCancellationCheckAnalyzer(), [document]);
		Assert.That(diagnostics, Has.Length.EqualTo(1));
		var actions = new List<CodeAction>();
		var provider = new RedundantCancellationCheckCodeFixProvider();
		await provider.RegisterCodeFixesAsync(new CodeFixContext(document, diagnostics[0], (action, _) => actions.Add(action), CancellationToken.None)).ConfigureAwait(false);
		Assert.That(actions, Has.Count.EqualTo(1));
		Assert.That(actions[0].Title, Is.EqualTo("Remove redundant ThrowIfCanceled call"));
		Assert.That(actions[0].EquivalenceKey, Is.EqualTo("remove-redundant-cancellation-check"));
		var solution = (await actions[0].GetOperationsAsync(CancellationToken.None).ConfigureAwait(false)).OfType<ApplyChangesOperation>().Single().ChangedSolution;
		return solution.GetDocument(document.Id)!;
	}

	private sealed class FixAllDiagnostics(IReadOnlyDictionary<DocumentId, ImmutableArray<Diagnostic>> diagnostics) : FixAllContext.DiagnosticProvider
	{
		public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken) =>
			Task.FromResult<IEnumerable<Diagnostic>>(diagnostics[document.Id]);

		public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken) =>
			Task.FromResult(Enumerable.Empty<Diagnostic>());

		public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken) =>
			Task.FromResult(project.Documents.SelectMany(x => diagnostics[x.Id]));
	}

	private static string CreateSource(string body, string members = "", string? contextMembers = null) => $$"""
		{{c_usings}}
		internal static class Example
		{
			private static async Task Run(AsyncMethodContext context, AsyncMethodContext other, SwitchToken token)
			{
				{{body}}
				await Task.Yield();
			}
			{{members}}
		}
		{{CreateApi(contextMembers ?? c_contextMembers)}}
		""";

	private static string CreateApi(string members) => $$"""
		namespace Libronix.Utility.Threading
		{
			public sealed class SwitchToken { }
			public enum ExecutionPriority { Normal }
			public sealed class AsyncMethodContext
			{
				{{members}}
			}
		}
		""";

	private static DiagnosticResult CreateDiagnostic(string source, string expression)
	{
		var index = source.IndexOf(expression, StringComparison.Ordinal);
		Assert.That(index, Is.GreaterThanOrEqualTo(0));
		var position = SourceText.From(source).Lines.GetLinePosition(index);
		return new DiagnosticResult
		{
			Id = RedundantCancellationCheckAnalyzer.DiagnosticId,
			Message = c_message,
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", position.Line + 1, position.Character + 1)],
		};
	}

	private static void AssertDiagnostic(Diagnostic diagnostic, string expression)
	{
		Assert.That(diagnostic.Id, Is.EqualTo(RedundantCancellationCheckAnalyzer.DiagnosticId));
		Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Warning));
		Assert.That(diagnostic.GetMessage(CultureInfo.InvariantCulture), Is.EqualTo(c_message));
		Assert.That(diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan), Is.EqualTo(expression));
	}

	private static async Task<PortableExecutableReference> CreateReferenceAsync(string assemblyName)
	{
		var compilation = await CreateDocument(c_usings + Environment.NewLine + CreateApi(c_contextMembers)).Project.GetCompilationAsync().ConfigureAwait(false);
		using var stream = new MemoryStream();
		var result = compilation!.WithAssemblyName(assemblyName).Emit(stream);
		Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Diagnostics));
		return MetadataReference.CreateFromImage(stream.ToArray());
	}

	private const string c_message = "Switch already checks cancellation on this context; avoid an immediate repeated ThrowIfCanceled call";
	private const string c_usings = """
		using System;
		using System.Threading.Tasks;
		using Libronix.Utility.Threading;
		using ContextAlias = Libronix.Utility.Threading.AsyncMethodContext;
		""";
	private const string c_contextMembers = """
		public void Switch(SwitchToken token) { }
		public void ThrowIfCanceled() { }
		public Task<SwitchToken> ToThreadPool() => throw new NotImplementedException();
		public Task<SwitchToken> ToThreadPoolQueue() => throw new NotImplementedException();
		public Task<SwitchToken> ToThreadPoolAfterDelay(TimeSpan delay) => throw new NotImplementedException();
		public Task<SwitchToken> ToSyncContext() => throw new NotImplementedException();
		public Task<SwitchToken> ToSyncContextWithPriority(ExecutionPriority priority) => throw new NotImplementedException();
		""";
}
