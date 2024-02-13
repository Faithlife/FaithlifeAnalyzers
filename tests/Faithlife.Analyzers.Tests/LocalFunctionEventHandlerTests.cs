using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests;

[TestFixture]
public class LocalFunctionEventHandlerTests : CodeFixVerifier
{
	[Test]
	public void LocalFunction()
	{
		const string program =
			"""
			using System;
			class Test
			{
				public event EventHandler OnFrob;
				public void Hook()
				{
					OnFrob += Local;
					OnFrob -= Local;
					void Local(object a, EventArgs b) { }
					OnFrob?.Invoke(this, EventArgs.Empty);
				}
			}
			""";
		VerifyCSharpDiagnostic(program, new DiagnosticResult
		{
			Id = LocalFunctionEventHandler.LocalFunctionDiagnosticId,
			Severity = DiagnosticSeverity.Warning,
			Message = "Local functions should probably not be used as event handlers (unless they are static).",
			Locations = new[] { new DiagnosticResultLocation("Test0.cs", 8, 3) },
		});
	}

	[Test]
	public void StaticLocalFunction()
	{
		const string program =
			"""
			using System;
			class Test
			{
				public event EventHandler OnFrob;
				public void Hook()
				{
					OnFrob += Local;
					OnFrob -= Local;
					static void Local(object a, EventArgs b) { }
					OnFrob?.Invoke(this, EventArgs.Empty);
				}
			}
			""";
		VerifyCSharpDiagnostic(program);
	}

	[Test]
	public void Lambda()
	{
		const string program =
			"""
			using System;
			class Test
			{
				public event EventHandler OnFrob;
				public void Hook()
				{
					OnFrob += (_, __) => { };
					OnFrob -= (_, __) => { };
					OnFrob?.Invoke(this, EventArgs.Empty);
				}
			}
			""";
		VerifyCSharpDiagnostic(program, new DiagnosticResult
		{
			Id = LocalFunctionEventHandler.LambdaDiagnosticId,
			Severity = DiagnosticSeverity.Error,
			Message = "Lambda expressions may not be used as event handlers.",
			Locations = new[] { new DiagnosticResultLocation("Test0.cs", 8, 3) },
		});
	}

	[Test]
	public void NonStaticInstanceMethod()
	{
		const string program =
			"""
			using System;
			class Test
			{
				public event EventHandler OnFrob;
				public void Hook()
				{
					OnFrob += Frobbed;
					OnFrob -= Frobbed;
					OnFrob?.Invoke(this, EventArgs.Empty);
				}
				private void Frobbed(object a, EventArgs b) { }
			}
			""";
		VerifyCSharpDiagnostic(program);
	}

	protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new LocalFunctionEventHandler();
}
