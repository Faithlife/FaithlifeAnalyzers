using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests;

[TestFixture]
internal sealed class OverloadWithStringComparisonTests : CodeFixVerifier
{
	[Test]
	public void ValidUsage()
	{
		const string program = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						"a".StartsWith("b", StringComparison.Ordinal);
					}
				}
			}
			""";
		VerifyCSharpDiagnostic(program);
	}

	[TestCase("""
		"a".StartsWith("b", true, CultureInfo.CurrentCulture);
		""")]
	[TestCase("""
		"a".EndsWith("b", true, CultureInfo.CurrentCulture);
		""")]
	[TestCase("""string.Compare("a", "b", true, CultureInfo.CurrentCulture);""")]
	[TestCase("""string.Compare("a", "b", CultureInfo.CurrentCulture, CompareOptions.None);""")]
	[TestCase("""string.Compare("a", 0, "b", 0, 1, true, CultureInfo.CurrentCulture);""")]
	[TestCase("""string.Compare("a", 0, "b", 0, 1, CultureInfo.CurrentCulture, CompareOptions.None);""")]
	public void ValidUsageWithCultureInfo(string expression)
	{
		string program = $$"""
			using System;
			using System.Globalization;

			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						{{expression}}
					}
				}
			}
			""";

		VerifyCSharpDiagnostic(program);
	}

	[Test]
	public void ValidUsageWithVariable()
	{
		const string program = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						var c = StringComparison.Ordinal;
						"a".StartsWith("b", c);
					}
				}
			}
			""";
		VerifyCSharpDiagnostic(program);
	}

	[Test]
	public void ValidUsageWithStaticField()
	{
		const string program = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						"a".StartsWith("b", s_comparison);
					}
					static readonly StringComparison s_comparison = StringComparison.CurrentCultureIgnoreCase;
				}
			}
			""";
		VerifyCSharpDiagnostic(program);
	}

	[Test]
	public void DiagnoseAndFixEndsWith()
	{
		const string program = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						"a".EndsWith("b");
					}
				}
			}
			""";
		var expected = new DiagnosticResult
		{
			Id = OverloadWithStringComparisonAnalyzer.UseStringComparisonDiagnosticId,
			Message = "Use an overload that takes a StringComparison",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 8, 8)],
		};

		VerifyCSharpDiagnostic(program, expected);

		const string fix = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						"a".EndsWith("b", StringComparison.Ordinal);
					}
				}
			}
			""";
		VerifyCSharpFix(program, fix, 0);
	}

	[Test]
	public void DiagnoseAndFixIndexOf()
	{
		const string program = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						"a".IndexOf("b", 0);
					}
				}
			}
			""";
		var expected = new DiagnosticResult
		{
			Id = OverloadWithStringComparisonAnalyzer.UseStringComparisonDiagnosticId,
			Message = "Use an overload that takes a StringComparison",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 8, 8)],
		};

		VerifyCSharpDiagnostic(program, expected);

		const string fix = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						"a".IndexOf("b", 0, StringComparison.Ordinal);
					}
				}
			}
			""";
		VerifyCSharpFix(program, fix, 0);
	}

	[Test]
	public void DiagnoseAndFixStartsWith()
	{
		const string program = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						"a".StartsWith("b");
					}
				}
			}
			""";
		var expected = new DiagnosticResult
		{
			Id = OverloadWithStringComparisonAnalyzer.UseStringComparisonDiagnosticId,
			Message = "Use an overload that takes a StringComparison",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 8, 8)],
		};

		VerifyCSharpDiagnostic(program, expected);

		const string fix = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						"a".StartsWith("b", StringComparison.Ordinal);
					}
				}
			}
			""";
		VerifyCSharpFix(program, fix, 0);
	}

	[Test]
	public void DiagnoseAndFixStaticStringEquals()
	{
		const string program = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						GC.KeepAlive(string.Equals("a", "b"));
					}
				}
			}
			""";
		var expected = new DiagnosticResult
		{
			Id = OverloadWithStringComparisonAnalyzer.AvoidStringEqualsDiagnosticId,
			Message = "Use operator== or a non-ordinal StringComparison",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 8, 24)],
		};

		VerifyCSharpDiagnostic(program, expected);

		const string fix = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						GC.KeepAlive("a" == "b");
					}
				}
			}
			""";
		VerifyCSharpFix(program, fix, 0);
	}

	[Test]
	public void DiagnoseAndFixStringEquals()
	{
		const string program = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						GC.KeepAlive("a".Equals("b"));
					}
				}
			}
			""";
		var expected = new DiagnosticResult
		{
			Id = OverloadWithStringComparisonAnalyzer.AvoidStringEqualsDiagnosticId,
			Message = "Use operator== or a non-ordinal StringComparison",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 8, 21)],
		};

		VerifyCSharpDiagnostic(program, expected);

		const string fix1 = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						GC.KeepAlive("a" == "b");
					}
				}
			}
			""";
		VerifyCSharpFix(program, fix1, 0);

		const string fix2 = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						GC.KeepAlive("a".Equals("b", StringComparison.OrdinalIgnoreCase));
					}
				}
			}
			""";
		VerifyCSharpFix(program, fix2, 1);
	}

	[Test]
	public void DiagnoseAndFixStringEqualsStringComparisonOrdinal()
	{
		const string program = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						GC.KeepAlive("a".Equals("b", StringComparison.Ordinal));
					}
				}
			}
			""";
		var expected = new DiagnosticResult
		{
			Id = OverloadWithStringComparisonAnalyzer.AvoidStringEqualsDiagnosticId,
			Message = "Use operator== or a non-ordinal StringComparison",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 8, 21)],
		};

		VerifyCSharpDiagnostic(program, expected);

		const string fix1 = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						GC.KeepAlive("a" == "b");
					}
				}
			}
			""";
		VerifyCSharpFix(program, fix1, 0);

		const string fix2 = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						GC.KeepAlive("a".Equals("b", StringComparison.OrdinalIgnoreCase));
					}
				}
			}
			""";
		VerifyCSharpFix(program, fix2, 1);
	}

	[Test]
	public void DiagnoseAndFixStaticStringEqualsStringComparisonOrdinal()
	{
		const string program = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						GC.KeepAlive(string.Equals("a", "b", StringComparison.Ordinal));
					}
				}
			}
			""";
		var expected = new DiagnosticResult
		{
			Id = OverloadWithStringComparisonAnalyzer.AvoidStringEqualsDiagnosticId,
			Message = "Use operator== or a non-ordinal StringComparison",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 8, 24)],
		};

		VerifyCSharpDiagnostic(program, expected);

		const string fix1 = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						GC.KeepAlive("a" == "b");
					}
				}
			}
			""";
		VerifyCSharpFix(program, fix1, 0);

		const string fix2 = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						GC.KeepAlive(string.Equals("a", "b", StringComparison.OrdinalIgnoreCase));
					}
				}
			}
			""";
		VerifyCSharpFix(program, fix2, 1);
	}

	[Test]
	public void CompareStringStringStringComparisonIsValid()
	{
		const string program = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						string.Compare("a", "b", StringComparison.OrdinalIgnoreCase);
					}
				}
			}
			""";
		VerifyCSharpDiagnostic(program);
	}

	[Test]
	public void CompareStringIntStringIntIntStringComparisonIsValid()
	{
		const string program = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						string.Compare("a", 0, "b", 0, 1, StringComparison.OrdinalIgnoreCase);
					}
				}
			}
			""";
		VerifyCSharpDiagnostic(program);
	}

	[Test]
	public void EndsWithCharIsValid()
	{
		const string program = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						"a".EndsWith('a');
					}
				}
			}
			""";
		VerifyCSharpDiagnostic(program);
	}

	[Test]
	public void EndsWithStringStringComparisonIsValid()
	{
		const string program = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						"a".IndexOf("b", StringComparison.OrdinalIgnoreCase);
					}
				}
			}
			""";
		VerifyCSharpDiagnostic(program);
	}

	[Test]
	public void EqualsObjectIsValid()
	{
		const string program = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						"a".Equals(default(object));
					}
				}
			}
			""";
		VerifyCSharpDiagnostic(program);
	}

	[Test]
	public void IndexOfCharIsValid()
	{
		const string program = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						"a".IndexOf('a');
					}
				}
			}
			""";
		VerifyCSharpDiagnostic(program);
	}

	[Test]
	public void IndexOfCharIntIsValid()
	{
		const string program = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						"a".IndexOf('a', 0);
					}
				}
			}
			""";
		VerifyCSharpDiagnostic(program);
	}

	[Test]
	public void IndexOfCharIntIntIsValid()
	{
		const string program = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						"a".IndexOf('a', 0, 1);
					}
				}
			}
			""";
		VerifyCSharpDiagnostic(program);
	}

	[Test]
	public void IndexOfStringStringComparisonIsValid()
	{
		const string program = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						"a".IndexOf("a", StringComparison.Ordinal);
					}
				}
			}
			""";
		VerifyCSharpDiagnostic(program);
	}

	[Test]
	public void IndexOfStringIntStringComparisonIsValid()
	{
		const string program = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						"a".IndexOf("a", 0, StringComparison.Ordinal);
					}
				}
			}
			""";
		VerifyCSharpDiagnostic(program);
	}

	[Test]
	public void IndexOfStringIntIntStringComparisonIsValid()
	{
		const string program = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						"a".IndexOf("a", 0, 1, StringComparison.Ordinal);
					}
				}
			}
			""";
		VerifyCSharpDiagnostic(program);
	}

	[Test]
	public void LastIndexOfCharIsValid()
	{
		const string program = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						"a".LastIndexOf('a');
					}
				}
			}
			""";
		VerifyCSharpDiagnostic(program);
	}

	[Test]
	public void LastIndexOfCharIntIsValid()
	{
		const string program = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						"a".LastIndexOf('a', 0);
					}
				}
			}
			""";
		VerifyCSharpDiagnostic(program);
	}

	[Test]
	public void LastIndexOfCharIntIntIsValid()
	{
		const string program = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						"a".LastIndexOf('a', 0, 1);
					}
				}
			}
			""";
		VerifyCSharpDiagnostic(program);
	}

	[Test]
	public void LastIndexOfStringStringComparisonIsValid()
	{
		const string program = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						"a".LastIndexOf("a", StringComparison.Ordinal);
					}
				}
			}
			""";
		VerifyCSharpDiagnostic(program);
	}

	[Test]
	public void LastIndexOfStringIntStringComparisonIsValid()
	{
		const string program = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						"a".LastIndexOf("a", 0, StringComparison.Ordinal);
					}
				}
			}
			""";
		VerifyCSharpDiagnostic(program);
	}

	[Test]
	public void LastIndexOfStringIntIntStringComparisonIsValid()
	{
		const string program = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						"a".LastIndexOf("a", 0, 1, StringComparison.Ordinal);
					}
				}
			}
			""";
		VerifyCSharpDiagnostic(program);
	}

	[Test]
	public void StartsWithCharIsValid()
	{
		const string program = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						"a".StartsWith('a');
					}
				}
			}
			""";
		VerifyCSharpDiagnostic(program);
	}

	[Test]
	public void StartsWithStringStringComparisonIsValid()
	{
		const string program = """
			using System;
			namespace ConsoleApplication1
			{
				class Program
				{
					static void Main(string[] args)
					{
						"a".StartsWith("b", StringComparison.CurrentCulture);
					}
				}
			}
			""";
		VerifyCSharpDiagnostic(program);
	}

	protected override CodeFixProvider GetCSharpCodeFixProvider()
	{
		return new OverloadWithStringComparisonCodeFixProvider();
	}

	protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
	{
		return new OverloadWithStringComparisonAnalyzer();
	}
}
