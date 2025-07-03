using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests;

[TestFixture]
internal sealed class ConstantSwitchTests : CodeFixVerifier
{
	[Test]
	public void ValidSwitchExpression()
	{
		const string validProgram = @"using System;

namespace TestApplication
{
	public class TestClass
	{
		public TestClass()
		{
			int? i = 0;
			var zero = i switch
			{
				0 => true,
				1 => false,
				_ => throw new InvalidOperationException(),
			};
		}
	}
}";
		VerifyCSharpDiagnostic(validProgram);
	}

	[Test]
	public void InvalidSwitchExpression()
	{
		const string invalidProgram = @"using System;

namespace TestApplication
{
	public class TestClass
	{
		public TestClass()
		{
			int? i = 0;
			var zero = 0 switch
			{
				_ when i is 0 => true,
				_ when i is 1 => false,
				_ => throw new InvalidOperationException(),
			};
		}
	}
}";
		var expected = new DiagnosticResult
		{
			Id = ConstantSwitchAnalyzer.DiagnosticId,
			Message = "Do not use a constant value as the target of a switch expression",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", 10, 15)],
		};

		VerifyCSharpDiagnostic(invalidProgram, expected);
	}

	protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new ConstantSwitchAnalyzer();
}
