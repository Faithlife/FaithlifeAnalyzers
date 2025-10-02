using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests;

[TestFixture]
internal sealed class AsyncMethodContextWorkStateTests : CodeFixVerifier
{
	[Test]
	public void NoDiagnosticWhenNotUsingFromCancellationToken()
	{
		string validProgram = $$"""
			{{c_preamble}}
			namespace TestApplication
			{
				internal class TestClass
				{
					public static void Method(AsyncMethodContext context)
					{
						var workState = context.WorkState;
					}
				}
			}
			""";
		VerifyCSharpDiagnostic(validProgram);
	}

	[Test]
	public void NoDiagnosticWhenUsingFromCancellationTokenWithNonContext()
	{
		string validProgram = $$"""
			{{c_preamble}}
			namespace TestApplication
			{
				internal class TestClass
				{
					public static void Method(CancellationToken token)
					{
						var workState = WorkState.FromCancellationToken(token);
					}
				}
			}
			""";
		VerifyCSharpDiagnostic(validProgram);
	}

	[Test]
	public void DiagnosticWhenUsingFromCancellationTokenWithContext()
	{
		string brokenProgram = $$"""
			{{c_preamble}}
			namespace TestApplication
			{
				internal class TestClass
				{
					public static void Method(AsyncMethodContext context)
					{
						var workState = WorkState.FromCancellationToken(context.CancellationToken);
					}
				}
			}
			""";

		var expected = new DiagnosticResult
		{
			Id = AsyncMethodContextWorkStateAnalyzer.DiagnosticId,
			Message = "Use AsyncMethodContext.WorkState",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", s_preambleLength + 7, 20)],
		};

		VerifyCSharpDiagnostic(brokenProgram, expected);
	}

	[Test]
	public void CodeFixReplacesWithContextWorkState()
	{
		string brokenProgram = $$"""
			{{c_preamble}}
			namespace TestApplication
			{
				internal class TestClass
				{
					public static void Method(AsyncMethodContext context)
					{
						var workState = WorkState.FromCancellationToken(context.CancellationToken);
					}
				}
			}
			""";

		string fixedProgram = $$"""
			{{c_preamble}}
			namespace TestApplication
			{
				internal class TestClass
				{
					public static void Method(AsyncMethodContext context)
					{
						var workState = context.WorkState;
					}
				}
			}
			""";

		VerifyCSharpFix(brokenProgram, fixedProgram);
	}

	[Test]
	public void CodeFixWorksWithDifferentVariableNames()
	{
		string brokenProgram = $$"""
			{{c_preamble}}
			namespace TestApplication
			{
				internal class TestClass
				{
					public static void Method(AsyncMethodContext asyncContext)
					{
						var workState = WorkState.FromCancellationToken(asyncContext.CancellationToken);
					}
				}
			}
			""";

		var expected = new DiagnosticResult
		{
			Id = AsyncMethodContextWorkStateAnalyzer.DiagnosticId,
			Message = "Use AsyncMethodContext.WorkState",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", s_preambleLength + 7, 20)],
		};

		VerifyCSharpDiagnostic(brokenProgram, expected);

		string fixedProgram = $$"""
			{{c_preamble}}
			namespace TestApplication
			{
				internal class TestClass
				{
					public static void Method(AsyncMethodContext asyncContext)
					{
						var workState = asyncContext.WorkState;
					}
				}
			}
			""";

		VerifyCSharpFix(brokenProgram, fixedProgram);
	}

	[Test]
	public void CodeFixWorksWithMethodCall()
	{
		var brokenProgram = $$"""
			{{c_preamble}}
			namespace TestApplication
			{
				internal class TestClass
				{
					public static void Method(AsyncMethodContext context)
					{
						Method2(WorkState.FromCancellationToken(context.CancellationToken));
					}
					public static bool Method2(IWorkState workState)
					{
						return workState.Canceled;
					}
				}
			}
			""";

		var expected = new DiagnosticResult
		{
			Id = AsyncMethodContextWorkStateAnalyzer.DiagnosticId,
			Message = "Use AsyncMethodContext.WorkState",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", s_preambleLength + 7, 12)],
		};

		VerifyCSharpDiagnostic(brokenProgram, expected);

		var fixedProgram = $$"""
			{{c_preamble}}
			namespace TestApplication
			{
				internal class TestClass
				{
					public static void Method(AsyncMethodContext context)
					{
						Method2(context.WorkState);
					}
					public static bool Method2(IWorkState workState)
					{
						return workState.Canceled;
					}
				}
			}
			""";

		VerifyCSharpFix(brokenProgram, fixedProgram);
	}

	[Test]
	public void CodeFixWorksWithCast()
	{
		var brokenProgram = $$"""
			{{c_preamble}}
			namespace TestApplication
			{
				internal class TestClass
				{
					public static void Method(AsyncMethodContext context)
					{
						var concrete = (ConcreteWorkState) WorkState.FromCancellationToken(context.CancellationToken);
					}
				}
			}
			""";

		var expected = new DiagnosticResult
		{
			Id = AsyncMethodContextWorkStateAnalyzer.DiagnosticId,
			Message = "Use AsyncMethodContext.WorkState",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", s_preambleLength + 7, 39)],
		};

		VerifyCSharpDiagnostic(brokenProgram, expected);

		var fixedProgram = $$"""
			{{c_preamble}}
			namespace TestApplication
			{
				internal class TestClass
				{
					public static void Method(AsyncMethodContext context)
					{
						var concrete = (ConcreteWorkState) context.WorkState;
					}
				}
			}
			""";

		VerifyCSharpFix(brokenProgram, fixedProgram);
	}

	[Test]
	public void CodeFixWorksWithPropertyAccess()
	{
		var brokenProgram = $$"""
			{{c_preamble}}
			namespace TestApplication
			{
				internal class TestClass
				{
					public static bool Method(AsyncMethodContext context)
					{
						return WorkState.FromCancellationToken(context.CancellationToken).Canceled;
					}
				}
			}
			""";

		var expected = new DiagnosticResult
		{
			Id = AsyncMethodContextWorkStateAnalyzer.DiagnosticId,
			Message = "Use AsyncMethodContext.WorkState",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", s_preambleLength + 7, 11)],
		};

		VerifyCSharpDiagnostic(brokenProgram, expected);

		var fixedProgram = $$"""
			{{c_preamble}}
			namespace TestApplication
			{
				internal class TestClass
				{
					public static bool Method(AsyncMethodContext context)
					{
						return context.WorkState.Canceled;
					}
				}
			}
			""";

		VerifyCSharpFix(brokenProgram, fixedProgram);
	}

	protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new AsyncMethodContextWorkStateAnalyzer();

	protected override CodeFixProvider GetCSharpCodeFixProvider() => new AsyncMethodContextWorkStateCodeFixProvider();

	private const string c_preamble = """
		using System;
		using System.Collections.Generic;
		using System.Threading;
		using Libronix.Utility.Threading;
		namespace Libronix.Utility.Threading
		{
			public sealed class AsyncAction {}
			public interface IWorkState
			{
				bool Canceled { get; }
			}
			public sealed class AsyncWorkItem : IWorkState
			{
				public static AsyncWorkItem Current => throw new NotImplementedException();
				public bool Canceled => false;
			}
			public sealed class AsyncMethodContext
			{
				public IWorkState WorkState => throw new NotImplementedException();
				public CancellationToken CancellationToken => throw new NotImplementedException();
			}
			public sealed class ConcreteWorkState : IWorkState
			{
				public bool Canceled => false;
			}
			public static class WorkState
			{
				public static IWorkState FromCancellationToken(CancellationToken token) => throw new NotImplementedException();
				public static IWorkState None => throw new NotImplementedException();
				public static IWorkState ToDo => throw new NotImplementedException();
			}
		}
		""";
	private static readonly int s_preambleLength = c_preamble.Split('\n').Length;
}
