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
				Locations = new[] { new DiagnosticResultLocation("Test0.cs", 24, 17) },
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
	public interface IWorkState {}
	public sealed class AsyncWorkItem
	{
		public static AsyncWorkItem Current
		{
			get { throw new NotImplementedException(); }
		}
	}
}
";
	}
}
