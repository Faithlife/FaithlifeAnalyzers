using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests;

[TestFixture]
internal sealed class FormattableStringInvariantTests : CodeFixVerifier
{
	[TestCase("FormattableString.Invariant($\"text = ({value})\")")]
	[TestCase("System.FormattableString.Invariant($\"text = ({value})\")")]
	[TestCase("global::System.FormattableString.Invariant($\"text = ({value})\")")]
	[TestCase("FormattableString.Invariant((FormattableString) $\"text = ({value})\")")]
	[TestCase("FormattableString.Invariant(formattable)")]
	public void QualifiedCallsAreFixed(string invocation)
	{
		var invalidProgram = CreateProgram(invocation);
		var fixedProgram = CreateProgram(invocation.Replace("FormattableString.Invariant", "Invariant", StringComparison.Ordinal)
			.Replace("System.Invariant", "Invariant", StringComparison.Ordinal)
			.Replace("global::Invariant", "Invariant", StringComparison.Ordinal),
			$"using System;{Environment.NewLine}using static System.FormattableString;");

		VerifyCSharpDiagnostic(invalidProgram, CreateDiagnostic(invalidProgram, invocation));
		VerifyCSharpFix(invalidProgram, fixedProgram);
	}

	[Test]
	public void MultipleCallsAddOnlyOneUsing()
	{
		const string invalidProgram = """
			using System;
			using System.Text;

			internal static class TestClass
			{
				public static string Format(int value, FormattableString formattable) =>
					FormattableString.Invariant($"first = {value}") + FormattableString.Invariant(formattable);
			}
			""";
		const string fixedProgram = """
			using System;
			using System.Text;
			using static System.FormattableString;

			internal static class TestClass
			{
				public static string Format(int value, FormattableString formattable) =>
					Invariant($"first = {value}") + Invariant(formattable);
			}
			""";

		VerifyCSharpDiagnostic(invalidProgram,
			CreateDiagnostic(invalidProgram, "FormattableString.Invariant($\"first = {value}\")"),
			CreateDiagnostic(invalidProgram, "FormattableString.Invariant(formattable)"));
		VerifyCSharpFix(invalidProgram, fixedProgram);
	}

	[Test]
	public void ExistingStaticUsingIsNotDuplicated()
	{
		const string invalidProgram = """
			using System;
			using static System.FormattableString;

			internal static class TestClass
			{
				public static string Format(int value, FormattableString formattable) => FormattableString.Invariant($"text = {value}");
			}
			""";
		const string fixedProgram = """
			using System;
			using static System.FormattableString;

			internal static class TestClass
			{
				public static string Format(int value, FormattableString formattable) => Invariant($"text = {value}");
			}
			""";

		VerifyCSharpDiagnostic(invalidProgram, CreateDiagnostic(invalidProgram, "FormattableString.Invariant($\"text = {value}\")"));
		VerifyCSharpFix(invalidProgram, fixedProgram);
	}

	[Test]
	public void ExistingNamespaceStaticUsingIsNotDuplicated()
	{
		const string invalidProgram = """
			using System;

			namespace Example
			{
				using static System.FormattableString;

				internal static class TestClass
				{
					public static string Format(int value, FormattableString formattable) => FormattableString.Invariant($"text = {value}");
				}
			}
			""";
		const string fixedProgram = """
			using System;

			namespace Example
			{
				using static System.FormattableString;

				internal static class TestClass
				{
					public static string Format(int value, FormattableString formattable) => Invariant($"text = {value}");
				}
			}
			""";

		VerifyCSharpDiagnostic(invalidProgram, CreateDiagnostic(invalidProgram, "FormattableString.Invariant($\"text = {value}\")"));
		VerifyCSharpFix(invalidProgram, fixedProgram);
	}

	[Test]
	public void AliasIsResolvedBySymbol()
	{
		var invalidProgram = CreateProgram("FS.Invariant($\"text = {value}\")", $"using System;{Environment.NewLine}using FS = System.FormattableString;");
		var fixedProgram = CreateProgram("Invariant($\"text = {value}\")",
			$"using System;{Environment.NewLine}using static System.FormattableString;");

		VerifyCSharpDiagnostic(invalidProgram, CreateDiagnostic(invalidProgram, "FS.Invariant($\"text = {value}\")"));
		VerifyCSharpFix(invalidProgram, fixedProgram);
	}

	[Test]
	public void NoUsingsAddsStaticUsingAtTop()
	{
		const string invalidProgram = """
			internal static class TestClass
			{
				public static string Format(int value) => System.FormattableString.Invariant($"text = {value}");
			}
			""";
		const string fixedProgram = """
			using static System.FormattableString;

			internal static class TestClass
			{
				public static string Format(int value) => Invariant($"text = {value}");
			}
			""";

		VerifyCSharpDiagnostic(invalidProgram, CreateDiagnostic(invalidProgram, "System.FormattableString.Invariant($\"text = {value}\")"));
		VerifyCSharpFix(invalidProgram, fixedProgram);
	}

	[Test]
	public void GlobalUsingIsKeptBeforeTheStaticUsing()
	{
		const string invalidProgram = """
			global using System;

			internal static class TestClass
			{
				public static string Format(int value, FormattableString formattable) => FormattableString.Invariant($"text = {value}");
			}
			""";
		const string fixedProgram = """
			global using System;
			using static System.FormattableString;

			internal static class TestClass
			{
				public static string Format(int value, FormattableString formattable) => Invariant($"text = {value}");
			}
			""";

		VerifyCSharpDiagnostic(invalidProgram, CreateDiagnostic(invalidProgram, "FormattableString.Invariant($\"text = {value}\")"));
		VerifyCSharpFix(invalidProgram, fixedProgram);
	}

	[Test]
	public void FileHeaderStaysAtTheTop()
	{
		const string invalidProgram = """
			// File header
			internal static class TestClass
			{
				public static string Format(int value) => System.FormattableString.Invariant($"text = {value}");
			}
			""";
		const string fixedProgram = """
			// File header
			using static System.FormattableString;

			internal static class TestClass
			{
				public static string Format(int value) => Invariant($"text = {value}");
			}
			""";

		VerifyCSharpDiagnostic(invalidProgram, CreateDiagnostic(invalidProgram, "System.FormattableString.Invariant($\"text = {value}\")"));
		VerifyCSharpFix(invalidProgram, fixedProgram);
	}

	[Test]
	public void FileHeaderIsPreservedWhenOldUsingIsRemoved()
	{
		const string invalidProgram = """
			// File header
			using System;

			internal static class TestClass
			{
				public static string Format(int value) => FormattableString.Invariant($"text = {value}");
			}
			""";
		const string fixedProgram = """
			// File header
			using static System.FormattableString;

			internal static class TestClass
			{
				public static string Format(int value) => Invariant($"text = {value}");
			}
			""";

		VerifyCSharpDiagnostic(invalidProgram, CreateDiagnostic(invalidProgram, "FormattableString.Invariant($\"text = {value}\")"));
		VerifyCSharpFix(invalidProgram, fixedProgram);
	}

	[Test]
	public void GlobalQualifiedStaticUsingIsNotDuplicated()
	{
		const string invalidProgram = """
			using System;
			using static global::System.FormattableString;

			internal static class TestClass
			{
				public static string Format(int value, FormattableString formattable) => FormattableString.Invariant($"text = {value}");
			}
			""";
		const string fixedProgram = """
			using System;
			using static global::System.FormattableString;

			internal static class TestClass
			{
				public static string Format(int value, FormattableString formattable) => Invariant($"text = {value}");
			}
			""";

		VerifyCSharpDiagnostic(invalidProgram, CreateDiagnostic(invalidProgram, "FormattableString.Invariant($\"text = {value}\")"));
		VerifyCSharpFix(invalidProgram, fixedProgram);
	}

	[Test]
	public void LocalInvariantMethodPreventsUnsafeFix()
	{
		const string program = """
			using System;

			internal static class TestClass
			{
				public static string Format(int value) => FormattableString.Invariant($"text = {value}");

				private static string Invariant(FormattableString value) => "";
			}
			""";

		VerifyCSharpDiagnostic(program, CreateDiagnostic(program, "FormattableString.Invariant($\"text = {value}\")"));
		VerifyCSharpFix(program, program);
	}

	[Test]
	public void CommentInQualifiedNamePreventsLossyFix()
	{
		const string program = """
			using System;

			internal static class TestClass
			{
				public static string Format(int value) => FormattableString /* keep */ .Invariant($"text = {value}");
			}
			""";

		VerifyCSharpDiagnostic(program, CreateDiagnostic(program, "FormattableString /* keep */ .Invariant($\"text = {value}\")"));
		VerifyCSharpFix(program, program);
	}

	[Test]
	public void OtherInvariantMethodsAndUnqualifiedCallsAreIgnored()
	{
		const string validProgram = """
			using System;
			using static System.FormattableString;

			internal static class TestClass
			{
				public static string Format(int value)
				{
					var formatted = Invariant($"text = {value}");
					return Other.Invariant(formatted);
				}

				private static class Other
				{
					public static string Invariant(string text) => text;
				}
			}
			""";
		VerifyCSharpDiagnostic(validProgram);
	}

	private static string CreateProgram(string invocation, string usings = "using System;") => $$"""
		{{usings}}

		internal static class TestClass
		{
			public static string Format(int value, FormattableString formattable) => {{invocation}};
		}
		""";

	private static DiagnosticResult CreateDiagnostic(string source, string text)
	{
		var index = source.IndexOf(text, StringComparison.Ordinal);
		Assert.That(index, Is.GreaterThanOrEqualTo(0));

		var line = 1;
		var column = 1;
		foreach (var character in source.Substring(0, index))
		{
			if (character == '\n')
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
			Id = FormattableStringInvariantAnalyzer.DiagnosticId,
			Message = "Use Invariant with a static import of System.FormattableString",
			Severity = DiagnosticSeverity.Info,
			Locations = [new DiagnosticResultLocation("Test0.cs", line, column)],
		};
	}

	protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new FormattableStringInvariantAnalyzer();

	protected override CodeFixProvider GetCSharpCodeFixProvider() => new FormattableStringInvariantCodeFixProvider();
}
