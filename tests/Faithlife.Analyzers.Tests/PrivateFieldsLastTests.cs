using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests;

[TestFixture]
internal sealed class PrivateFieldsLastTests : CodeFixVerifier
{
	[Test]
	public void ValidWhenPrivateFieldsAreLast()
	{
		const string validProgram = """
			#pragma warning disable 0169, 0414

			namespace TestApplication
			{
				public class TestClass
				{
					public int Value => _value;

					private readonly int _value;
				}
			}
			""";

		VerifyCSharpDiagnostic(validProgram);
	}

	[Test]
	public void ValidWhenAllMembersArePrivateFields()
	{
		const string validProgram = """
			#pragma warning disable 0169, 0414

			namespace TestApplication
			{
				public class TestClass
				{
					private readonly int _first;
					int _second;
				}
			}
			""";

		VerifyCSharpDiagnostic(validProgram);
	}

	[Test]
	public void InvalidWhenTypeStartsWithPrivateFields()
	{
		const string invalidProgram = """
			#pragma warning disable 0169, 0414

			namespace TestApplication
			{
				public class TestClass
				{
					private readonly int _first;
					private readonly int _second;

					public int Sum() => _first + _second;
				}
			}
			""";
		var expected = new DiagnosticResult
		{
			Id = PrivateFieldsLastAnalyzer.DiagnosticId,
			Message = "Move private fields to the end of the type",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 7, 3)],
		};

		VerifyCSharpDiagnostic(invalidProgram, expected);

		const string fixedProgram = """
			#pragma warning disable 0169, 0414

			namespace TestApplication
			{
				public class TestClass
				{
					public int Sum() => _first + _second;
					private readonly int _first;
					private readonly int _second;
				}
			}
			""";

		VerifyCSharpFix(invalidProgram, fixedProgram);
	}

	[Test]
	public void InvalidWhenImplicitlyPrivateFieldsAreFirst()
	{
		const string invalidProgram = """
			#pragma warning disable 0169, 0414

			namespace TestApplication
			{
				public class TestClass
				{
					int _first;

					public int Value => _first;
				}
			}
			""";
		var expected = new DiagnosticResult
		{
			Id = PrivateFieldsLastAnalyzer.DiagnosticId,
			Message = "Move private fields to the end of the type",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 7, 3)],
		};

		VerifyCSharpDiagnostic(invalidProgram, expected);

		const string fixedProgram = """
			#pragma warning disable 0169, 0414

			namespace TestApplication
			{
				public class TestClass
				{
					public int Value => _first;
					int _first;
				}
			}
			""";

		VerifyCSharpFix(invalidProgram, fixedProgram);
	}

	protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new PrivateFieldsLastAnalyzer();

	protected override CodeFixProvider GetCSharpCodeFixProvider() => new PrivateFieldsLastCodeFixProvider();
}
