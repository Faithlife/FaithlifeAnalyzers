using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests;

[TestFixture]
internal sealed class RedundantCancellationCheckTests : DiagnosticVerifier
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

	protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new RedundantCancellationCheckAnalyzer();

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
