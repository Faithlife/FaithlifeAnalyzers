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
		public void ValidDollarSign()
		{
			const string validProgram = @"
namespace TestApplication
{
	public class TestClass
	{
		public TestClass()
		{
			string one = ""one"";
			string two = $""{one} costs $0.00"";
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
				Locations = new[] { new DiagnosticResultLocation("Test0.cs", 9, 20) },
			});
		}

		protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new StringAnalyzer();
	}
}
