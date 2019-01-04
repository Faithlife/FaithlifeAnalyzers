using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests
{
	[TestFixture]
	public class UntilCanceledTests : CodeFixVerifier
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
			foreach (var x in new int[0].UntilCanceled())
			{
			}

			yield break;
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
			foreach (var x in new int[0].UntilCanceled())
			{
			}
		}
	}
}
";

			var expected = new DiagnosticResult
			{
				Id = UntilCanceledAnalyzer.DiagnosticId,
				Message = "UntilCanceled() may only be used in methods that return IEnumerable<AsyncAction>.",
				Severity = DiagnosticSeverity.Warning,
				Locations = new[] { new DiagnosticResultLocation("Test0.cs", c_preambleLength + 7, 46) },
			};

			VerifyCSharpDiagnostic(brokenProgram, expected);

			const string firstFix = preamble + @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static void UtilityMethod(IWorkState ignored)
		{
			foreach (var x in new int[0].UntilCanceled(ignored))
			{
			}
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
			foreach (var x in new int[0].UntilCanceled(workState))
			{
			}
		}
	}
}
";
			VerifyCSharpFix(brokenProgram, secondFix, 1);
		}


		[Test]
		public void InvalidMultiLineUsage()
		{
			const string brokenProgram = preamble + @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static void UtilityMethod(IWorkState ignored)
		{
			foreach (var x in new int[0]
				.UntilCanceled()
				.ToList())
			{
			}
		}
	}
}
";

			var expected = new DiagnosticResult
			{
				Id = UntilCanceledAnalyzer.DiagnosticId,
				Message = "UntilCanceled() may only be used in methods that return IEnumerable<AsyncAction>.",
				Severity = DiagnosticSeverity.Warning,
				Locations = new[] { new DiagnosticResultLocation("Test0.cs", c_preambleLength + 8, 19) },
			};

			VerifyCSharpDiagnostic(brokenProgram, expected);

			const string firstFix = preamble + @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static void UtilityMethod(IWorkState ignored)
		{
			foreach (var x in new int[0]
				.UntilCanceled(ignored)
				.ToList())
			{
			}
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
			foreach (var x in new int[0]
				.UntilCanceled(workState)
				.ToList())
			{
			}
		}
	}
}
";
			VerifyCSharpFix(brokenProgram, secondFix, 1);
		}

		protected override CodeFixProvider GetCSharpCodeFixProvider() => new UntilCanceledCodeFixProvider();

		protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new UntilCanceledAnalyzer();

		private const string preamble = @"using System;
using System.Collections.Generic;
using System.Linq;
using Libronix.Utility.Threading;

namespace Libronix.Utility.Threading
{
	public sealed class AsyncAction {}
	public interface IWorkState {}
	public static class AsyncEnumerableUtility
	{
		public static IEnumerable<T> UntilCanceled<T>(this IEnumerable<T> seq) => throw new NotImplementedException();
		public static IEnumerable<T> UntilCanceled<T>(this IEnumerable<T> seq, IWorkState state) => throw new NotImplementedException();
	}
}
";

		static readonly int c_preambleLength = preamble.Split('\n').Length;
	}
}

