using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests;

[TestFixture]
internal sealed class ExpressionBodiedMemberArrowTests : CodeFixVerifier
{
	[Test]
	public void ValidExpressionBodiedMethod()
	{
		const string validProgram = """
			class Test
			{
				public int GetValue() => 1;
			}
			""";
		VerifyCSharpDiagnostic(validProgram);
	}

	[Test]
	public void ValidExpressionBodiedProperty()
	{
		const string validProgram = """
			class Test
			{
				public int Value => 1;
			}
			""";
		VerifyCSharpDiagnostic(validProgram);
	}

	[Test]
	public void ValidLambdaExpression()
	{
		const string validProgram = """
			using System;

			class Test
			{
				private readonly Func<int, int> increment = x
					=> x + 1;
			}
			""";
		VerifyCSharpDiagnostic(validProgram);
	}

	[Test]
	public void InvalidExpressionBodiedMethod()
	{
		const string invalidProgram = """
			class Test
			{
				public int GetValue()
					=> 1;
			}
			""";
		var expected = new DiagnosticResult
		{
			Id = ExpressionBodiedMemberArrowAnalyzer.DiagnosticId,
			Message = "Move => to the end of the previous line",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 4, 3)],
		};

		VerifyCSharpDiagnostic(invalidProgram, expected);

		const string fixedProgram = """
			class Test
			{
				public int GetValue() =>
					1;
			}
			""";

		VerifyCSharpFix(invalidProgram, fixedProgram, 0);
	}

	[Test]
	public void InvalidExpressionBodiedMethodWithConstraint()
	{
		const string invalidProgram = """
			class Test
			{
				public T GetValue<T>() where T : new()
					=> new T();
			}
			""";
		var expected = new DiagnosticResult
		{
			Id = ExpressionBodiedMemberArrowAnalyzer.DiagnosticId,
			Message = "Move => to the end of the previous line",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 4, 3)],
		};

		VerifyCSharpDiagnostic(invalidProgram, expected);

		const string fixedProgram = """
			class Test
			{
				public T GetValue<T>() where T : new() =>
					new T();
			}
			""";

		VerifyCSharpFix(invalidProgram, fixedProgram, 0);
	}

	[Test]
	public void InvalidExpressionBodiedMethodWithCommentAfterArrow()
	{
		const string invalidProgram = """
			class Test
			{
				public int GetValue()
					=> // keep this comment
					1;
			}
			""";
		var expected = new DiagnosticResult
		{
			Id = ExpressionBodiedMemberArrowAnalyzer.DiagnosticId,
			Message = "Move => to the end of the previous line",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 4, 3)],
		};

		VerifyCSharpDiagnostic(invalidProgram, expected);

		const string fixedProgram = """
			class Test
			{
				public int GetValue() => // keep this comment
					1;
			}
			""";

		VerifyCSharpFix(invalidProgram, fixedProgram, 0);
	}

	[Test]
	public void InvalidExpressionBodiedConstructor()
	{
		const string invalidProgram = """
			class Test
			{
				public int Value { get; }

				public Test()
					=> Value = 1;
			}
			""";
		var expected = new DiagnosticResult
		{
			Id = ExpressionBodiedMemberArrowAnalyzer.DiagnosticId,
			Message = "Move => to the end of the previous line",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 6, 3)],
		};

		VerifyCSharpDiagnostic(invalidProgram, expected);

		const string fixedProgram = """
			class Test
			{
				public int Value { get; }

				public Test() =>
					Value = 1;
			}
			""";

		VerifyCSharpFix(invalidProgram, fixedProgram, 0);
	}

	[Test]
	public void InvalidExpressionBodiedStaticConstructor()
	{
		const string invalidProgram = """
			class Test
			{
				public static int Value { get; }

				static Test()
					=> Value = 1;
			}
			""";
		var expected = new DiagnosticResult
		{
			Id = ExpressionBodiedMemberArrowAnalyzer.DiagnosticId,
			Message = "Move => to the end of the previous line",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 6, 3)],
		};

		VerifyCSharpDiagnostic(invalidProgram, expected);

		const string fixedProgram = """
			class Test
			{
				public static int Value { get; }

				static Test() =>
					Value = 1;
			}
			""";

		VerifyCSharpFix(invalidProgram, fixedProgram, 0);
	}

	[Test]
	public void InvalidExpressionBodiedProperty()
	{
		const string invalidProgram = """
			class Test
			{
				public int Value
					=> 1;
			}
			""";
		var expected = new DiagnosticResult
		{
			Id = ExpressionBodiedMemberArrowAnalyzer.DiagnosticId,
			Message = "Move => to the end of the previous line",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 4, 3)],
		};

		VerifyCSharpDiagnostic(invalidProgram, expected);

		const string fixedProgram = """
			class Test
			{
				public int Value =>
					1;
			}
			""";

		VerifyCSharpFix(invalidProgram, fixedProgram, 0);
	}

	[Test]
	public void InvalidExpressionBodiedIndexer()
	{
		const string invalidProgram = """
			class Test
			{
				public int this[int index]
					=> index;
			}
			""";
		var expected = new DiagnosticResult
		{
			Id = ExpressionBodiedMemberArrowAnalyzer.DiagnosticId,
			Message = "Move => to the end of the previous line",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 4, 3)],
		};

		VerifyCSharpDiagnostic(invalidProgram, expected);

		const string fixedProgram = """
			class Test
			{
				public int this[int index] =>
					index;
			}
			""";

		VerifyCSharpFix(invalidProgram, fixedProgram, 0);
	}

	[Test]
	public void InvalidExpressionBodiedAccessor()
	{
		const string invalidProgram = """
			class Test
			{
				public int Value
				{
					get
						=> 1;
				}
			}
			""";
		var expected = new DiagnosticResult
		{
			Id = ExpressionBodiedMemberArrowAnalyzer.DiagnosticId,
			Message = "Move => to the end of the previous line",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 6, 4)],
		};

		VerifyCSharpDiagnostic(invalidProgram, expected);

		const string fixedProgram = """
			class Test
			{
				public int Value
				{
					get =>
						1;
				}
			}
			""";

		VerifyCSharpFix(invalidProgram, fixedProgram, 0);
	}

	[Test]
	public void InvalidExpressionBodiedMethodsFixAll()
	{
		const string invalidProgram = """
			class Test
			{
				public int GetFirst()
					=> 1;

				public int GetSecond()
					=> 2;
			}
			""";

		const string fixedProgram = """
			class Test
			{
				public int GetFirst() =>
					1;

				public int GetSecond() =>
					2;
			}
			""";

		VerifyCSharpFix(invalidProgram, fixedProgram);
	}

	protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new ExpressionBodiedMemberArrowAnalyzer();

	protected override CodeFixProvider GetCSharpCodeFixProvider() => new ExpressionBodiedMemberArrowCodeFixProvider();
}
