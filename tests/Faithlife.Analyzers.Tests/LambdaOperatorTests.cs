using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests;

[TestFixture]
internal sealed class LambdaOperatorTests : CodeFixVerifier
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
				private readonly Func<int, int> increment = x => x + 1;
				private readonly Func<int, int, int> add = (x, y) => x + y;
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
				public string GetText(int value)
				{
					return value switch
					{
						0 => "zero",
						_ => "other",
					};
				}
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

		VerifyCSharpDiagnostic(invalidProgram, Expected(4, 3));

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

		VerifyCSharpDiagnostic(invalidProgram, Expected(4, 3));

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
	public void InvalidExpressionBodiedMethodWithCommentAfterOperator()
	{
		const string invalidProgram = """
			class Test
			{
				public int GetValue()
					=> // keep this comment
					1;
			}
			""";

		VerifyCSharpDiagnostic(invalidProgram, Expected(4, 3));

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

		VerifyCSharpDiagnostic(invalidProgram, Expected(6, 3));

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

		VerifyCSharpDiagnostic(invalidProgram, Expected(6, 3));

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

		VerifyCSharpDiagnostic(invalidProgram, Expected(4, 3));

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

		VerifyCSharpDiagnostic(invalidProgram, Expected(4, 3));

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

		VerifyCSharpDiagnostic(invalidProgram, Expected(6, 4));

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
	public void InvalidSimpleLambdaExpression()
	{
		const string invalidProgram = """
			using System;

			class Test
			{
				private readonly Func<int, int> increment = x
					=> x + 1;
			}
			""";

		VerifyCSharpDiagnostic(invalidProgram, Expected(6, 3));

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
	public void InvalidParenthesizedLambdaExpression()
	{
		const string invalidProgram = """
			using System;

			class Test
			{
				private readonly Func<int, int, int> add = (x, y)
					=> x + y;
			}
			""";

		VerifyCSharpDiagnostic(invalidProgram, Expected(6, 3));

		const string fixedProgram = """
			using System;

			class Test
			{
				private readonly Func<int, int, int> add = (x, y) =>
					x + y;
			}
			""";

		VerifyCSharpFix(invalidProgram, fixedProgram, 0);
	}

	[Test]
	public void InvalidSwitchExpressionArm()
	{
		const string invalidProgram = """
			class Test
			{
				public string GetText(int value)
				{
					return value switch
					{
						0
							=> "zero",
						_
							=> "other",
					};
				}
			}
			""";

		VerifyCSharpDiagnostic(invalidProgram, Expected(8, 5), Expected(10, 5));

		const string fixedProgram = """
			class Test
			{
				public string GetText(int value)
				{
					return value switch
					{
						0 =>
							"zero",
						_ =>
							"other",
					};
				}
			}
			""";

		VerifyCSharpFix(invalidProgram, fixedProgram);
	}

	[Test]
	public void InvalidExpressionBodiedMembersAndLambdaOperatorsFixAll()
	{
		const string invalidProgram = """
			using System;

			class Test
			{
				private readonly Func<int, int> increment = x
					=> x + 1;

				public int GetFirst()
					=> 1;

				public string GetText(int value)
				{
					return value switch
					{
						0
							=> "zero",
						_
							=> "other",
					};
				}
			}
			""";

		const string fixedProgram = """
			using System;

			class Test
			{
				private readonly Func<int, int> increment = x =>
					x + 1;

				public int GetFirst() =>
					1;

				public string GetText(int value)
				{
					return value switch
					{
						0 =>
							"zero",
						_ =>
							"other",
					};
				}
			}
			""";

		VerifyCSharpFix(invalidProgram, fixedProgram);
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
