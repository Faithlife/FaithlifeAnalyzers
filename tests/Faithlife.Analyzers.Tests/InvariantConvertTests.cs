using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests;

[TestFixture]
internal sealed class InvariantConvertTests : CodeFixVerifier
{
	[TestCase("var result = bool.Parse(input);", "var result = InvariantConvert.ParseBoolean(input);", "bool.Parse(input)")]
	[TestCase("var result = Boolean.Parse(input);", "var result = InvariantConvert.ParseBoolean(input);", "Boolean.Parse(input)")]
	[TestCase("var result = System.Boolean.Parse(input);", "var result = InvariantConvert.ParseBoolean(input);", "System.Boolean.Parse(input)")]
	[TestCase("var result = int.Parse(input, CultureInfo.InvariantCulture);", "var result = InvariantConvert.ParseInt32(input);", "int.Parse(input, CultureInfo.InvariantCulture)")]
	[TestCase("var result = Int32.Parse(input, CultureInfo.InvariantCulture);", "var result = InvariantConvert.ParseInt32(input);", "Int32.Parse(input, CultureInfo.InvariantCulture)")]
	[TestCase("var result = System.Int32.Parse(input, CultureInfo.InvariantCulture);", "var result = InvariantConvert.ParseInt32(input);", "System.Int32.Parse(input, CultureInfo.InvariantCulture)")]
	[TestCase("var result = int.Parse(input, NumberStyles.Integer, CultureInfo.InvariantCulture);", "var result = InvariantConvert.ParseInt32(input);", "int.Parse(input, NumberStyles.Integer, CultureInfo.InvariantCulture)")]
	[TestCase("var result = Int32.Parse(input, NumberStyles.Integer, CultureInfo.InvariantCulture);", "var result = InvariantConvert.ParseInt32(input);", "Int32.Parse(input, NumberStyles.Integer, CultureInfo.InvariantCulture)")]
	[TestCase("var result = double.Parse(input, CultureInfo.InvariantCulture);", "var result = InvariantConvert.ParseDouble(input);", "double.Parse(input, CultureInfo.InvariantCulture)")]
	[TestCase("var result = Double.Parse(input, CultureInfo.InvariantCulture);", "var result = InvariantConvert.ParseDouble(input);", "Double.Parse(input, CultureInfo.InvariantCulture)")]
	[TestCase("var result = System.Double.Parse(input, CultureInfo.InvariantCulture);", "var result = InvariantConvert.ParseDouble(input);", "System.Double.Parse(input, CultureInfo.InvariantCulture)")]
	[TestCase("var result = double.Parse(input, NumberStyles.Float, CultureInfo.InvariantCulture);", "var result = InvariantConvert.ParseDouble(input);", "double.Parse(input, NumberStyles.Float, CultureInfo.InvariantCulture)")]
	[TestCase("var result = Double.Parse(input, NumberStyles.Float, CultureInfo.InvariantCulture);", "var result = InvariantConvert.ParseDouble(input);", "Double.Parse(input, NumberStyles.Float, CultureInfo.InvariantCulture)")]
	[TestCase("var result = long.Parse(input, CultureInfo.InvariantCulture);", "var result = InvariantConvert.ParseInt64(input);", "long.Parse(input, CultureInfo.InvariantCulture)")]
	[TestCase("var result = Int64.Parse(input, CultureInfo.InvariantCulture);", "var result = InvariantConvert.ParseInt64(input);", "Int64.Parse(input, CultureInfo.InvariantCulture)")]
	[TestCase("var result = System.Int64.Parse(input, CultureInfo.InvariantCulture);", "var result = InvariantConvert.ParseInt64(input);", "System.Int64.Parse(input, CultureInfo.InvariantCulture)")]
	[TestCase("var result = long.Parse(input, NumberStyles.Integer, CultureInfo.InvariantCulture);", "var result = InvariantConvert.ParseInt64(input);", "long.Parse(input, NumberStyles.Integer, CultureInfo.InvariantCulture)")]
	[TestCase("var result = Int64.Parse(input, NumberStyles.Integer, CultureInfo.InvariantCulture);", "var result = InvariantConvert.ParseInt64(input);", "Int64.Parse(input, NumberStyles.Integer, CultureInfo.InvariantCulture)")]
	public void Parse(string invalidStatement, string fixedStatement, string diagnosticText)
	{
		var invalidProgram = CreateProgram(invalidStatement);
		var fixedProgram = CreateProgram(fixedStatement, includeSystemGlobalization: false, includeInvariantUsing: true);

		VerifyCSharpDiagnostic(invalidProgram, CreateDiagnostic(invalidProgram, diagnosticText));
		VerifyCSharpFix(invalidProgram, fixedProgram);
	}

	[TestCase("bool.TryParse(input, out var value)", "InvariantConvert.TryParseBoolean(input) is { } value")]
	[TestCase("Boolean.TryParse(input, out bool value)", "InvariantConvert.TryParseBoolean(input) is { } value")]
	[TestCase("int.TryParse(input, CultureInfo.InvariantCulture, out var value)", "InvariantConvert.TryParseInt32(input) is { } value")]
	[TestCase("Int32.TryParse(input, CultureInfo.InvariantCulture, out int value)", "InvariantConvert.TryParseInt32(input) is { } value")]
	[TestCase("int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)", "InvariantConvert.TryParseInt32(input) is { } value")]
	[TestCase("double.TryParse(input, CultureInfo.InvariantCulture, out var value)", "InvariantConvert.TryParseDouble(input) is { } value")]
	[TestCase("Double.TryParse(input, CultureInfo.InvariantCulture, out double value)", "InvariantConvert.TryParseDouble(input) is { } value")]
	[TestCase("double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)", "InvariantConvert.TryParseDouble(input) is { } value")]
	[TestCase("long.TryParse(input, CultureInfo.InvariantCulture, out var value)", "InvariantConvert.TryParseInt64(input) is { } value")]
	[TestCase("Int64.TryParse(input, CultureInfo.InvariantCulture, out long value)", "InvariantConvert.TryParseInt64(input) is { } value")]
	[TestCase("long.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)", "InvariantConvert.TryParseInt64(input) is { } value")]
	public void TryParseCondition(string invalidCondition, string fixedCondition)
	{
		var invalidStatement = $"if ({invalidCondition})\n\t\t\t\tGC.KeepAlive(value);";
		var fixedStatement = $"if ({fixedCondition})\n\t\t\t\tGC.KeepAlive(value);";
		var invalidProgram = CreateProgram(invalidStatement);
		var fixedProgram = CreateProgram(fixedStatement, includeSystemGlobalization: false, includeInvariantUsing: true);

		VerifyCSharpDiagnostic(invalidProgram, CreateDiagnostic(invalidProgram, invalidCondition));
		VerifyCSharpFix(invalidProgram, fixedProgram);
	}

	[TestCase("!bool.TryParse(input, out var value)", "InvariantConvert.TryParseBoolean(input) is not { } value", "bool.TryParse(input, out var value)")]
	[TestCase("!int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)", "InvariantConvert.TryParseInt32(input) is not { } value", "int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)")]
	[TestCase("!double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)", "InvariantConvert.TryParseDouble(input) is not { } value", "double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)")]
	[TestCase("!long.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)", "InvariantConvert.TryParseInt64(input) is not { } value", "long.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)")]
	public void NegatedTryParseCondition(string invalidCondition, string fixedCondition, string diagnosticText)
	{
		var invalidStatement = $"if ({invalidCondition})\n\t\t\t\treturn;\n\t\t\tGC.KeepAlive(value);";
		var fixedStatement = $"if ({fixedCondition})\n\t\t\t\treturn;\n\t\t\tGC.KeepAlive(value);";
		var invalidProgram = CreateProgram(invalidStatement);
		var fixedProgram = CreateProgram(fixedStatement, includeSystemGlobalization: false, includeInvariantUsing: true);

		VerifyCSharpDiagnostic(invalidProgram, CreateDiagnostic(invalidProgram, diagnosticText));
		VerifyCSharpFix(invalidProgram, fixedProgram);
	}

	[TestCase("boolValue", "boolValue.ToString(CultureInfo.InvariantCulture)", "boolValue.ToInvariantString()")]
	[TestCase("intValue", "intValue.ToString(CultureInfo.InvariantCulture)", "intValue.ToInvariantString()")]
	[TestCase("doubleValue", "doubleValue.ToString(CultureInfo.InvariantCulture)", "doubleValue.ToInvariantString()")]
	[TestCase("longValue", "longValue.ToString(CultureInfo.InvariantCulture)", "longValue.ToInvariantString()")]
	public void ToStringInvariantCulture(string valueExpression, string invalidExpression, string fixedExpression)
	{
		var invalidStatement = $"var result = {invalidExpression};";
		var fixedStatement = $"var result = {fixedExpression};";
		var invalidProgram = CreateProgram(invalidStatement);
		var fixedProgram = CreateProgram(fixedStatement, includeSystemGlobalization: false, includeInvariantUsing: true);

		VerifyCSharpDiagnostic(invalidProgram, CreateDiagnostic(invalidProgram, invalidExpression));
		VerifyCSharpFix(invalidProgram, fixedProgram);
	}

	[Test]
	public void NoDiagnosticWithoutInvariantConvert()
	{
		var invalidProgram = CreateProgram("var result = int.Parse(input, CultureInfo.InvariantCulture);", includeInvariantConvert: false);

		VerifyCSharpDiagnostic(invalidProgram);
	}

	[TestCase("var result = int.Parse(input, NumberStyles.None, CultureInfo.InvariantCulture);")]
	[TestCase("var result = long.Parse(input, NumberStyles.None, CultureInfo.InvariantCulture);")]
	[TestCase("var result = double.Parse(input, NumberStyles.None, CultureInfo.InvariantCulture);")]
	[TestCase("var result = int.Parse(input, NumberStyles.HexNumber, CultureInfo.InvariantCulture);")]
	[TestCase("var result = int.Parse(input);")]
	[TestCase("var result = int.Parse(input, CultureInfo.CurrentCulture);")]
	[TestCase("var result = decimal.Parse(input, CultureInfo.InvariantCulture);")]
	[TestCase("var result = objectValue.ToString();")]
	[TestCase("var result = DateTime.UtcNow.ToString(CultureInfo.InvariantCulture);")]
	[TestCase("var result = nullableInt.ToString();")]
	public void UnsupportedUsage(string statement)
	{
		VerifyCSharpDiagnostic(CreateProgram(statement));
	}

	[Test]
	public void DoesNotDiagnoseTryParseExistingOutVariable()
	{
		const string statement = """
			int value;
			if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
				GC.KeepAlive(value);
			""";

		VerifyCSharpDiagnostic(CreateProgram(statement));
	}

	[Test]
	public void DoesNotDiagnoseTryParseOutVariableUsedAfterCondition()
	{
		const string statement = """
			if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
			{
			}
			GC.KeepAlive(value);
			""";

		VerifyCSharpDiagnostic(CreateProgram(statement));
	}

	protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new InvariantConvertAnalyzer();

	protected override CodeFixProvider GetCSharpCodeFixProvider() => new InvariantConvertCodeFixProvider();

	private static DiagnosticResult CreateDiagnostic(string program, string diagnosticText)
	{
		var index = program.IndexOf(diagnosticText, System.StringComparison.Ordinal);
		Assert.That(index, Is.GreaterThanOrEqualTo(0), $"Could not find '{diagnosticText}' in the test program.");

		var line = 1;
		var column = 1;
		for (int i = 0; i < index; i++)
		{
			if (program[i] == '\n')
			{
				line++;
				column = 1;
			}
			else
			{
				column++;
			}
		}

		return new DiagnosticResult
		{
			Id = InvariantConvertAnalyzer.DiagnosticId,
			Message = "Use InvariantConvert for invariant culture conversions",
			Severity = DiagnosticSeverity.Info,
			Locations = [new DiagnosticResultLocation("Test0.cs", line, column)],
		};
	}

	private static string CreateProgram(string statement, bool includeInvariantConvert = true, bool includeSystemGlobalization = true, bool includeInvariantUsing = false)
	{
		var usings = "using System;";
		if (includeSystemGlobalization)
			usings += "\nusing System.Globalization;";
		if (includeInvariantUsing)
			usings += "\nusing Libronix.Utility.Invariant;";

		var invariantConvert = includeInvariantConvert ? c_invariantConvert : "";

		return $$"""
			{{usings}}

			{{invariantConvert}}
			namespace TestApplication
			{
				internal static class TestClass
				{
					public static void Test(string input, bool boolValue, int intValue, double doubleValue, long longValue, object objectValue, int? nullableInt)
					{
						{{statement}}
					}
				}
			}
			""";
	}

	private const string c_invariantConvert = """
		namespace Libronix.Utility.Invariant
		{
			public static class InvariantConvert
			{
				public static string ToInvariantString(this bool value) => throw new NotImplementedException();
				public static bool? TryParseBoolean(string text) => throw new NotImplementedException();
				public static bool ParseBoolean(string text) => throw new NotImplementedException();
				public static string ToInvariantString(this double value) => throw new NotImplementedException();
				public static double? TryParseDouble(string text) => throw new NotImplementedException();
				public static double ParseDouble(string text) => throw new NotImplementedException();
				public static string ToInvariantString(this int value) => throw new NotImplementedException();
				public static int? TryParseInt32(string text) => throw new NotImplementedException();
				public static int ParseInt32(string text) => throw new NotImplementedException();
				public static string ToInvariantString(this long value) => throw new NotImplementedException();
				public static long? TryParseInt64(string text) => throw new NotImplementedException();
				public static long ParseInt64(string text) => throw new NotImplementedException();
			}
		}

		""";
}
