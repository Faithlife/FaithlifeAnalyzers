using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests;

[TestFixture]
internal sealed class NullableHasValueConditionalTests : CodeFixVerifier
{
	[Test]
	public void ValidNullConditional()
	{
		const string validProgram = """
			using System;

			namespace TestApplication
			{
				public class TestClass
				{
					public void TestMethod()
					{
						int? nullable = 5;
						var result = nullable?.ToString();
					}
				}
			}
			""";
		VerifyCSharpDiagnostic(validProgram);
	}

	[TestCase("int")]
	[TestCase("long")]
	public void InvalidNullableConditionalWithCast(string type)
	{
		string invalidProgram = $$"""
			using System;

			namespace TestApplication
			{
				public class TestClass
				{
					public void Method()
					{
						{{type}}? x = 5;
						var y = x.HasValue ? x.Value : ({{type}}?) null;
					}
				}
			}
			""";
		var expected = new DiagnosticResult
		{
			Id = NullableHasValueConditionalAnalyzer.DiagnosticId,
			Message = "Use null propagation",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 10, 12)],
		};

		VerifyCSharpDiagnostic(invalidProgram, expected);

		string fixedProgram = $$"""
			using System;

			namespace TestApplication
			{
				public class TestClass
				{
					public void Method()
					{
						{{type}}? x = 5;
						var y = x;
					}
				}
			}
			""";

		VerifyCSharpFix(invalidProgram, fixedProgram);
	}

	[Test]
	public void InvalidNullableConditionalSimple()
	{
		const string invalidProgram = """
			using System;

			namespace TestApplication
			{
				public class TestClass
				{
					public void TestMethod()
					{
						int? x = 5;
						var result = x.HasValue ? x.Value.ToString() : null;
					}
				}
			}
			""";
		var expected = new DiagnosticResult
		{
			Id = NullableHasValueConditionalAnalyzer.DiagnosticId,
			Message = "Use null propagation",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 10, 17)],
		};

		VerifyCSharpDiagnostic(invalidProgram, expected);

		const string fixedProgram = """
			using System;

			namespace TestApplication
			{
				public class TestClass
				{
					public void TestMethod()
					{
						int? x = 5;
						var result = x?.ToString();
					}
				}
			}
			""";

		VerifyCSharpFix(invalidProgram, fixedProgram, 0);
	}

	[Test]
	public void InvalidNullableConditionalWithChaining()
	{
		const string invalidProgram = """
			using System;

			namespace TestApplication
			{
				public struct Person
				{
					public string Name { get; set; }
				}

				public class TestClass
				{
					public void TestMethod()
					{
						Person? person = new Person { Name = "John" };
						var result = person.HasValue ? person.Value.Name.Length : (int?) null;
					}
				}
			}
			""";
		var expected = new DiagnosticResult
		{
			Id = NullableHasValueConditionalAnalyzer.DiagnosticId,
			Message = "Use null propagation",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 15, 17)],
		};

		VerifyCSharpDiagnostic(invalidProgram, expected);

		const string fixedProgram = """
			using System;

			namespace TestApplication
			{
				public struct Person
				{
					public string Name { get; set; }
				}

				public class TestClass
				{
					public void TestMethod()
					{
						Person? person = new Person { Name = "John" };
						var result = person?.Name.Length;
					}
				}
			}
			""";

		VerifyCSharpFix(invalidProgram, fixedProgram, 0);
	}

	[Test]
	public void InvalidNullableConditionalParameter()
	{
		const string invalidProgram = """
			using System;

			namespace TestApplication
			{
				public class TestClass
				{
					public void TestMethod(int? parameter)
					{
						var result = parameter.HasValue ? parameter.Value.ToString() : null;
					}
				}
			}
			""";
		var expected = new DiagnosticResult
		{
			Id = NullableHasValueConditionalAnalyzer.DiagnosticId,
			Message = "Use null propagation",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 9, 17)],
		};

		VerifyCSharpDiagnostic(invalidProgram, expected);

		const string fixedProgram = """
			using System;

			namespace TestApplication
			{
				public class TestClass
				{
					public void TestMethod(int? parameter)
					{
						var result = parameter?.ToString();
					}
				}
			}
			""";

		VerifyCSharpFix(invalidProgram, fixedProgram, 0);
	}

	[Test]
	public void InvalidNullableConditionalField()
	{
		const string invalidProgram = """
			using System;

			namespace TestApplication
			{
				public class TestClass
				{
					private int? field;

					public void TestMethod()
					{
						var result = field.HasValue ? field.Value.ToString() : null;
					}
				}
			}
			""";
		var expected = new DiagnosticResult
		{
			Id = NullableHasValueConditionalAnalyzer.DiagnosticId,
			Message = "Use null propagation",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 11, 17)],
		};

		VerifyCSharpDiagnostic(invalidProgram, expected);

		const string fixedProgram = """
			using System;

			namespace TestApplication
			{
				public class TestClass
				{
					private int? field;

					public void TestMethod()
					{
						var result = field?.ToString();
					}
				}
			}
			""";

		VerifyCSharpFix(invalidProgram, fixedProgram, 0);
	}

	[Test]
	public void ValidNullableReferenceType()
	{
		// should not trigger the diagnostic because it's for nullable reference types, not Nullable<T>
		const string validProgram = """
			#nullable enable
			using System;

			namespace TestApplication
			{
				public class TestClass
				{
					public void TestMethod()
					{
						string? str = "test";
						var result = str != null ? str.Length : 0;
					}
				}
			}
			""";
		VerifyCSharpDiagnostic(validProgram);
	}

	[Test]
	public void ValidDifferentVariable()
	{
		// should not trigger when different variables are used
		const string validProgram = """
			using System;

			namespace TestApplication
			{
				public class TestClass
				{
					public void TestMethod()
					{
						int? nullable1 = 5;
						int? nullable2 = 10;
						var result = nullable1.HasValue ? nullable2.Value.ToString() : null;
					}
				}
			}
			""";
		VerifyCSharpDiagnostic(validProgram);
	}

	[Test]
	public void ValidNonNullElse()
	{
		// should not trigger when else clause is not null
		const string validProgram = """
			using System;

			namespace TestApplication
			{
				public class TestClass
				{
					public void TestMethod()
					{
						int? nullable = 5;
						var result = nullable.HasValue ? nullable.Value.ToString() : "default";
					}
				}
			}
			""";
		VerifyCSharpDiagnostic(validProgram);
	}

	[Test]
	public void ValidNonValueAccess()
	{
		// should not trigger when not accessing .Value
		const string validProgram = """
			using System;

			namespace TestApplication
			{
				public class TestClass
				{
					public void TestMethod()
					{
						int? nullable = 5;
						var result = nullable.HasValue ? nullable.ToString() : null;
					}
				}
			}
			""";
		VerifyCSharpDiagnostic(validProgram);
	}

	[Test]
	public void CodeFixWorksForEnum()
	{
		const string invalidProgram = """
			using System;

			namespace TestApplication
			{
				public enum Mode
				{
					First,
					Second,
					Third
				}
				public class TestClass
				{
					public void TestMethod(Mode? mode)
					{
						var result = mode.HasValue ? mode.Value.ToString() : null;
					}
				}
			}
			""";
		var expected = new DiagnosticResult
		{
			Id = NullableHasValueConditionalAnalyzer.DiagnosticId,
			Message = "Use null propagation",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 15, 17)],
		};

		VerifyCSharpDiagnostic(invalidProgram, expected);

		const string fixedProgram = """
			using System;

			namespace TestApplication
			{
				public enum Mode
				{
					First,
					Second,
					Third
				}
				public class TestClass
				{
					public void TestMethod(Mode? mode)
					{
						var result = mode?.ToString();
					}
				}
			}
			""";

		VerifyCSharpFix(invalidProgram, fixedProgram, 0);
	}

	[Test]
	public void CodeFixWorksInMethodCall()
	{
		const string invalidProgram = """
			using System;

			namespace TestApplication
			{
				public class TestClass
				{
					public void TestMethod(int? value)
					{
						Method2(value.HasValue ? value.Value.ToString() : null);
					}
					public void Method2(string value)
					{
					}
				}
			}
			""";
		var expected = new DiagnosticResult
		{
			Id = NullableHasValueConditionalAnalyzer.DiagnosticId,
			Message = "Use null propagation",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 9, 12)],
		};

		VerifyCSharpDiagnostic(invalidProgram, expected);

		const string fixedProgram = """
			using System;

			namespace TestApplication
			{
				public class TestClass
				{
					public void TestMethod(int? value)
					{
						Method2(value?.ToString());
					}
					public void Method2(string value)
					{
					}
				}
			}
			""";

		VerifyCSharpFix(invalidProgram, fixedProgram, 0);
	}

	protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new NullableHasValueConditionalAnalyzer();

	protected override CodeFixProvider GetCSharpCodeFixProvider() => new NullableHasValueConditionalCodeFixProvider();
}
