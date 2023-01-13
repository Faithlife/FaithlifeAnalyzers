using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests;

[TestFixture]
public sealed class FormatInvariantTests : CodeFixVerifier
{
	[Test]
	public void ValidFormat()
	{
		const string validProgram = @"
namespace TestApplication
{
	public class TestClass
	{
		public TestClass()
		{
			var foo = ""foo"";
			Method($""{foo}"");
		}

		private void Method(string parameter)
		{
		}
	}
}";
		VerifyCSharpDiagnostic(validProgram);
	}

	[TestCase(@"""pre {0} post"".FormatInvariant(foo)", @"$""pre {foo} post""")]
	[TestCase(@"""pre {0} mid {1} dup {0} format {1:D1} parens {2} alignment {1,-10} alignment+format {1,10:D1} post"".FormatInvariant(foo, 10, b ? 1 : 2)", @"FormattableString.Invariant($""pre {foo} mid {10} dup {foo} format {10:D1} parens {(b ? 1 : 2)} alignment {10,-10} alignment+format {10,10:D1} post"")")]
	[TestCase(@"""with quotes \""{0}\"" post"".FormatInvariant(foo)", @"$""with quotes \""{foo}\"" post""")]
	[TestCase(@"""with newline \n{0} post"".FormatInvariant(foo)", @"$""with newline \n{foo} post""")]
	public void InvalidFormat(string invalidCode, string fixedCode)
	{
		var invalidProgram = $@"using System;
using Libronix.Utility;

namespace Libronix.Utility
{{
	public static class StringUtility
	{{
		public static string FormatInvariant(this string format, params object[] args) => throw new NotImplementedException();
	}}
}}

namespace TestApplication
{{
	public class TestClass
	{{
		public TestClass(bool b)
		{{
			var foo = ""foo"";
			Method({invalidCode});
		}}

		private void Method(string parameter)
		{{
		}}
	}}
}}";
		var expected = new DiagnosticResult
		{
			Id = FormatInvariantAnalyzer.DiagnosticId,
			Message = "Prefer string interpolation over FormatInvariant",
			Severity = DiagnosticSeverity.Info,
			Locations = new[] { new DiagnosticResultLocation("Test0.cs", 19, 11) },
		};

		VerifyCSharpDiagnostic(invalidProgram, expected);

		var fixedProgram = $@"using System;

namespace Libronix.Utility
{{
	public static class StringUtility
	{{
		public static string FormatInvariant(this string format, params object[] args) => throw new NotImplementedException();
	}}
}}

namespace TestApplication
{{
	public class TestClass
	{{
		public TestClass(bool b)
		{{
			var foo = ""foo"";
			Method({fixedCode});
		}}

		private void Method(string parameter)
		{{
		}}
	}}
}}";

		VerifyCSharpFix(invalidProgram, fixedProgram, 0);
	}

	protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new FormatInvariantAnalyzer();

	protected override CodeFixProvider GetCSharpCodeFixProvider() => new FormatInvariantCodeFixProvider();
}
