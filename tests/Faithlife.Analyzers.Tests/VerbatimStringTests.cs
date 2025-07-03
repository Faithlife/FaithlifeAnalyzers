using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests;

[TestFixture]
internal sealed class VerbatimStringTests : DiagnosticVerifier
{
	[Test]
	public void ValidStrings()
	{
		const string validProgram = @"
namespace TestApplication
{
	public class TestClass
	{
		public TestClass()
		{
			@""Hello
World"".Trim();
			@""Hello\World"".Trim();
			""Hello World"".Trim();
			@""Hello """"wait for it"""" World"".Trim();
		}
	}
}";
		VerifyCSharpDiagnostic(validProgram);
	}

	[Test]
	public void UnnecessaryVerbatimString()
	{
		const string invalidProgram = @"
namespace TestApplication
{
	public class TestClass
	{
		public TestClass()
		{
			@""Hello World"".Trim();
		}
	}
}";
		VerifyCSharpDiagnostic(invalidProgram, new DiagnosticResult
		{
			Id = VerbatimStringAnalyzer.DiagnosticId,
			Message = "Avoid using verbatim string literals without special characters",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 8, 4)],
		});
	}

	[Test]
	public void EmptyString()
	{
		const string invalidProgram = @"
namespace TestApplication
{
	public class TestClass
	{
		public TestClass()
		{
			@"""".Trim();
		}
	}
}";
		VerifyCSharpDiagnostic(invalidProgram, new DiagnosticResult
		{
			Id = VerbatimStringAnalyzer.DiagnosticId,
			Message = "Avoid using verbatim string literals without special characters",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 8, 4)],
		});
	}

	protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new VerbatimStringAnalyzer();
}
