using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests
{
		[TestFixture]
		public class OverloadWithStringComparisonTests : CodeFixVerifier
		{
				[Test]
				public void ValidUsage()
				{
						const string program = @"using System;
namespace ConsoleApplication1
{
	class Program
	{
		static void Main(string[] args)
		{
			""a"".StartsWith(""b"", StringComparison.Ordinal);
		}
	}
}
";
						VerifyCSharpDiagnostic(program);
				}

				[Test]
				public void ValidUsageWithVariable()
				{
						const string program = @"using System;
namespace ConsoleApplication1
{
	class Program
	{
		static void Main(string[] args)
		{
			var c = StringComparison.Ordinal;
			""a"".StartsWith(""b"", c);
		}
	}
}
";
						VerifyCSharpDiagnostic(program);
				}

				[Test]
				public void ValidUsageWithStaticField()
				{
						const string program = @"using System;
namespace ConsoleApplication1
{
	class Program
	{
		static void Main(string[] args)
		{
			""a"".StartsWith(""b"", s_comparison);
		}
		static readonly StringComparison s_comparison = StringComparison.CurrentCultureIgnoreCase;
	}
}
";
						VerifyCSharpDiagnostic(program);
				}

				[Test]
				public void DiagnoseAndFixStartsWith()
				{
						const string program = @"using System;
namespace ConsoleApplication1
{
	class Program
	{
		static void Main(string[] args)
		{
			""a"".StartsWith(""b"");
		}
	}
}
";
						var expected = new DiagnosticResult
						{
								Id = OverloadWithStringComparisonAnalyzer.DiagnosticId,
								Message = "Use an overload that takes a StringComparison.",
								Severity = DiagnosticSeverity.Warning,
								Locations = new[] { new DiagnosticResultLocation("Test0.cs", 8, 4) },
						};

						VerifyCSharpDiagnostic(program, expected);

						const string fix = @"using System;
namespace ConsoleApplication1
{
	class Program
	{
		static void Main(string[] args)
		{
			""a"".StartsWith(""b"", StringComparison.Ordinal);
		}
	}
}
";
						VerifyCSharpFix(program, fix, 0);
				}

				[Test]
				public void DiagnoseAndFixStringEquals()
				{
						const string program = @"using System;
namespace ConsoleApplication1
{
	class Program
	{
		static void Main(string[] args)
		{
			string.Equals(""a"", ""b"");
		}
	}
}
";
						var expected = new DiagnosticResult
						{
								Id = OverloadWithStringComparisonAnalyzer.DiagnosticId,
								Message = "Use an overload that takes a StringComparison.",
								Severity = DiagnosticSeverity.Warning,
								Locations = new[] { new DiagnosticResultLocation("Test0.cs", 8, 4) },
						};

						VerifyCSharpDiagnostic(program, expected);

						const string fix = @"using System;
namespace ConsoleApplication1
{
	class Program
	{
		static void Main(string[] args)
		{
			string.Equals(""a"", ""b"", StringComparison.Ordinal);
		}
	}
}
";
						VerifyCSharpFix(program, fix, 0);
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
}
