using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests;

[TestFixture]
internal sealed class UseInvariantConvertTests : CodeFixVerifier
{
	[TestCase("bool.Parse(input)", "InvariantConvert.ParseBoolean(input)", "ParseBoolean")]
	[TestCase("Boolean.Parse(input)", "InvariantConvert.ParseBoolean(input)", "ParseBoolean")]
	[TestCase("int.Parse(input, CultureInfo.InvariantCulture)", "InvariantConvert.ParseInt32(input)", "ParseInt32")]
	[TestCase("int.Parse(input, NumberStyles.None, CultureInfo.InvariantCulture)", "InvariantConvert.ParseInt32(input)", "ParseInt32")]
	[TestCase("int.Parse(input, NumberStyles.Integer, CultureInfo.InvariantCulture)", "InvariantConvert.ParseInt32(input)", "ParseInt32")]
	[TestCase("Int32.Parse(input, CultureInfo.InvariantCulture)", "InvariantConvert.ParseInt32(input)", "ParseInt32")]
	[TestCase("double.Parse(input, CultureInfo.InvariantCulture)", "InvariantConvert.ParseDouble(input)", "ParseDouble")]
	[TestCase("double.Parse(input, NumberStyles.None, CultureInfo.InvariantCulture)", "InvariantConvert.ParseDouble(input)", "ParseDouble")]
	[TestCase("double.Parse(input, NumberStyles.Float, CultureInfo.InvariantCulture)", "InvariantConvert.ParseDouble(input)", "ParseDouble")]
	[TestCase("Double.Parse(input, CultureInfo.InvariantCulture)", "InvariantConvert.ParseDouble(input)", "ParseDouble")]
	[TestCase("long.Parse(input, CultureInfo.InvariantCulture)", "InvariantConvert.ParseInt64(input)", "ParseInt64")]
	[TestCase("long.Parse(input, NumberStyles.None, CultureInfo.InvariantCulture)", "InvariantConvert.ParseInt64(input)", "ParseInt64")]
	[TestCase("long.Parse(input, NumberStyles.Integer, CultureInfo.InvariantCulture)", "InvariantConvert.ParseInt64(input)", "ParseInt64")]
	[TestCase("Int64.Parse(input, CultureInfo.InvariantCulture)", "InvariantConvert.ParseInt64(input)", "ParseInt64")]
	[TestCase("boolValue.ToString(CultureInfo.InvariantCulture)", "boolValue.ToInvariantString()", "ToInvariantString")]
	[TestCase("intValue.ToString(CultureInfo.InvariantCulture)", "intValue.ToInvariantString()", "ToInvariantString")]
	[TestCase("longValue.ToString(CultureInfo.InvariantCulture)", "longValue.ToInvariantString()", "ToInvariantString")]
	[TestCase("doubleValue.ToString(CultureInfo.InvariantCulture)", "doubleValue.ToInvariantString()", "ToInvariantString")]
	public void ParseAndToString(string call, string fixedCall, string suggestedMethod)
	{
		var invalidProgram = CreateExpressionProgram(c_preamble, call);

		var expected = new DiagnosticResult
		{
			Id = UseInvariantConvertAnalyzer.DiagnosticId,
			Message = $"Use 'InvariantConvert.{suggestedMethod}'",
			Severity = DiagnosticSeverity.Info,
			Locations = [new DiagnosticResultLocation("Test0.cs", s_preambleLength + 7, 11)],
		};

		VerifyCSharpDiagnostic(invalidProgram, expected);
		VerifyCSharpFix(invalidProgram, CreateExpressionProgram(s_fixedPreamble, fixedCall));
	}

	[TestCase("int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours)", "InvariantConvert.TryParseInt32(parts[0]) is not { } hours", "TryParseInt32")]
	[TestCase("int.TryParse(parts[0], CultureInfo.InvariantCulture, out var hours)", "InvariantConvert.TryParseInt32(parts[0]) is not { } hours", "TryParseInt32")]
	[TestCase("long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours)", "InvariantConvert.TryParseInt64(parts[0]) is not { } hours", "TryParseInt64")]
	[TestCase("double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var hours)", "InvariantConvert.TryParseDouble(parts[0]) is not { } hours", "TryParseDouble")]
	[TestCase("bool.TryParse(parts[0], out var hours)", "InvariantConvert.TryParseBoolean(parts[0]) is not { } hours", "TryParseBoolean")]
	public void NegatedTryParse(string call, string fixedCall, string suggestedMethod)
	{
		var invalidProgram = CreateNegatedTryParseProgram(c_preamble, "!" + call);

		var expected = new DiagnosticResult
		{
			Id = UseInvariantConvertAnalyzer.DiagnosticId,
			Message = $"Use 'InvariantConvert.{suggestedMethod}'",
			Severity = DiagnosticSeverity.Info,
			Locations = [new DiagnosticResultLocation("Test0.cs", s_preambleLength + 7, 9)],
		};

		VerifyCSharpDiagnostic(invalidProgram, expected);
		VerifyCSharpFix(invalidProgram, CreateNegatedTryParseProgram(s_fixedPreamble, fixedCall));
	}

	[TestCase("int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes)", "InvariantConvert.TryParseInt32(parts[1]) is { } minutes", "TryParseInt32")]
	[TestCase("long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes)", "InvariantConvert.TryParseInt64(parts[1]) is { } minutes", "TryParseInt64")]
	[TestCase("double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes)", "InvariantConvert.TryParseDouble(parts[1]) is { } minutes", "TryParseDouble")]
	[TestCase("bool.TryParse(parts[1], out var minutes)", "InvariantConvert.TryParseBoolean(parts[1]) is { } minutes", "TryParseBoolean")]
	public void NonNegatedTryParse(string call, string fixedCall, string suggestedMethod)
	{
		var invalidProgram = CreateNonNegatedTryParseProgram(c_preamble, call);

		var expected = new DiagnosticResult
		{
			Id = UseInvariantConvertAnalyzer.DiagnosticId,
			Message = $"Use 'InvariantConvert.{suggestedMethod}'",
			Severity = DiagnosticSeverity.Info,
			Locations = [new DiagnosticResultLocation("Test0.cs", s_preambleLength + 7, 8)],
		};

		VerifyCSharpDiagnostic(invalidProgram, expected);
		VerifyCSharpFix(invalidProgram, CreateNonNegatedTryParseProgram(s_fixedPreamble, fixedCall));
	}

	[TestCase("int.Parse(input)")]
	[TestCase("int.Parse(input, CultureInfo.CurrentCulture)")]
	[TestCase("int.Parse(input, NumberStyles.HexNumber, CultureInfo.InvariantCulture)")]
	[TestCase("double.Parse(input, NumberStyles.Integer, CultureInfo.InvariantCulture)")]
	[TestCase("decimal.Parse(input, CultureInfo.InvariantCulture)")]
	[TestCase("intValue.ToString()")]
	[TestCase("intValue.ToString(\"D\", CultureInfo.InvariantCulture)")]
	[TestCase("input.ToString(CultureInfo.InvariantCulture)")]
	public void NoDiagnostic(string call)
	{
		VerifyCSharpDiagnostic(CreateExpressionProgram(c_preamble, call));
	}

	[Test]
	public void NoDiagnosticWhenTypeNotReferenced()
	{
		const string program = """
			using System;
			using System.Globalization;

			namespace TestProgram
			{
				internal static class TestClass
				{
					public static int TestMethod(string input) => int.Parse(input, CultureInfo.InvariantCulture);
				}
			}
			""";

		VerifyCSharpDiagnostic(program);
	}

	[Test]
	public void NotFixedInUnsupportedContext()
	{
		var invalidProgram = $$"""
			{{c_preamble}}
			namespace TestProgram
			{
				internal static class TestClass
				{
					public static int TestMethod(string input)
					{
						var ok = int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value);
						return ok ? value : 0;
					}
				}
			}
			""";

		var expected = new DiagnosticResult
		{
			Id = UseInvariantConvertAnalyzer.DiagnosticId,
			Message = "Use 'InvariantConvert.TryParseInt32'",
			Severity = DiagnosticSeverity.Info,
			Locations = [new DiagnosticResultLocation("Test0.cs", s_preambleLength + 7, 13)],
		};

		VerifyCSharpDiagnostic(invalidProgram, expected);

		// The analyzer reports the usage, but the fixer declines to transform it because the
		// out variable is read on both branches, so the program is left unchanged.
		VerifyCSharpFix(invalidProgram, invalidProgram);
	}

	protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new UseInvariantConvertAnalyzer();

	protected override CodeFixProvider GetCSharpCodeFixProvider() => new UseInvariantConvertCodeFixProvider();

	private static string CreateExpressionProgram(string preamble, string statement) => $$"""
		{{preamble}}
		namespace TestProgram
		{
			internal static class TestClass
			{
				public static object TestMethod(string input, bool boolValue, int intValue, long longValue, double doubleValue)
				{
					return {{statement}};
				}
			}
		}
		""";

	private static string CreateNegatedTryParseProgram(string preamble, string condition) => $$"""
		{{preamble}}
		namespace TestProgram
		{
			internal static class TestClass
			{
				public static object TestMethod(string[] parts)
				{
					if ({{condition}})
						return 0;
					return hours;
				}
			}
		}
		""";

	private static string CreateNonNegatedTryParseProgram(string preamble, string condition) => $$"""
		{{preamble}}
		namespace TestProgram
		{
			internal static class TestClass
			{
				public static object TestMethod(string[] parts)
				{
					if ({{condition}})
						return minutes;
					return 0;
				}
			}
		}
		""";

	private const string c_preamble = """
		using System;
		using System.Globalization;

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

	private static readonly string s_fixedPreamble = c_preamble.Replace("using System.Globalization;", "using Libronix.Utility.Invariant;", StringComparison.Ordinal);

	private static readonly int s_preambleLength = c_preamble.Split('\n').Length;
}
