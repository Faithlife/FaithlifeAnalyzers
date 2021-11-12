using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests
{
	[TestFixture]
	public class NullTestTernariesTests : CodeFixVerifier
	{
		[Test]
		public void ValidUsage()
		{
			const string validProgram = c_preamble + @"
namespace TestApplication
{
	public class Person
	{
		public string FirstName { get; set; }
		public string LastName { get; set; }
	}

	internal static class TestClass
	{
		public static void UtilityMethod()
		{
			Person person = null;
			var firstName = person?.FirstName;
		}
	}
}";

			VerifyCSharpDiagnostic(validProgram);
		}

		[TestCase("var firstName = person.Age != null ? person.FirstName : null;")]
		[TestCase("var firstName = person.Age == null ? null : person.FirstName;")]
		[TestCase("var firstName = person.Age.HasValue ? person.FirstName : null;")]
		public void InvalidUsage(string badExample)
		{
			var brokenProgram = c_preamble + $@"
namespace TestApplication
{{
	public class Person
	{{
		public string FirstName {{ get; set; }}
		public string LastName {{ get; set; }}
		public int? Age {{ get; set; }}
	}}

	internal static class TestClass
	{{
		public static void UtilityMethod()
		{{
			var person = new Person {{ FirstName = ""Bob"", LastName = ""Dole"" }};
			{badExample}
		}}
	}}
}}";

			var expected = new DiagnosticResult
			{
				Id = NullTestTernariesAnalyzer.DiagnosticId,
				Message = "Prefer null conditional operators over ternaries explicitly checking for null",
				Severity = DiagnosticSeverity.Warning,
				Locations = new[] { new DiagnosticResultLocation("Test0.cs", s_preambleLength + 15, 20) },
			};

			VerifyCSharpDiagnostic(brokenProgram, expected);
		}

		protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new NullTestTernariesAnalyzer();

		private const string c_preamble = @"";

		private static readonly int s_preambleLength = c_preamble.Split('\n').Length;
	}
}
