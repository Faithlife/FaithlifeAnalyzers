using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests;

[TestFixture]
internal sealed class LoggerInterpolatedStringTests : CodeFixVerifier
{
	[Test]
	public void ValidLiteral()
	{
		const string validProgram = $$"""
			{{c_preamble}}

			namespace TestApplication
			{
				internal static class TestClass
				{
					private static Logger m_logger = null!;

					public static void UtilityMethod(string x)
					{
						m_logger.Warn("Search query failed.");
					}
				}
			}
			""";

		VerifyCSharpDiagnostic(validProgram);
	}

	[Test]
	public void InvalidSimpleInterpolation()
	{
		const string invalidProgram = $$"""
			{{c_preamble}}

			namespace TestApplication
			{
				internal static class TestClass
				{
					private static Logger m_logger = null!;

					public static void UtilityMethod(string errorMessage)
					{
						m_logger.Warn($"Search query failed: {errorMessage}");
					}
				}
			}
			""";

		var expected = new DiagnosticResult
		{
			Id = LoggerInterpolatedStringAnalyzer.DiagnosticId,
			Message = "Replace interpolated string with composite format string arguments",
			Severity = DiagnosticSeverity.Info,
			Locations = [new("Test0.cs", 39, 18)],
		};

		VerifyCSharpDiagnostic(invalidProgram, expected);

		const string fixedProgram = $$"""
			{{c_preamble}}

			namespace TestApplication
			{
				internal static class TestClass
				{
					private static Logger m_logger = null!;

					public static void UtilityMethod(string errorMessage)
					{
						m_logger.Warn("Search query failed: {0}", errorMessage);
					}
				}
			}
			""";

		VerifyCSharpFix(invalidProgram, fixedProgram, 0);
	}

	[Test]
	public void InvalidWithFormatAndAlignment()
	{
		const string invalidProgram = $$"""
			{{c_preamble}}

			namespace TestApplication
			{
				internal static class TestClass
				{
					private static Logger m_logger = null!;

					public static void UtilityMethod(int code, double percent)
					{
						m_logger.Error($"Code={code,4:D2} Percent={percent:0.00}%");
					}
				}
			}
			""";

		var expected = new DiagnosticResult
		{
			Id = LoggerInterpolatedStringAnalyzer.DiagnosticId,
			Message = "Replace interpolated string with composite format string arguments",
			Severity = DiagnosticSeverity.Info,
			Locations = [new("Test0.cs", 39, 19)],
		};

		VerifyCSharpDiagnostic(invalidProgram, expected);

		const string fixedProgram = $$"""
			{{c_preamble}}

			namespace TestApplication
			{
				internal static class TestClass
				{
					private static Logger m_logger = null!;

					public static void UtilityMethod(int code, double percent)
					{
						m_logger.Error("Code={0,4:D2} Percent={1:0.00}%", code, percent);
					}
				}
			}
			""";

		VerifyCSharpFix(invalidProgram, fixedProgram, 0);
	}

	protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new LoggerInterpolatedStringAnalyzer();

	protected override CodeFixProvider GetCSharpCodeFixProvider() => new LoggerInterpolatedStringCodeFixProvider();

	private const string c_preamble = """
		using Libronix.Utility.Logging;

		namespace Libronix.Utility.Logging
		{
			public enum LogLevel
			{
				Debug,
				Info,
				Warn,
				Error,
				Fatal,
			}

			public class Logger
			{
				public void Debug(string message) => throw new System.NotImplementedException();
				public void Debug(string message, params object[] args) => throw new System.NotImplementedException();
				public void Info(string message) => throw new System.NotImplementedException();
				public void Info(string message, params object[] args) => throw new System.NotImplementedException();
				public void Warn(string message) => throw new System.NotImplementedException();
				public void Warn(string message, params object[] args) => throw new System.NotImplementedException();
				public void Error(string message) => throw new System.NotImplementedException();
				public void Error(string message, params object[] args) => throw new System.NotImplementedException();
				public void Fatal(string message) => throw new System.NotImplementedException();
				public void Fatal(string message, params object[] args) => throw new System.NotImplementedException();
				public void Write(LogLevel level, string message) => throw new System.NotImplementedException();
				public void Write(LogLevel level, string message, params object[] args) => throw new System.NotImplementedException();
			}
		}
		""";
}
