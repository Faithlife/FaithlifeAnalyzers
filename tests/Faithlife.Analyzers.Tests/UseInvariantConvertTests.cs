using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests;

[TestFixture]
internal sealed class UseInvariantConvertTests : CodeFixVerifier
{
	[TestCase("bool.Parse(input)", "InvariantConvert.ParseBoolean(input)")]
	[TestCase("Boolean.Parse(input)", "InvariantConvert.ParseBoolean(input)")]
	[TestCase("int.Parse(input, CultureInfo.InvariantCulture)", "InvariantConvert.ParseInt32(input)")]
	[TestCase("int.Parse(input, NumberStyles.None, CultureInfo.InvariantCulture)", "InvariantConvert.ParseInt32(input)")]
	[TestCase("int.Parse(input, NumberStyles.Integer, CultureInfo.InvariantCulture)", "InvariantConvert.ParseInt32(input)")]
	[TestCase("Int32.Parse(input, CultureInfo.InvariantCulture)", "InvariantConvert.ParseInt32(input)")]
	[TestCase("long.Parse(input, CultureInfo.InvariantCulture)", "InvariantConvert.ParseInt64(input)")]
	[TestCase("long.Parse(input, NumberStyles.None, CultureInfo.InvariantCulture)", "InvariantConvert.ParseInt64(input)")]
	[TestCase("long.Parse(input, NumberStyles.Integer, CultureInfo.InvariantCulture)", "InvariantConvert.ParseInt64(input)")]
	[TestCase("Int64.Parse(input, CultureInfo.InvariantCulture)", "InvariantConvert.ParseInt64(input)")]
	[TestCase("double.Parse(input, CultureInfo.InvariantCulture)", "InvariantConvert.ParseDouble(input)")]
	[TestCase("double.Parse(input, NumberStyles.None, CultureInfo.InvariantCulture)", "InvariantConvert.ParseDouble(input)")]
	[TestCase("double.Parse(input, NumberStyles.Float, CultureInfo.InvariantCulture)", "InvariantConvert.ParseDouble(input)")]
	[TestCase("Double.Parse(input, CultureInfo.InvariantCulture)", "InvariantConvert.ParseDouble(input)")]
	[TestCase("int.Parse(parts[0], CultureInfo.InvariantCulture)", "InvariantConvert.ParseInt32(parts[0])")]
	[TestCase("boolValue.ToString(CultureInfo.InvariantCulture)", "boolValue.ToInvariantString()")]
	[TestCase("intValue.ToString(CultureInfo.InvariantCulture)", "intValue.ToInvariantString()")]
	[TestCase("longValue.ToString(CultureInfo.InvariantCulture)", "longValue.ToInvariantString()")]
	[TestCase("doubleValue.ToString(CultureInfo.InvariantCulture)", "doubleValue.ToInvariantString()")]
	public void DetectsExpression(string call, string fixedCall)
	{
		var program = CreateProgram($"var result = {call};");
		VerifyCSharpDiagnostic(program, CreateDiagnostic(program, call));
		VerifyCSharpFix(program, CreateProgram($"var result = {fixedCall};"));
	}

	[TestCase("bool.TryParse(input, out var value)", "InvariantConvert.TryParseBoolean(input) is { } value", false)]
	[TestCase("bool.TryParse(input, out var value)", "InvariantConvert.TryParseBoolean(input) is not { } value", true)]
	[TestCase("int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)", "InvariantConvert.TryParseInt32(input) is { } value", false)]
	[TestCase("int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)", "InvariantConvert.TryParseInt32(input) is not { } value", true)]
	[TestCase("int.TryParse(input, CultureInfo.InvariantCulture, out var value)", "InvariantConvert.TryParseInt32(input) is { } value", false)]
	[TestCase("int.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out var value)", "InvariantConvert.TryParseInt32(input) is { } value", false)]
	[TestCase("long.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)", "InvariantConvert.TryParseInt64(input) is { } value", false)]
	[TestCase("long.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)", "InvariantConvert.TryParseInt64(input) is not { } value", true)]
	[TestCase("double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)", "InvariantConvert.TryParseDouble(input) is { } value", false)]
	[TestCase("double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)", "InvariantConvert.TryParseDouble(input) is not { } value", true)]
	[TestCase("int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)", "InvariantConvert.TryParseInt32(parts[0]) is not { } value", true)]
	[TestCase("int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)", "InvariantConvert.TryParseInt32(input) is { } value", false)]
	public void DetectsTryParse(string call, string fixedCondition, bool negate)
	{
		var body = negate
			? $"if (!{call}) return; _ = value;"
			: $"if ({call}) _ = value;";
		var fixedBody = negate
			? $"if ({fixedCondition}) return; _ = value;"
			: $"if ({fixedCondition}) _ = value;";

		var program = CreateProgram(body);
		VerifyCSharpDiagnostic(program, CreateDiagnostic(program, call));
		VerifyCSharpFix(program, CreateProgram(fixedBody));
	}

	[TestCase("int.Parse(input)")]
	[TestCase("int.Parse(input, CultureInfo.CurrentCulture)")]
	[TestCase("int.Parse(input, NumberStyles.HexNumber, CultureInfo.InvariantCulture)")]
	[TestCase("double.Parse(input, NumberStyles.Any, CultureInfo.InvariantCulture)")]
	[TestCase("intValue.ToString()")]
	[TestCase("intValue.ToString(\"G\")")]
	[TestCase("intValue.ToString(CultureInfo.CurrentCulture)")]
	[TestCase("decimalValue.ToString(CultureInfo.InvariantCulture)")]
	public void DoesNotFire(string call)
	{
		var program = CreateProgram($"var result = {call};");
		VerifyCSharpDiagnostic(program);
	}

	[Test]
	public void DoesNotFireWhenPackageNotReferenced()
	{
		const string program = """
			using System.Globalization;

			namespace TestApplication
			{
				public static class TestClass
				{
					public static void Run(string input)
					{
						var result = int.Parse(input, CultureInfo.InvariantCulture);
					}
				}
			}
			""";
		VerifyCSharpDiagnostic(program);
	}

	protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new UseInvariantConvertAnalyzer();

	protected override CodeFixProvider GetCSharpCodeFixProvider() => new UseInvariantConvertCodeFixProvider();

	private static DiagnosticResult CreateDiagnostic(string program, string invocationText)
	{
		var index = program.IndexOf(invocationText, StringComparison.Ordinal);
		Assert.That(index, Is.GreaterThanOrEqualTo(0), $"Could not find '{invocationText}' in the test program.");

		var prefix = program.Substring(0, index);
		var line = prefix.Count(c => c == '\n') + 1;
		var column = index - prefix.LastIndexOf('\n');

		return new DiagnosticResult
		{
			Id = UseInvariantConvertAnalyzer.DiagnosticId,
			Message = "Use InvariantConvert",
			Severity = DiagnosticSeverity.Info,
			Locations = [new DiagnosticResultLocation("Test0.cs", line, column)],
		};
	}

	private static string CreateProgram(string body) => $$"""
		using System;
		using System.Globalization;
		using Libronix.Utility.Invariant;

		namespace Libronix.Utility.Invariant
		{
			public static class InvariantConvert
			{
				public static string ToInvariantString(this bool value) => throw new NotImplementedException();
				public static string ToInvariantString(this int value) => throw new NotImplementedException();
				public static string ToInvariantString(this long value) => throw new NotImplementedException();
				public static string ToInvariantString(this double value) => throw new NotImplementedException();
				public static bool ParseBoolean(string text) => throw new NotImplementedException();
				public static int ParseInt32(string text) => throw new NotImplementedException();
				public static long ParseInt64(string text) => throw new NotImplementedException();
				public static double ParseDouble(string text) => throw new NotImplementedException();
				public static bool? TryParseBoolean(string text) => throw new NotImplementedException();
				public static int? TryParseInt32(string text) => throw new NotImplementedException();
				public static long? TryParseInt64(string text) => throw new NotImplementedException();
				public static double? TryParseDouble(string text) => throw new NotImplementedException();

				// keep the System.Globalization using referenced so the code fix does not create an unused-using diagnostic
				public static CultureInfo Culture => CultureInfo.InvariantCulture;
				public static NumberStyles Styles => NumberStyles.Integer;
			}
		}

		namespace TestApplication
		{
			public static class TestClass
			{
				public static void Run(string input, string[] parts, bool boolValue, int intValue, long longValue, double doubleValue, decimal decimalValue)
				{
					{{body}}
				}
			}
		}
		""";
}
