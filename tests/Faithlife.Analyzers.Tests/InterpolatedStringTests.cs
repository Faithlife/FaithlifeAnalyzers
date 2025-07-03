using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests;

[TestFixture]
public sealed class InterpolatedStringTests : DiagnosticVerifier
{
	[Test]
	public void ValidInterpolatedStrings()
	{
		const string validProgram = @"
namespace TestApplication
{
	public class TestClass
	{
		public TestClass()
		{
			string one = ""${hello}"";
			string two = $""{one}"";
		}
	}
}";
		VerifyCSharpDiagnostic(validProgram);
	}

	[Test]
	public void ValidDollarSign()
	{
		const string validProgram = @"
namespace TestApplication
{
	public class TestClass
	{
		public TestClass()
		{
			string one = ""one"";
			string two = $""{one} costs $0.00"";
		}
	}
}";
		VerifyCSharpDiagnostic(validProgram);
	}

	[Test]
	public void InvalidInterpolatedString()
	{
		const string invalidProgram = @"
namespace TestApplication
{
	public class TestClass
	{
		public TestClass()
		{
			string one = ""${hello}"";
			string two = $""${one}"";
		}
	}
}";
		VerifyCSharpDiagnostic(invalidProgram, new DiagnosticResult
		{
			Id = InterpolatedStringAnalyzer.DiagnosticIdDollar,
			Message = "Avoid using ${} in interpolated strings",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 9, 20)],
		});
	}

	[Test]
	public void InvalidInterpolatedStrings()
	{
		const string invalidProgram = @"
namespace TestApplication
{
	public class TestClass
	{
		public TestClass()
		{
			string one = ""${hello}"";
			string two = $""${one}${one}"";
		}
	}
}";
		VerifyCSharpDiagnostic(invalidProgram,
			new DiagnosticResult
			{
				Id = InterpolatedStringAnalyzer.DiagnosticIdDollar,
				Message = "Avoid using ${} in interpolated strings",
				Severity = DiagnosticSeverity.Warning,
				Locations = [new DiagnosticResultLocation("Test0.cs", 9, 20)],
			},
			new DiagnosticResult
			{
				Id = InterpolatedStringAnalyzer.DiagnosticIdDollar,
				Message = "Avoid using ${} in interpolated strings",
				Severity = DiagnosticSeverity.Warning,
				Locations = [new DiagnosticResultLocation("Test0.cs", 9, 26)],
			});
	}

	[Test]
	public void ConsecutiveInterpolatedStrings()
	{
		const string invalidProgram = @"
namespace TestApplication
{
	public class TestClass
	{
		public TestClass()
		{
			string one = ""${hello}"";
			string two = $""${one}{one}"";
		}
	}
}";
		VerifyCSharpDiagnostic(invalidProgram, new DiagnosticResult
		{
			Id = InterpolatedStringAnalyzer.DiagnosticIdDollar,
			Message = "Avoid using ${} in interpolated strings",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 9, 20)],
		});
	}

	[Test]
	public void UnnecessaryInterpolatedString()
	{
		const string invalidProgram = @"
namespace TestApplication
{
	public class TestClass
	{
		public TestClass()
		{
			string str = $""Hello World"";
		}
	}
}";
		VerifyCSharpDiagnostic(invalidProgram, new DiagnosticResult
		{
			Id = InterpolatedStringAnalyzer.DiagnosticIdUnnecessary,
			Message = "Avoid using an interpolated string where an equivalent literal string exists",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 8, 17)],
		});
	}

	[Test]
	public void EmptyInterpolatedString()
	{
		const string invalidProgram = @"
namespace TestApplication
{
	public class TestClass
	{
		public TestClass()
		{
			string str = $"""";
		}
	}
}";
		VerifyCSharpDiagnostic(invalidProgram, new DiagnosticResult
		{
			Id = InterpolatedStringAnalyzer.DiagnosticIdUnnecessary,
			Message = "Avoid using an interpolated string where an equivalent literal string exists",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 8, 17)],
		});
	}

	[Test]
	public void InterpolatedStringWithDebugAssert()
	{
		const string validProgram = """
			using System.Diagnostics;

			namespace TestApplication
			{
				public class TestClass
				{
					public TestClass()
					{
						var result = false;
						Debug.Assert(result, $"Assertion was {result}");
					}
				}
			}
			""";
		VerifyCSharpDiagnostic(validProgram);
	}

	[Test]
	public void UnnecessaryInterpolatedStringWithDebugAssert()
	{
		const string invalidProgram = """
			using System.Diagnostics;

			namespace TestApplication
			{
				public class TestClass
				{
					public void TestMethod()
					{
						var result = false;
						Debug.Assert(result, $"Assertion message");
					}
				}
			}
			""";

		VerifyCSharpDiagnostic(invalidProgram, new DiagnosticResult
		{
			Id = InterpolatedStringAnalyzer.DiagnosticIdUnnecessary,
			Message = "Avoid using an interpolated string where an equivalent literal string exists",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 10, 25)],
		});
	}

	[Test]
	public void InterpolatedStringWithStringBuilderAppend()
	{
		const string validProgram = """
			using System.Diagnostics;

			namespace TestApplication
			{
				public class TestClass
				{
					public void TestMethod()
					{
						var result = false;
						var sb = new System.Text.StringBuilder();
						sb.Append($"Result was {result}");
					}
				}
			}
			""";

		VerifyCSharpDiagnostic(validProgram);
	}

	protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new InterpolatedStringAnalyzer();
}
