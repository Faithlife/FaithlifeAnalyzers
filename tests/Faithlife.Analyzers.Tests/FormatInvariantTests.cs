using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests;

[TestFixture]
public sealed class FormatInvariantTests : CodeFixVerifier
{
	[Test]
	public void ValidFormat()
	{
		const string validProgram = @"
namespace TestApplication
{
	public class TestClass
	{
		public TestClass()
		{
			var foo = ""foo"";
			Method($""{foo}"");
		}

		private void Method(string parameter)
		{
		}
	}
}";
		VerifyCSharpDiagnostic(validProgram);
	}

	[Test]
	public void InvalidFormat()
	{
		const string invalidProgram = @"using System;
using Libronix.Utility;

namespace Libronix.Utility
{
	public static class StringUtility
	{
		public static string FormatInvariant(this string format, params object[] args) => throw new NotImplementedException();
	}
}

namespace TestApplication
{
	public class TestClass
	{
		public TestClass()
		{
			var foo = ""foo"";
			Method(""{0}"".FormatInvariant(foo));
		}

		private void Method(string parameter)
		{
		}
	}
}";
		var expected = new DiagnosticResult
		{
			Id = FormatInvariantAnalyzer.DiagnosticId,
			Message = "Prefer string interpolation over FormatInvariant",
			Severity = DiagnosticSeverity.Info,
			Locations = new[] { new DiagnosticResultLocation("Test0.cs", 19, 11) },
		};

		VerifyCSharpDiagnostic(invalidProgram, expected);
	}

	protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new FormatInvariantAnalyzer();
}
