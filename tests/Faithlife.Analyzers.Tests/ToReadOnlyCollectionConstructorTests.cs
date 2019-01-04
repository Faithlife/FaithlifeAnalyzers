using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests
{
	[TestFixture]
	public class ToReadOnlyCollectionConstructorTests : CodeFixVerifier
	{
		[Test]
		public void EnumerationIsValid()
		{
			const string validProgram = c_preamble + @"
namespace TestApplication
{
	public class TestClass
	{
		public TestClass(IEnumerable<int> args)
		{
			foreach (var arg in args.ToReadOnlyCollection())
			{
			}
		}
	}
}";
			VerifyCSharpDiagnostic(validProgram);
		}

		[Test]
		public void VariableDeclarationIsValid()
		{
			const string validProgram = c_preamble + @"
namespace TestApplication
{
	public class TestClass
	{
		public TestClass(IEnumerable<int> args)
		{
			var local = args.ToReadOnlyCollection();
		}
	}
}";
			VerifyCSharpDiagnostic(validProgram);
		}

		[Test]
		public void AssignLocalIsValid()
		{
			const string validProgram = c_preamble + @"
namespace TestApplication
{
	public class TestClass
	{
		public TestClass(IEnumerable<int> args)
		{
			ReadOnlyCollection<int> local;
			local = args.ToReadOnlyCollection();
		}
	}
}";
			VerifyCSharpDiagnostic(validProgram);
		}

		[Test]
		public void AssignField()
		{
			const string program = c_preamble + @"
namespace TestApplication
{
	public class TestClass
	{
		public TestClass(IEnumerable<int> args)
		{
			m_field = args.ToReadOnlyCollection();
		}

		ReadOnlyCollection<int> m_field;
	}
}";

			var expected = new DiagnosticResult
			{
				Id = ToReadOnlyCollectionAnalyzer.DiagnosticId,
				Message = "Avoid ToReadOnlyCollection in constructors.",
				Severity = DiagnosticSeverity.Warning,
				Locations = new[] { new DiagnosticResultLocation("Test0.cs", 20, 19) },
			};

			VerifyCSharpDiagnostic(program, expected);

			const string fix = c_preamble + @"
namespace TestApplication
{
	public class TestClass
	{
		public TestClass(IEnumerable<int> args)
		{
			m_field = args.ToList().AsReadOnly();
		}

		ReadOnlyCollection<int> m_field;
	}
}";

			VerifyCSharpFix(program, fix, 0);
		}

		[Test]
		public void AssignFieldConditionalNull()
		{
			const string program = c_preamble + @"
namespace TestApplication
{
	public class TestClass
	{
		public TestClass(IEnumerable<int> args)
		{
			m_field = args?.ToReadOnlyCollection();
		}

		ReadOnlyCollection<int> m_field;
	}
}";

			var expected = new DiagnosticResult
			{
				Id = ToReadOnlyCollectionAnalyzer.DiagnosticId,
				Message = "Avoid ToReadOnlyCollection in constructors.",
				Severity = DiagnosticSeverity.Warning,
				Locations = new[] { new DiagnosticResultLocation("Test0.cs", 20, 20) },
			};

			VerifyCSharpDiagnostic(program, expected);

			const string fix = c_preamble + @"
namespace TestApplication
{
	public class TestClass
	{
		public TestClass(IEnumerable<int> args)
		{
			m_field = args?.ToList().AsReadOnly();
		}

		ReadOnlyCollection<int> m_field;
	}
}";

			VerifyCSharpFix(program, fix, 0);
		}

		[Test]
		public void AssignProperty()
		{
			const string program = c_preamble + @"
namespace TestApplication
{
	public class TestClass
	{
		public TestClass(IEnumerable<int> args)
		{
			Property = args.ToReadOnlyCollection();
		}

		public ReadOnlyCollection<int> Property { get; }
	}
}";

			var expected = new DiagnosticResult
			{
				Id = ToReadOnlyCollectionAnalyzer.DiagnosticId,
				Message = "Avoid ToReadOnlyCollection in constructors.",
				Severity = DiagnosticSeverity.Warning,
				Locations = new[] { new DiagnosticResultLocation("Test0.cs", 20, 20) },
			};

			VerifyCSharpDiagnostic(program, expected);


			const string fix = c_preamble + @"
namespace TestApplication
{
	public class TestClass
	{
		public TestClass(IEnumerable<int> args)
		{
			Property = args.ToList().AsReadOnly();
		}

		public ReadOnlyCollection<int> Property { get; }
	}
}";

			VerifyCSharpFix(program, fix, 0);
		}

		protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new ToReadOnlyCollectionAnalyzer();

		protected override CodeFixProvider GetCSharpCodeFixProvider() => new ToReadOnlyCollectionCodeFixProvider();

		private const string c_preamble = @"using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Libronix.Utility;

namespace Libronix.Utility
{
	public static class EnumerableUtility
	{
		public static ReadOnlyCollection<T> ToReadOnlyCollection<T>(this IEnumerable<T> seq) => throw new NotImplementedException();
	}
}
";
	}
}
