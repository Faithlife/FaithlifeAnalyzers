using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests;

[TestFixture]
public class DbConnectorCommandInterpolatedTests : CodeFixVerifier
{
	[Test]
	public void ValidUsage()
	{
		const string validProgram = c_preamble + @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static void UtilityMethod()
		{
			var connector = new DbConnector();
			connector.Command(""valid"");
		}
	}
}";

		VerifyCSharpDiagnostic(validProgram);
	}

	[TestCase("connector.Command($\"invalid\");")]
	[TestCase("connector.Command($\"invalid\", (\"value\", true));")]
	public void InvalidUsage(string invalidCall)
	{
		var brokenProgram = c_preamble + @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static void UtilityMethod()
		{
			var connector = new DbConnector();
			" + invalidCall + @"
		}
	}
}";

		var expected = new DiagnosticResult
		{
			Id = DbConnectorCommandInterpolatedAnalyzer.DiagnosticId,
			Message = "Command should not be used with an interpolated string; use CommandFormat instead",
			Severity = DiagnosticSeverity.Warning,
			Locations = new[] { new DiagnosticResultLocation("Test0.cs", s_preambleLength + 8, 4) },
		};

		VerifyCSharpDiagnostic(brokenProgram, expected);
	}

	protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new DbConnectorCommandInterpolatedAnalyzer();

	private const string c_preamble = @"using System;
using Faithlife.Data;

namespace Faithlife.Data
{
	public class DbConnector
	{
		public object Command(string text) => throw new NotImplementedException();
		public object Command(string text, params (string Name, object Value)[] parameters) => throw new NotImplementedException();
	}
}
";

	private static readonly int s_preambleLength = c_preamble.Split('\n').Length;
}
