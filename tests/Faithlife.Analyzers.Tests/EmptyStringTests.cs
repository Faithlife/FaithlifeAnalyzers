using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests;

[TestFixture]
public sealed class EmptyStringTests : CodeFixVerifier
{
	[Test]
	public void ValidEmptyString()
	{
		const string validProgram = @"
namespace TestApplication
{
	public class TestClass
	{
		public TestClass()
		{
			Method("""");
		}

		private void Method(string parameter)
		{
		}
	}
}";
		VerifyCSharpDiagnostic(validProgram);
	}

	[TestCase("String.Empty")]
	[TestCase("string.Empty")]
	[TestCase("System.String.Empty")]
	public void InvalidEmptyString(string parameter)
	{
		string invalidProgram = @"using System;

namespace TestApplication
{
	public class TestClass
	{
		public TestClass()
		{
			Method(" + parameter + @");
		}

		private void Method(string parameter)
		{
		}
	}
}";
		var expected = new DiagnosticResult
		{
			Id = EmptyStringAnalyzer.DiagnosticId,
			Message = "Prefer \"\" over string.Empty.",
			Severity = DiagnosticSeverity.Warning,
			Locations = new[] { new DiagnosticResultLocation("Test0.cs", 9, 11) },
		};

		VerifyCSharpDiagnostic(invalidProgram, expected);

		const string fixedProgram = @"using System;

namespace TestApplication
{
	public class TestClass
	{
		public TestClass()
		{
			Method("""");
		}

		private void Method(string parameter)
		{
		}
	}
}";

		VerifyCSharpFix(invalidProgram, fixedProgram, 0);
	}

	protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new EmptyStringAnalyzer();

	protected override CodeFixProvider GetCSharpCodeFixProvider() => new EmptyStringCodeFixProvider();
}
