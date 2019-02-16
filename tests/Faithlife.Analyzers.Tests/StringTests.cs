using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests
{
	[TestFixture]
	public class StringTests : CodeFixVerifier
	{
		[Test]
		public void ValidInterpolatedStrings()
		{
			const string validProgram = @"
namespace TestApplication
{
	public class TestClass
	{
		public TestClass()
		{
			string one = ""${hello}"";
			string two = $""{one}"";
		}
	}
}";
			VerifyCSharpDiagnostic(validProgram);
		}

		[Test]
		public void InvalidInterpolatedString()
		{
			const string invalidProgram = @"
namespace TestApplication
{
	public class TestClass
	{
		public TestClass()
		{
			string one = ""${hello}"";
			string two = $""${one}"";
		}
	}
}";
			VerifyCSharpDiagnostic(invalidProgram, new DiagnosticResult
			{
				Id = StringAnalyzer.DiagnosticId,
				Message = "Avoid using ${} in interpolated strings.",
				Severity = DiagnosticSeverity.Warning,
			});
		}

		protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new StringAnalyzer();
	}
}
