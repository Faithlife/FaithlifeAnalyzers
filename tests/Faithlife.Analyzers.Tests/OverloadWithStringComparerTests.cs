using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests;

[TestFixture]
public class OverloadWithStringComparerTests : CodeFixVerifier
{
	public enum Ordering
	{
		OrderBy,
		OrderByDescending,
		ThenBy,
		ThenByDescending,
	}

	[Test]
	public void OrderByStringWithoutComparer_Invalid([Values] Ordering ordering)
	{
		string code = GetExtensionMethodCode(ordering, @"new[] { ""A"", ""b"", ""C"" }", "x => x");
		VerifyInvalidExpression(code, code.IndexOf(GetMethodName(ordering), StringComparison.Ordinal));
	}

	[Test]
	public void OrderByStringWithoutComparerWithNullConditional_Invalid([Values] Ordering ordering)
	{
		string code = GetExtensionMethodCode(ordering, @"new[] { ""A"", ""b"", ""C"" }", "x => x", isNullConditional: true);
		VerifyInvalidExpression(code, code.IndexOf(GetMethodName(ordering), StringComparison.Ordinal));
	}

	[Test]
	public void OrderByStringWithComparer_Valid([Values] Ordering ordering)
	{
		string code = GetExtensionMethodCode(ordering, @"new[] { ""A"", ""b"", ""C"" }", "x => x", "StringComparer.Ordinal");
		VerifyValidExpression(code);
	}

	[Test]
	public void OrderByStringFromIntegerWithoutComparer_Invalid([Values] Ordering ordering)
	{
		string code = GetExtensionMethodCode(ordering, "new[] { 3, 2, 1 }", "x => x.ToString()");
		VerifyInvalidExpression(code, code.IndexOf(GetMethodName(ordering), StringComparison.Ordinal));
	}

	[Test]
	public void OrderByStringFromIntegerWithComparer_Valid([Values] Ordering ordering)
	{
		string code = GetExtensionMethodCode(ordering, "new[] { 3, 2, 1 }", "x => x.ToString()", "StringComparer.OrdinalIgnoreCase");
		VerifyValidExpression(code);
	}

	[Test]
	public void OrderByIntegerWithoutComparer_Valid([Values] Ordering ordering)
	{
		string code = GetExtensionMethodCode(ordering, "new[] { 3, 2, 1 }", "x => x");
		VerifyValidExpression(code);
	}

	[Test]
	public void ExplicitOrderByStringWithoutComparer_Invalid()
	{
		const string code = @"Enumerable.OrderBy(new[] { ""A"", ""b"", ""C"" }, x => x)";
		VerifyInvalidExpression(code, code.IndexOf("OrderBy", StringComparison.Ordinal));
	}

	[Test]
	public void ExplicitOrderByStringWithComparer_Valid()
	{
		VerifyValidExpression("Enumerable.OrderBy(new[] { 3, 2, 1 }, x => x.ToString(), StringComparer.CurrentCulture)");
	}

	[Test]
	public void LinqSyntaxOrderByString_Invalid([Values] Ordering ordering)
	{
		string code = GetLinqSyntaxCode(ordering, @"new[] { ""A"", ""b"", ""C"" }", "x");
		VerifyInvalidExpression(code, code.IndexOf("orderby", StringComparison.Ordinal));
	}

	[Test]
	public void LinqSyntaxOrderByStringFromInteger_Invalid([Values] Ordering ordering)
	{
		string code = GetLinqSyntaxCode(ordering, "new[] { 3, 2, 1 }", "x.ToString()");
		VerifyInvalidExpression(code, code.IndexOf("orderby", StringComparison.Ordinal));
	}

	[Test]
	public void LinqSyntaxOrderByStringWithComparer_Valid()
	{
		VerifyValidExpression(@"(from letter in new[] { ""A"", ""b"", ""C"" } select letter).OrderByDescending(x => x, StringComparer.OrdinalIgnoreCase)");
	}

	protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new OverloadWithStringComparerAnalyzer();

	private static string GetMethodName(Ordering ordering) => ordering.ToString();

	private static string GetExtensionMethodCode(Ordering ordering, string collection, string lambda, string? comparer = null, bool isNullConditional = false)
	{
		var parameter = comparer is null ? "" : $", {comparer}";
		var nullConditional = isNullConditional ? "?" : "";

		return ordering switch
		{
			Ordering.OrderBy => $"{collection}{nullConditional}.OrderBy({lambda}{parameter})",
			Ordering.OrderByDescending => $"{collection}{nullConditional}.OrderByDescending({lambda}{parameter})",
			Ordering.ThenBy => $"{collection}{nullConditional}.OrderBy(x => x.GetHashCode()).ThenBy({lambda}{parameter})",
			Ordering.ThenByDescending => $"{collection}{nullConditional}.OrderBy(x => x.GetHashCode()).ThenByDescending({lambda}{parameter})",
			_ => throw new ArgumentOutOfRangeException(nameof(ordering), ordering, null),
		};
	}

	private static string GetLinqSyntaxCode(Ordering ordering, string collection, string orderBy) =>
		ordering switch
		{
			Ordering.OrderBy => $"from x in {collection} orderby {orderBy} select x",
			Ordering.OrderByDescending => $"from x in {collection} orderby {orderBy} descending select x",
			Ordering.ThenBy => $"from x in {collection} orderby x.GetHashCode(), {orderBy} select x",
			Ordering.ThenByDescending => $"from x in {collection} orderby x.GetHashCode(), {orderBy} descending select x",
			_ => throw new ArgumentOutOfRangeException(nameof(ordering), ordering, null),
		};

	private void VerifyValidExpression(string source)
	{
		VerifyCSharpDiagnostic(c_linesBefore + c_beforeExpression + source + c_afterExpression + c_linesAfter);
	}

	private void VerifyInvalidExpression(string source, int columnOffset)
	{
		VerifyCSharpDiagnostic(c_linesBefore + c_beforeExpression + source + c_afterExpression + c_linesAfter,
			new DiagnosticResult
			{
				Id = OverloadWithStringComparerAnalyzer.UseStringComparerDiagnosticId,
				Message = "Use the overload that takes an IComparer<string>",
				Severity = DiagnosticSeverity.Warning,
				Locations =
				[
					new DiagnosticResultLocation("Test0.cs", s_lineNumber, s_columnNumber + columnOffset),
				],
			});
	}

	private const string c_linesBefore = @"
using System;
using System.Linq;

namespace TestApplication
{
	internal static class TestClass
	{
		public static void UtilityMethod()
		{
";

	private const string c_linesAfter = @"
		}
	}
}";

	private const string c_beforeExpression = "GC.KeepAlive(";

	private const string c_afterExpression = ");";

	private static readonly int s_lineNumber = c_linesBefore.Split('\n').Length;

	private static readonly int s_columnNumber = c_beforeExpression.Length + 1;
}
