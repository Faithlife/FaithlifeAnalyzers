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
		// This pattern is "unsubscribe the old handler; then subscribe the new handler".
		// This pattern is wrong because the old and new handlers are different instances.
		const string program =
			"""
			using System;
			class Test
			{
				public event EventHandler OnFrob;
				public void Hook()
				{
					OnFrob -= Local;
					OnFrob += Local;
					void Local(object a, EventArgs b) { }
					OnFrob?.Invoke(this, EventArgs.Empty);
				}
			}
			""";
		VerifyCSharpDiagnostic(program, new DiagnosticResult
		{
			Id = LocalFunctionEventHandler.LocalFunctionDiagnosticId,
			Severity = DiagnosticSeverity.Warning,
			Message = "Local function event handler",
			Locations = [new DiagnosticResultLocation("Test0.cs", 7, 3)],
		});
	}

	[Test]
	public void LocalFunctionReturningUnsubscribe()
	{
		// This pattern is "subscribe the new handler; then return some action to later unsubscribe the handler".
		// This pattern is correct because the same handler is being subscribed and unsubscribed.
		const string program =
			"""
			using System;
			class Test
			{
				public event EventHandler OnFrob;
				public Action Hook()
				{
					OnFrob += Local;
					void Local(object a, EventArgs b) { }
					OnFrob?.Invoke(this, EventArgs.Empty);
					return () => OnFrob -= Local;
				}
			}
			""";
		VerifyCSharpDiagnostic(program);
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
					OnFrob -= Local;
					OnFrob += Local;
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
			Message = "Lambda expression event handler",
			Locations = [new DiagnosticResultLocation("Test0.cs", 8, 3)],
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
					OnFrob -= Frobbed;
					OnFrob += Frobbed;
					OnFrob?.Invoke(this, EventArgs.Empty);
				}
				private void Frobbed(object a, EventArgs b) { }
			}
			""";
		VerifyCSharpDiagnostic(program);
	}

	protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new LocalFunctionEventHandler();
}
