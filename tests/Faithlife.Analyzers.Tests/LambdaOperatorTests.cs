using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests;

[TestFixture]
internal sealed class LambdaOperatorTests : CodeFixVerifier
{
	[Test]
	public void ValidExpressionBodiedMember()
	{
		const string validProgram = """
			class Test
			{
				public int GetValue() => 1;
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
				private readonly Func<int, int> increment = x => x + 1;
			}
			""";
		VerifyCSharpDiagnostic(validProgram);
	}

	[Test]
	public void ValidSwitchExpression()
	{
		const string validProgram = """
			class Test
			{
				public string GetValue(int value) => value switch
				{
					0 => "zero",
					_ => "other",
				};
			}
			""";
		VerifyCSharpDiagnostic(validProgram);
	}

	[Test]
	public void InvalidExpressionBodiedMember()
	{
		const string invalidProgram = """
			class Test
			{
				public int GetValue()
					=> 1;
			}
			""";
		var expected = Expected(4, 3);

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
	public void InvalidLambdaExpression()
	{
		const string invalidProgram = """
			using System;

			class Test
			{
				private readonly Func<int, int> increment = x
					=> x + 1;
			}
			""";
		var expected = Expected(6, 3);

		VerifyCSharpDiagnostic(invalidProgram, expected);

		const string fixedProgram = """
			using System;

			class Test
			{
				private readonly Func<int, int> increment = x =>
					x + 1;
			}
			""";

		VerifyCSharpFix(invalidProgram, fixedProgram, 0);
	}

	[Test]
	public void InvalidSwitchExpression()
	{
		const string invalidProgram = """
			class Test
			{
				public string GetValue(int value) => value switch
				{
					0
						=> "zero",
					_
						=> "other",
				};
			}
			""";
		var expected = new[] { Expected(6, 4), Expected(8, 4) };

		VerifyCSharpDiagnostic(invalidProgram, expected);

		const string fixedProgram = """
			class Test
			{
				public string GetValue(int value) => value switch
				{
					0 =>
						"zero",
					_ =>
						"other",
				};
			}
			""";

		VerifyCSharpFix(invalidProgram, fixedProgram);
	}

	[Test]
	public void InvalidExpressionBodiedMemberWithCommentAfterOperator()
	{
		const string invalidProgram = """
			class Test
			{
				public int GetValue()
					=> // keep this comment
					1;
			}
			""";
		var expected = Expected(4, 3);

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
	public void ValidExpressionBodiedMemberWithCommentBeforeOperator()
	{
		const string validProgram = """
			class Test
			{
				public int GetValue() // keep this comment
					=> 1;
			}
			""";
		VerifyCSharpDiagnostic(validProgram);
	}

	protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new LambdaOperatorAnalyzer();

	protected override CodeFixProvider GetCSharpCodeFixProvider() => new LambdaOperatorCodeFixProvider();

	private static DiagnosticResult Expected(int line, int column) =>
		new()
		{
			Id = LambdaOperatorAnalyzer.DiagnosticId,
			Message = "Move => to the end of the previous line",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", line, column)],
		};
}
