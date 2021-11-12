using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests
{
	[TestFixture]
	public sealed class NumericConstantAssignmentTests : DiagnosticVerifier
	{
		[Test]
		public void ValidAssignments()
		{
			const string validProgram = @"
namespace TestApplication
{
	public class TestClass
	{
		private const int a = 14;
		const int b = 3;
		private const double c = 7.0;
		const double d = 14;
		const string s = ""Hello World"";
		const int m = 5 * 5 * 7;
		const int n = 24 - 7;
		const int p = 365 / 7;
		}
}";
			VerifyCSharpDiagnostic(validProgram);
		}

		[TestCase("private const int x", "24")]
		[TestCase("const int x", "7")]
		[TestCase("private const double x", "60")]
		[TestCase("const double x", "60 * 60")]
		[TestCase("private const int x", "60 * 60 * 24")]
		public void InvalidUsage(string declaration, string expression)
		{
			var brokenProgram = $@"
namespace TestApplication
{{
	internal static class TestClass
	{{
		{declaration} = {expression};
	}}
}}";
			VerifyCSharpDiagnostic(brokenProgram, new DiagnosticResult
			{
				Id = NumericConstantAssignmentAnalyzer.DiagnosticId,
				Message = "Avoid creating constants for date/time operations; consider using a library instead.",
				Severity = DiagnosticSeverity.Warning,
				Locations = new[] { new DiagnosticResultLocation("Test0.cs", 6, declaration.Length + 6) },
			});
		}

		protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new NumericConstantAssignmentAnalyzer();
	}
}
