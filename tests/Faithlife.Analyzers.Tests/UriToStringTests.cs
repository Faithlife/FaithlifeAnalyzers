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
			var x = new Uri(""http://www.faithlife.com"").ToString();
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
				Locations = new[] { new DiagnosticResultLocation("Test0.cs", c_preambleLength + 7, 59) },
			};

			VerifyCSharpDiagnostic(brokenProgram, expected);
		}

		protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new UriToStringAnalyzer();

		private const string c_preamble = @"using System;
using System.Runtime.Serialization;
";

		private static readonly int c_preambleLength = c_preamble.Split('\n').Length;
	}
}
