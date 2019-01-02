using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests
{
	[TestFixture]
	public class CurrentAsyncWorkItemTests : CodeFixVerifier
	{
		[Test]
		public void ValidUsage()
		{
			const string validProgram = preamble + @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static IEnumerable<AsyncAction> UtilityMethod(IWorkState ignored)
		{
			HelperMethod(AsyncWorkItem.Current);

			yield break;
		}

		private static void HelperMethod(IWorkState workState)
		{
		}
	}
}";
			VerifyCSharpDiagnostic(validProgram);
		}

		[Test]
		public void InvalidUsage()
		{
			const string brokenProgram = preamble + @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static void UtilityMethod(IWorkState ignored)
		{
			HelperMethod(AsyncWorkItem.Current);
		}

		private static void HelperMethod(IWorkState workState)
		{
		}
	}
}
";

			var expected = new DiagnosticResult
			{
				Id = CurrentAsyncWorkItemAnalyzer.DiagnosticId,
				Message = "AsyncWorkItem.Current must only be used in methods that return IEnumerable<AsyncAction>.",
				Severity = DiagnosticSeverity.Warning,
				Locations = new[] { new DiagnosticResultLocation("Test0.cs", 28, 17) },
			};

			VerifyCSharpDiagnostic(brokenProgram, expected);

			const string firstFix = preamble + @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static void UtilityMethod(IWorkState ignored)
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

			const string secondFix = preamble + @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static void UtilityMethod(IWorkState ignored, IWorkState workState)
		{
			HelperMethod(workState);
		}

		private static void HelperMethod(IWorkState workState)
		{
		}
	}
}
";
			VerifyCSharpFix(brokenProgram, secondFix, 1);
		}

		[Test]
		public void FixAsyncWorkItemCurrentCanceled()
		{
			const string brokenProgram = preamble + @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static void Method(IWorkState ignored)
		{
			if (AsyncWorkItem.Current.Canceled)
				return;
		}
	}
}
";

			var expected = new DiagnosticResult
			{
				Id = CurrentAsyncWorkItemAnalyzer.DiagnosticId,
				Message = "AsyncWorkItem.Current must only be used in methods that return IEnumerable<AsyncAction>.",
				Severity = DiagnosticSeverity.Warning,
				Locations = new[] { new DiagnosticResultLocation("Test0.cs", 28, 8) },
			};

			VerifyCSharpDiagnostic(brokenProgram, expected);

			const string firstFix = preamble + @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static void Method(IWorkState ignored)
		{
			if (ignored.Canceled)
				return;
		}
	}
}
";

			VerifyCSharpFix(brokenProgram, firstFix, 0);

			const string secondFix = preamble + @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static void Method(IWorkState ignored, IWorkState workState)
		{
			if (workState.Canceled)
				return;
		}
	}
}
";
			VerifyCSharpFix(brokenProgram, secondFix, 1);
		}

		[Test]
		public void GenerateUniqueName()
		{
			const string brokenProgram = preamble + @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static void Method(IWorkState workState, int workState1, string workState2)
		{
			var workState3 = new object();
			if (AsyncWorkItem.Current.Canceled)
				return;
		}
	}
}
";

			var expected = new DiagnosticResult
			{
				Id = CurrentAsyncWorkItemAnalyzer.DiagnosticId,
				Message = "AsyncWorkItem.Current must only be used in methods that return IEnumerable<AsyncAction>.",
				Severity = DiagnosticSeverity.Warning,
				Locations = new[] { new DiagnosticResultLocation("Test0.cs", 29, 8) },
			};

			VerifyCSharpDiagnostic(brokenProgram, expected);

			const string firstFix = preamble + @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static void Method(IWorkState workState, int workState1, string workState2)
		{
			var workState3 = new object();
			if (workState.Canceled)
				return;
		}
	}
}
";

			VerifyCSharpFix(brokenProgram, firstFix, 0);

			const string secondFix = preamble + @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static void Method(IWorkState workState, int workState1, string workState2, IWorkState workState4)
		{
			var workState3 = new object();
			if (workState4.Canceled)
				return;
		}
	}
}
";
			VerifyCSharpFix(brokenProgram, secondFix, 1);
		}

		protected override CodeFixProvider GetCSharpCodeFixProvider()
		{
			return new CurrentAsyncWorkItemCodeFixProvider();
		}

		protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
		{
			return new CurrentAsyncWorkItemAnalyzer();
		}

		private const string preamble = @"using System;
using System.Collections.Generic;
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
		public static AsyncWorkItem Current
		{
			get { throw new NotImplementedException(); }
		}
		public bool Canceled => false;
	}
}
";
	}
}
