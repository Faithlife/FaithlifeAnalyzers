using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests
{
	[TestFixture]
	public class UriToStringTests : CodeFixVerifier
	{
		[Test]
		public void ValidUsage()
		{
			const string validProgram = c_preamble + @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static void UtilityMethod()
		{
			var x = new Uri(""http://www.faithlife.com"").AbsolutePath;
		}
	}
}";

			VerifyCSharpDiagnostic(validProgram);
		}

		[Test]
		public void InvalidUsage()
		{
			var brokenProgram = c_preamble + @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static void UtilityMethod()
		{
			var x = new Uri(""http://www.faithlife.com"").ToString();
		}
	}
}";

			var expected = new DiagnosticResult
			{
				Id = UriToStringAnalyzer.DiagnosticId,
				Message = "Uri MAY NOT use .ToString()",
				Severity = DiagnosticSeverity.Warning,
				Locations = new[] { new DiagnosticResultLocation("Test0.cs", c_preambleLength + 7, 48) },
			};

			VerifyCSharpDiagnostic(brokenProgram, expected);
		}

		protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new UriToStringAnalyzer();

		private const string c_preamble = @"using System;
";

		private static readonly int c_preambleLength = c_preamble.Split('\n').Length;
	}
}
