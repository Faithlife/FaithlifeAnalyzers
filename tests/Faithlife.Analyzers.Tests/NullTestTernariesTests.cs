using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests
{
	[TestFixture]
	public class NullTestTernariesTests : CodeFixVerifier
	{
		[TestCase("Person person = null;", "var firstName = person?.FirstName;")]
		[TestCase("var person = new Person { FirstName = \"Bob\", LastName = \"Dole\" };", "var firstName = person.Age?.BirthYear;")]
		public void ValidUsage(string declaration, string ternary)
		{
			string validProgram = $@"
namespace TestApplication
{{
	public class Person
	{{
		public string FirstName {{ get; set; }}
		public string LastName {{ get; set; }}
		public Age? Age {{ get; set; }}
	}}

	public struct Age
	{{
		public int? InYears {{ get; set; }}

		public int? BirthYear 
		{{
			get
			{{
				return System.DateTime.Now.Year - InYears;
			}}
		}}
	}}

	internal static class TestClass
	{{
		public static void UtilityMethod()
		{{
			{declaration}
			{ternary}
		}}
	}}
}}";

			VerifyCSharpDiagnostic(validProgram);
		}

		[TestCase("var firstName = person != null ? person.FirstName : null;")]
		[TestCase("var firstName = person == null ? null : person.FirstName;")]
		[TestCase("var firstName = person.Age.HasValue ? person.Age.Value.BirthYear : null;")]
		[TestCase("var firstName = !person.Age.HasValue ? null : person.Age.Value.BirthYear;")]
		public void InvalidUsage(string badExample)
		{
			var brokenProgram = $@"
namespace TestApplication
{{
	public class Person
	{{
		public string FirstName {{ get; set; }}
		public string LastName {{ get; set; }}
		public Age? Age {{ get; set; }}
	}}

	public struct Age
	{{
		public int? InYears {{ get; set; }}

		public int? BirthYear 
		{{
			get
			{{
				return System.DateTime.Now.Year - InYears;
			}}
		}}
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
				Locations = new[] { new DiagnosticResultLocation("Test0.cs", 29, 20) },
			};

			VerifyCSharpDiagnostic(brokenProgram, expected);
		}

		protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new NullTestTernariesAnalyzer();
	}
}
