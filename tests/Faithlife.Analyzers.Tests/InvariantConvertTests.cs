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
	[TestCase("var result = int.Parse(s: input, provider: CultureInfo.InvariantCulture);", "var result = InvariantConvert.ParseInt32(input);", "int.Parse(s: input, provider: CultureInfo.InvariantCulture)")]
	[TestCase("var result = int.Parse(provider: CultureInfo.InvariantCulture, s: input);", "var result = InvariantConvert.ParseInt32(input);", "int.Parse(provider: CultureInfo.InvariantCulture, s: input)")]
	[TestCase("var result = int.Parse(input, NumberStyles.Integer, CultureInfo.InvariantCulture);", "var result = InvariantConvert.ParseInt32(input);", "int.Parse(input, NumberStyles.Integer, CultureInfo.InvariantCulture)")]
	[TestCase("var result = Int32.Parse(input, NumberStyles.Integer, CultureInfo.InvariantCulture);", "var result = InvariantConvert.ParseInt32(input);", "Int32.Parse(input, NumberStyles.Integer, CultureInfo.InvariantCulture)")]
	[TestCase("var result = int.Parse(style: NumberStyles.Integer, provider: CultureInfo.InvariantCulture, s: input);", "var result = InvariantConvert.ParseInt32(input);", "int.Parse(style: NumberStyles.Integer, provider: CultureInfo.InvariantCulture, s: input)")]
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
	[TestCase("int.TryParse(result: out var value, provider: CultureInfo.InvariantCulture, s: input)", "InvariantConvert.TryParseInt32(input) is { } value")]
	[TestCase("int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)", "InvariantConvert.TryParseInt32(input) is { } value")]
	[TestCase("int.TryParse(result: out var value, provider: CultureInfo.InvariantCulture, style: NumberStyles.Integer, s: input)", "InvariantConvert.TryParseInt32(input) is { } value")]
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
	[TestCase("!(int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))", "InvariantConvert.TryParseInt32(input) is not { } value", "int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)")]
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
	public void ToStringClassPropertyReceiversAreFixed()
	{
		const string invalidStatement = """
			var record = new DataRecord();
			var length = record.Segment.Length.ToString(CultureInfo.InvariantCulture);
			var offset = record.Segment.Offset.ToString(CultureInfo.InvariantCulture);
			""";
		const string fixedStatement = """
			var record = new DataRecord();
			var length = record.Segment.Length.ToInvariantString();
			var offset = record.Segment.Offset.ToInvariantString();
			""";
		var invalidProgram = CreateProgram(invalidStatement, extraTypes: c_segmentTypes);
		var fixedProgram = CreateProgram(fixedStatement, includeSystemGlobalization: false, includeInvariantUsing: true, extraTypes: c_segmentTypes);

		VerifyCSharpDiagnostic(invalidProgram,
			CreateDiagnostic(invalidProgram, "record.Segment.Length.ToString(CultureInfo.InvariantCulture)"),
			CreateDiagnostic(invalidProgram, "record.Segment.Offset.ToString(CultureInfo.InvariantCulture)"));
		VerifyCSharpFix(invalidProgram, fixedProgram);
	}

	[Test]
	public void ToStringMethodCallReceiversAreFixed()
	{
		const string invalidStatement = """
			var provider = new ValueProvider();
			var length = provider.GetLength().ToString(CultureInfo.InvariantCulture);
			var offset = provider.GetSegment().Offset.ToString(CultureInfo.InvariantCulture);
			""";
		const string fixedStatement = """
			var provider = new ValueProvider();
			var length = provider.GetLength().ToInvariantString();
			var offset = provider.GetSegment().Offset.ToInvariantString();
			""";
		var invalidProgram = CreateProgram(invalidStatement, extraTypes: c_segmentTypes);
		var fixedProgram = CreateProgram(fixedStatement, includeSystemGlobalization: false, includeInvariantUsing: true, extraTypes: c_segmentTypes);

		VerifyCSharpDiagnostic(invalidProgram,
			CreateDiagnostic(invalidProgram, "provider.GetLength().ToString(CultureInfo.InvariantCulture)"),
			CreateDiagnostic(invalidProgram, "provider.GetSegment().Offset.ToString(CultureInfo.InvariantCulture)"));
		VerifyCSharpFix(invalidProgram, fixedProgram);
	}

	[Test]
	public void ToStringLambdaParameterReceiversAreFixed()
	{
		const string invalidStatement = "Func<int, string> formatter = x => x.ToString(CultureInfo.InvariantCulture);";
		const string fixedStatement = "Func<int, string> formatter = x => x.ToInvariantString();";
		var invalidProgram = CreateProgram(invalidStatement);
		var fixedProgram = CreateProgram(fixedStatement, includeSystemGlobalization: false, includeInvariantUsing: true);

		VerifyCSharpDiagnostic(invalidProgram, CreateDiagnostic(invalidProgram, "x.ToString(CultureInfo.InvariantCulture)"));
		VerifyCSharpFix(invalidProgram, fixedProgram);
	}

	[Test]
	public void ToStringLambdaMemberAccessReceiversAreFixed()
	{
		const string invalidStatement = """
			Func<ValueFields, string> intFormatter = x => x.IntField.ToString(CultureInfo.InvariantCulture);
			Func<ValueFields, string> longFormatter = x => x.LongField.ToString(CultureInfo.InvariantCulture);
			Func<ValueFields, string> doubleFormatter = x => x.DoubleField.ToString(CultureInfo.InvariantCulture);
			Func<ValueFields, string> boolFormatter = x => x.BoolField.ToString(CultureInfo.InvariantCulture);
			""";
		const string fixedStatement = """
			Func<ValueFields, string> intFormatter = x => x.IntField.ToInvariantString();
			Func<ValueFields, string> longFormatter = x => x.LongField.ToInvariantString();
			Func<ValueFields, string> doubleFormatter = x => x.DoubleField.ToInvariantString();
			Func<ValueFields, string> boolFormatter = x => x.BoolField.ToInvariantString();
			""";
		var invalidProgram = CreateProgram(invalidStatement, extraTypes: c_valueFieldTypes);
		var fixedProgram = CreateProgram(fixedStatement, includeSystemGlobalization: false, includeInvariantUsing: true, extraTypes: c_valueFieldTypes);

		VerifyCSharpDiagnostic(invalidProgram,
			CreateDiagnostic(invalidProgram, "x.IntField.ToString(CultureInfo.InvariantCulture)"),
			CreateDiagnostic(invalidProgram, "x.LongField.ToString(CultureInfo.InvariantCulture)"),
			CreateDiagnostic(invalidProgram, "x.DoubleField.ToString(CultureInfo.InvariantCulture)"),
			CreateDiagnostic(invalidProgram, "x.BoolField.ToString(CultureInfo.InvariantCulture)"));
		VerifyCSharpFix(invalidProgram, fixedProgram);
	}

	[Test]
	public void ParseComplexInputExpressionsAreFixed()
	{
		const string invalidStatement = """
			var provider = new TextProvider();
			var index = 0;
			var values = new[] { "1", "2" };
			var parsedProperty = int.Parse(provider.GetText().Value, CultureInfo.InvariantCulture);
			var parsedElement = long.Parse(values[index], NumberStyles.Integer, CultureInfo.InvariantCulture);
			var parsedConditional = double.Parse(boolValue ? provider.GetText().Value : input, NumberStyles.Float, CultureInfo.InvariantCulture);
			var parsedCoalesce = bool.Parse(provider.GetNullableText() ?? input);
			""";
		const string fixedStatement = """
			var provider = new TextProvider();
			var index = 0;
			var values = new[] { "1", "2" };
			var parsedProperty = InvariantConvert.ParseInt32(provider.GetText().Value);
			var parsedElement = InvariantConvert.ParseInt64(values[index]);
			var parsedConditional = InvariantConvert.ParseDouble(boolValue ? provider.GetText().Value : input);
			var parsedCoalesce = InvariantConvert.ParseBoolean(provider.GetNullableText() ?? input);
			""";
		var invalidProgram = CreateProgram(invalidStatement, extraTypes: c_textProviderTypes);
		var fixedProgram = CreateProgram(fixedStatement, includeSystemGlobalization: false, includeInvariantUsing: true, extraTypes: c_textProviderTypes);

		VerifyCSharpDiagnostic(invalidProgram,
			CreateDiagnostic(invalidProgram, "int.Parse(provider.GetText().Value, CultureInfo.InvariantCulture)"),
			CreateDiagnostic(invalidProgram, "long.Parse(values[index], NumberStyles.Integer, CultureInfo.InvariantCulture)"),
			CreateDiagnostic(invalidProgram, "double.Parse(boolValue ? provider.GetText().Value : input, NumberStyles.Float, CultureInfo.InvariantCulture)"),
			CreateDiagnostic(invalidProgram, "bool.Parse(provider.GetNullableText() ?? input)"));
		VerifyCSharpFix(invalidProgram, fixedProgram);
	}

	[Test]
	public void NoDiagnosticWithoutInvariantConvert()
	{
		VerifyCSharpDiagnostic(CreateProgram("var result = int.Parse(input, CultureInfo.InvariantCulture);", includeInvariantConvert: false));
	}

	[TestCase("var result = int.Parse(input, NumberStyles.None, CultureInfo.InvariantCulture);")]
	[TestCase("var result = long.Parse(input, NumberStyles.None, CultureInfo.InvariantCulture);")]
	[TestCase("var result = double.Parse(input, NumberStyles.None, CultureInfo.InvariantCulture);")]
	[TestCase("var result = int.Parse(input, NumberStyles.HexNumber, CultureInfo.InvariantCulture);")]
	[TestCase("var result = int.Parse(input);")]
	[TestCase("var result = int.Parse(input, CultureInfo.CurrentCulture);")]
	[TestCase("var result = decimal.Parse(input, CultureInfo.InvariantCulture);")]
	[TestCase("var result = intValue.ToString(\"D\", CultureInfo.InvariantCulture);")]
	[TestCase("var result = objectValue.ToString();")]
	[TestCase("var result = DateTime.UtcNow.ToString(CultureInfo.InvariantCulture);")]
	[TestCase("var result = nullableInt.ToString();")]
	public void UnsupportedUsage(string statement)
	{
		VerifyCSharpDiagnostic(CreateProgram(statement));
	}

	[Test]
	public void TryParseExistingOutVariableIsDiagnosedButNotFixed()
	{
		const string statement = """
			int value;
			if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
				GC.KeepAlive(value);
			""";
		var invalidProgram = CreateProgram(statement);

		VerifyCSharpDiagnostic(invalidProgram, CreateDiagnostic(invalidProgram, "int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)"));
		VerifyCSharpFix(invalidProgram, invalidProgram);
	}

	[Test]
	public void TryParseOutVariableUsedAfterConditionIsDiagnosedButNotFixed()
	{
		const string statement = """
			if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
			{
			}
			GC.KeepAlive(value);
			""";
		var invalidProgram = CreateProgram(statement);

		VerifyCSharpDiagnostic(invalidProgram, CreateDiagnostic(invalidProgram, "int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)"));
		VerifyCSharpFix(invalidProgram, invalidProgram);
	}

	[Test]
	public void TryParseOutVariableUsedInBodyAndAfterConditionIsDiagnosedButNotFixed()
	{
		const string statement = """
			if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
				GC.KeepAlive(value);
			GC.KeepAlive(value);
			""";

		VerifyDiagnosticWithoutFix(statement, "int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)");
	}

	[Test]
	public void TryParseOutVariableUsedAfterElseReturnIsFixed()
	{
		const string invalidStatement = """
			if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
			{
			}
			else
			{
				return;
			}
			GC.KeepAlive(value);
			""";
		const string fixedStatement = """
			if (InvariantConvert.TryParseInt32(input) is { } value)
			{
			}
			else
			{
				return;
			}
			GC.KeepAlive(value);
			""";
		var invalidProgram = CreateProgram(invalidStatement);
		var fixedProgram = CreateProgram(fixedStatement, includeSystemGlobalization: false, includeInvariantUsing: true);

		VerifyCSharpDiagnostic(invalidProgram, CreateDiagnostic(invalidProgram, "int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)"));
		VerifyCSharpFix(invalidProgram, fixedProgram);
	}

	[Test]
	public void TryParseDiscardIsDiagnosedButNotFixed()
	{
		const string statement = """
			if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
				return;
			""";

		VerifyDiagnosticWithoutFix(statement, "int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)");
	}

	[Test]
	public void TryParseStatementIsDiagnosedButNotFixed()
	{
		const string statement = "int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value);";

		VerifyDiagnosticWithoutFix(statement, "int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)");
	}

	[Test]
	public void TryParseAndConditionIsDiagnosedButNotFixed()
	{
		const string statement = """
			if (boolValue && int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
				GC.KeepAlive(value);
			""";

		VerifyDiagnosticWithoutFix(statement, "int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)");
	}

	[Test]
	public void TryParseOrConditionIsDiagnosedButNotFixed()
	{
		const string statement = """
			if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || boolValue)
				GC.KeepAlive(value);
			""";

		VerifyDiagnosticWithoutFix(statement, "int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)");
	}

	[Test]
	public void StaticAndAliasUsingsAreRemovedByFix()
	{
		const string invalidStatement = """
			if (TryParse(input, NS.Integer, CI.InvariantCulture, out var value))
				GC.KeepAlive(value);
			""";
		const string fixedStatement = """
			if (InvariantConvert.TryParseInt32(input) is { } value)
				GC.KeepAlive(value);
			""";
		var invalidProgram = CreateProgram(
			invalidStatement,
			includeSystemGlobalization: false,
			extraUsings: "using CI = System.Globalization.CultureInfo;\nusing NS = System.Globalization.NumberStyles;\nusing static System.Int32;");
		var fixedProgram = CreateProgram(fixedStatement, includeSystemGlobalization: false, includeInvariantUsing: true);

		VerifyCSharpDiagnostic(invalidProgram, CreateDiagnostic(invalidProgram, "TryParse(input, NS.Integer, CI.InvariantCulture, out var value)"));
		VerifyCSharpFix(invalidProgram, fixedProgram);
	}

	[Test]
	public void StaticCultureAndNumberStylesUsingsAreRemovedByFix()
	{
		const string invalidStatement = """
			if (int.TryParse(input, Integer, InvariantCulture, out var value))
				GC.KeepAlive(value);
			""";
		const string fixedStatement = """
			if (InvariantConvert.TryParseInt32(input) is { } value)
				GC.KeepAlive(value);
			""";
		var invalidProgram = CreateProgram(
			invalidStatement,
			includeSystemGlobalization: false,
			extraUsings: "using static System.Globalization.CultureInfo;\nusing static System.Globalization.NumberStyles;");
		var fixedProgram = CreateProgram(fixedStatement, includeSystemGlobalization: false, includeInvariantUsing: true);

		VerifyCSharpDiagnostic(invalidProgram, CreateDiagnostic(invalidProgram, "int.TryParse(input, Integer, InvariantCulture, out var value)"));
		VerifyCSharpFix(invalidProgram, fixedProgram);
	}

	[Test]
	public void TypeAliasUsingIsRemovedByFix()
	{
		const string invalidStatement = "var result = Integer.Parse(input, CultureInfo.InvariantCulture);";
		const string fixedStatement = "var result = InvariantConvert.ParseInt32(input);";
		var invalidProgram = CreateProgram(invalidStatement, extraUsings: "using Integer = System.Int32;");
		var fixedProgram = CreateProgram(fixedStatement, includeSystemGlobalization: false, includeInvariantUsing: true);

		VerifyCSharpDiagnostic(invalidProgram, CreateDiagnostic(invalidProgram, "Integer.Parse(input, CultureInfo.InvariantCulture)"));
		VerifyCSharpFix(invalidProgram, fixedProgram);
	}

	[Test]
	public void ExistingInvariantUsingIsNotDuplicated()
	{
		const string invalidStatement = "var result = int.Parse(input, CultureInfo.InvariantCulture);";
		const string fixedStatement = "var result = InvariantConvert.ParseInt32(input);";
		var invalidProgram = CreateProgram(invalidStatement, includeInvariantUsing: true);
		var fixedProgram = CreateProgram(fixedStatement, includeSystemGlobalization: false, includeInvariantUsing: true);

		VerifyCSharpDiagnostic(invalidProgram, CreateDiagnostic(invalidProgram, "int.Parse(input, CultureInfo.InvariantCulture)"));
		VerifyCSharpFix(invalidProgram, fixedProgram);
	}

	[Test]
	public void InvariantUsingIsInsertedBeforeOtherNonSystemUsings()
	{
		const string invalidStatement = "var result = int.Parse(input, CultureInfo.InvariantCulture);";
		const string fixedStatement = "var result = InvariantConvert.ParseInt32(input);";
		const string extraUsings = "using Microsoft.CodeAnalysis;";
		var invalidProgram = CreateProgram(invalidStatement, extraUsings: extraUsings);
		var fixedProgram = CreateProgram(fixedStatement, includeSystemGlobalization: false, includeInvariantUsing: true, extraUsings: extraUsings);

		VerifyCSharpDiagnostic(invalidProgram, CreateDiagnostic(invalidProgram, "int.Parse(input, CultureInfo.InvariantCulture)"));
		VerifyCSharpFix(invalidProgram, fixedProgram);
	}

	protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new InvariantConvertAnalyzer();

	protected override CodeFixProvider GetCSharpCodeFixProvider() => new InvariantConvertCodeFixProvider();

	private void VerifyDiagnosticWithoutFix(string statement, string diagnosticText)
	{
		var invalidProgram = CreateProgram(statement);

		VerifyCSharpDiagnostic(invalidProgram, CreateDiagnostic(invalidProgram, diagnosticText));
		VerifyCSharpFix(invalidProgram, invalidProgram);
	}

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
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", line, column)],
		};
	}

	private static string CreateProgram(string statement, bool includeInvariantConvert = true, bool includeSystemGlobalization = true,
		bool includeInvariantUsing = false, string extraUsings = "", string extraTypes = "")
	{
		var usings = "using System;";
		if (includeSystemGlobalization)
			usings += "\nusing System.Globalization;";
		if (includeInvariantUsing)
			usings += "\nusing Libronix.Utility.Invariant;";
		if (extraUsings.Length != 0)
			usings += "\n" + extraUsings;

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

				{{extraTypes}}
			}
			""";
	}

	private const string c_segmentTypes = """
		internal sealed class DataRecord
		{
			public Segment Segment { get; } = new();
		}

		internal sealed class Segment
		{
			public int Length { get; set; }

			public int Offset { get; set; }
		}

		internal sealed class ValueProvider
		{
			public int GetLength() => 0;

			public Segment GetSegment() => new();
		}
		""";

	private const string c_valueFieldTypes = """
		internal sealed class ValueFields
		{
			public int IntField;

			public long LongField;

			public double DoubleField;

			public bool BoolField;
		}
		""";

	private const string c_textProviderTypes = """
		internal sealed class TextProvider
		{
			public TextRecord GetText() => new();

			public string GetNullableText() => null!;
		}

		internal sealed class TextRecord
		{
			public string Value { get; } = "";
		}
		""";

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
