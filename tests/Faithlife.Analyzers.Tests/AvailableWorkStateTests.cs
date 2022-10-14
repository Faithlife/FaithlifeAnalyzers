using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests;

[TestFixture]
public class AvailableWorkStateTests : CodeFixVerifier
{
	[Test]
	public void NoDiagnosticWhenNotUsed()
	{
		string validProgram = preamble + @"
namespace TestApplication
{
	internal class TestClass
	{
		public static IEnumerable<AsyncAction> Method(TestClass testClass)
		{
			testClass.Property = 1;
			yield break;
		}

		public int Property { get; set; }
	}
}";
		VerifyCSharpDiagnostic(validProgram);
	}

	[TestCase("None")]
	[TestCase("ToDo")]
	public void ValidUsage(string property)
	{
		string validProgram = preamble + @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static void UtilityMethod()
		{
			HelperMethod(WorkState." + property + @");
		}

		private static void HelperMethod(IWorkState workState)
		{
		}
	}
}";
		VerifyCSharpDiagnostic(validProgram);
	}

	[TestCase("None")]
	[TestCase("ToDo")]
	public void IgnoredCurrentWorkState(string property)
	{
		string brokenProgram = preamble + @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static IEnumerable<AsyncAction> Method()
		{
			HelperMethod(WorkState." + property + @");
			yield break;
		}

		private static void HelperMethod(IWorkState workState)
		{
		}
	}
}
";

		var expected = new DiagnosticResult
		{
			Id = AvailableWorkStateAnalyzer.DiagnosticId,
			Message = "WorkState.None and WorkState.ToDo must not be used when an IWorkState is available.",
			Severity = DiagnosticSeverity.Error,
			Locations = new[] { new DiagnosticResultLocation("Test0.cs", c_preambleLength + 7, 17) },
		};

		VerifyCSharpDiagnostic(brokenProgram, expected);

		const string firstFix = preamble + @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static IEnumerable<AsyncAction> Method()
		{
			HelperMethod(AsyncWorkItem.Current);
			yield break;
		}

		private static void HelperMethod(IWorkState workState)
		{
		}
	}
}
";

		VerifyCSharpFix(brokenProgram, firstFix, 0);
	}

	[TestCase("None")]
	[TestCase("ToDo")]
	public void IgnoredIWorkState(string property)
	{
		string brokenProgram = preamble + @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static void Method(IWorkState ignored)
		{
			HelperMethod(WorkState." + property + @");
		}

		private static void HelperMethod(IWorkState workState)
		{
		}
	}
}
";

		var expected = new DiagnosticResult
		{
			Id = AvailableWorkStateAnalyzer.DiagnosticId,
			Message = "WorkState.None and WorkState.ToDo must not be used when an IWorkState is available.",
			Severity = DiagnosticSeverity.Error,
			Locations = new[] { new DiagnosticResultLocation("Test0.cs", c_preambleLength + 7, 17) },
		};

		VerifyCSharpDiagnostic(brokenProgram, expected);

		const string firstFix = preamble + @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static void Method(IWorkState ignored)
		{
			HelperMethod(ignored);
		}

		private static void HelperMethod(IWorkState workState)
		{
		}
	}
}
";

		VerifyCSharpFix(brokenProgram, firstFix, 0);
	}

	[TestCase("None")]
	[TestCase("ToDo")]
	public void IgnoredConcreteWorkState(string property)
	{
		string brokenProgram = preamble + @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static void Method(ConcreteWorkState ignored)
		{
			HelperMethod(WorkState." + property + @");
		}

		private static void HelperMethod(IWorkState workState)
		{
		}
	}
}
";

		var expected = new DiagnosticResult
		{
			Id = AvailableWorkStateAnalyzer.DiagnosticId,
			Message = "WorkState.None and WorkState.ToDo must not be used when an IWorkState is available.",
			Severity = DiagnosticSeverity.Error,
			Locations = new[] { new DiagnosticResultLocation("Test0.cs", c_preambleLength + 7, 17) },
		};

		VerifyCSharpDiagnostic(brokenProgram, expected);

		const string firstFix = preamble + @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static void Method(ConcreteWorkState ignored)
		{
			HelperMethod(ignored);
		}

		private static void HelperMethod(IWorkState workState)
		{
		}
	}
}
";

		VerifyCSharpFix(brokenProgram, firstFix, 0);
	}

	[TestCase("None")]
	[TestCase("ToDo")]
	public void IgnoredCancellationToken(string property)
	{
		string brokenProgram = preamble + @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static void Method(CancellationToken ignored)
		{
			HelperMethod(WorkState." + property + @");
		}

		private static void HelperMethod(IWorkState workState)
		{
		}
	}
}
";

		var expected = new DiagnosticResult
		{
			Id = AvailableWorkStateAnalyzer.DiagnosticId,
			Message = "WorkState.None and WorkState.ToDo must not be used when an IWorkState is available.",
			Severity = DiagnosticSeverity.Error,
			Locations = new[] { new DiagnosticResultLocation("Test0.cs", c_preambleLength + 7, 17) },
		};

		VerifyCSharpDiagnostic(brokenProgram, expected);

		const string firstFix = preamble + @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static void Method(CancellationToken ignored)
		{
			HelperMethod(WorkState.FromCancellationToken(ignored));
		}

		private static void HelperMethod(IWorkState workState)
		{
		}
	}
}
";

		VerifyCSharpFix(brokenProgram, firstFix, 0);
	}

	[TestCase("None")]
	[TestCase("ToDo")]
	public void IgnoredAsyncMethodContext(string property)
	{
		string brokenProgram = preamble + @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static void Method(AsyncMethodContext ignored)
		{
			HelperMethod(WorkState." + property + @");
		}

		private static void HelperMethod(IWorkState workState)
		{
		}
	}
}
";

		var expected = new DiagnosticResult
		{
			Id = AvailableWorkStateAnalyzer.DiagnosticId,
			Message = "WorkState.None and WorkState.ToDo must not be used when an IWorkState is available.",
			Severity = DiagnosticSeverity.Error,
			Locations = new[] { new DiagnosticResultLocation("Test0.cs", c_preambleLength + 7, 17) },
		};

		VerifyCSharpDiagnostic(brokenProgram, expected);

		const string firstFix = preamble + @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static void Method(AsyncMethodContext ignored)
		{
			HelperMethod(ignored.WorkState);
		}

		private static void HelperMethod(IWorkState workState)
		{
		}
	}
}
";

		VerifyCSharpFix(brokenProgram, firstFix, 0);
	}

	[TestCase("None")]
	[TestCase("ToDo")]
	public void MultipleOptions(string property)
	{
		string CreateProgramWithParameter(string expectedParameter) =>
			preamble + @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static IEnumerable<AsyncAction> Method(IWorkState ignored1, ConcreteWorkState ignored2, CancellationToken ignored3, AsyncMethodContext ignored4)
		{
			HelperMethod(" + expectedParameter + @");
			yield break;
		}

		private static void HelperMethod(IWorkState workState)
		{
		}
	}
}
";

		string brokenProgram = CreateProgramWithParameter($"WorkState.{property}");

		var expected = new DiagnosticResult
		{
			Id = AvailableWorkStateAnalyzer.DiagnosticId,
			Message = "WorkState.None and WorkState.ToDo must not be used when an IWorkState is available.",
			Severity = DiagnosticSeverity.Error,
			Locations = new[] { new DiagnosticResultLocation("Test0.cs", c_preambleLength + 7, 17) },
		};

		VerifyCSharpDiagnostic(brokenProgram, expected);
		VerifyCSharpFixes(
			brokenProgram,
			CreateProgramWithParameter("AsyncWorkItem.Current"),
			CreateProgramWithParameter("ignored1"),
			CreateProgramWithParameter("ignored2"),
			CreateProgramWithParameter("WorkState.FromCancellationToken(ignored3)"),
			CreateProgramWithParameter("ignored4.WorkState"));
	}

	protected override CodeFixProvider GetCSharpCodeFixProvider()
	{
		return new AvailableWorkStateCodeFixProvider();
	}

	protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
	{
		return new AvailableWorkStateAnalyzer();
	}

	private void VerifyCSharpFixes(string brokenProgram, params string[] fixedPrograms)
	{
		for (var currentFix = 0; currentFix < fixedPrograms.Length; currentFix++)
			VerifyCSharpFix(brokenProgram, fixedPrograms[currentFix], currentFix);
	}

	private const string preamble = @"using System;
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
";

	private static readonly int c_preambleLength = preamble.Split('\n').Length;
}
