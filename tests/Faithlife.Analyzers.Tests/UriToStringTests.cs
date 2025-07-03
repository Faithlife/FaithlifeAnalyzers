using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests;

[TestFixture]
public class UriToStringTests : CodeFixVerifier
{
	[Test]
	public void ValidUsage()
	{
		const string validProgram = @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static void UtilityMethod()
		{
			var x = new System.Uri(""http://www.faithlife.com"").AbsolutePath;
		}
	}
}";

		VerifyCSharpDiagnostic(validProgram);
	}

	[Test]
	public void InvalidUsage()
	{
		var brokenProgram = @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static void UtilityMethod()
		{
			var x = new System.Uri(""http://www.faithlife.com"").ToString();
		}
	}
}";

		var expected = new DiagnosticResult
		{
			Id = UriToStringAnalyzer.DiagnosticId,
			Message = "Do not use Uri.ToString()",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 8, 12)],
		};

		VerifyCSharpDiagnostic(brokenProgram, expected);
	}

	[Test]
	public void InvalidNullableUsage()
	{
		var brokenProgram = @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static void UtilityMethod(System.Uri uri)
		{
			var x = uri?.ToString();
		}
	}
}";

		var expected = new DiagnosticResult
		{
			Id = UriToStringAnalyzer.DiagnosticId,
			Message = "Do not use Uri.ToString()",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 8, 16)],
		};

		VerifyCSharpDiagnostic(brokenProgram, expected);
	}

	protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new UriToStringAnalyzer();
}
