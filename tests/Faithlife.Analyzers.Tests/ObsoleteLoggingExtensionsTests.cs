using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests;

[TestFixture]
internal sealed class ObsoleteLoggingExtensionsTests : CodeFixVerifier
{
	[Test]
	public void ValidILoggerMethod()
	{
		const string validProgram = """
			using Microsoft.Extensions.Logging;

			namespace Logos.Common.Logging
			{
				public static class Extensions
				{
					public static void Debug(this ILogger logger, string message) => throw new System.NotImplementedException();
				}
			}

			namespace Microsoft.Extensions.Logging
			{
				public interface ILogger { }

				public static class LoggerExtensions
				{
					public static void LogDebug(this ILogger logger, string message) => throw new System.NotImplementedException();
					public static void LogInformation(this ILogger logger, string message) => throw new System.NotImplementedException();
					public static void LogWarning(this ILogger logger, string message) => throw new System.NotImplementedException();
					public static void LogError(this ILogger logger, string message) => throw new System.NotImplementedException();
					public static void LogCritical(this ILogger logger, string message) => throw new System.NotImplementedException();
				}
			}

			namespace TestApplication
			{
				public class TestClass
				{
					private readonly Microsoft.Extensions.Logging.ILogger _logger;

					public void TestMethod()
					{
						_logger.LogDebug("Test message");
						_logger.LogInformation("Test message");
						_logger.LogWarning("Test message");
						_logger.LogError("Test message");
						_logger.LogCritical("Test message");
					}
				}
			}
			""";
		VerifyCSharpDiagnostic(validProgram);
	}

	[TestCase("Debug", "LogDebug")]
	[TestCase("Info", "LogInformation")]
	[TestCase("Warn", "LogWarning")]
	[TestCase("Error", "LogError")]
	[TestCase("Fatal", "LogCritical")]
	public void ObsoleteLoggingMethod(string obsoleteMethod, string replacementMethod)
	{
		var invalidProgram = $$"""
			using Logos.Common.Logging;
			using Microsoft.Extensions.Logging;

			namespace Logos.Common.Logging
			{
				public static class Extensions
				{
					public static void Debug(this ILogger logger, string message) => throw new System.NotImplementedException();
					public static void Info(this ILogger logger, string message) => throw new System.NotImplementedException();
					public static void Warn(this ILogger logger, string message) => throw new System.NotImplementedException();
					public static void Error(this ILogger logger, string message) => throw new System.NotImplementedException();
					public static void Fatal(this ILogger logger, string message) => throw new System.NotImplementedException();
				}
			}

			namespace Microsoft.Extensions.Logging
			{
				public interface ILogger { }
			}

			namespace TestApplication
			{
				public class TestClass
				{
					private readonly ILogger _logger;

					public void TestMethod()
					{
						_logger.{{obsoleteMethod}}("Test message");
					}
				}
			}
			""";

		var expected = new DiagnosticResult
		{
			Id = ObsoleteLoggingExtensionsAnalyzer.DiagnosticId,
			Message = $"Replace obsolete '{obsoleteMethod}' method with '{replacementMethod}'",
			Severity = DiagnosticSeverity.Error,
			Locations = [new DiagnosticResultLocation("Test0.cs", 29, 4)],
		};

		VerifyCSharpDiagnostic(invalidProgram, expected);

		var fixedProgram = $$"""
			using Microsoft.Extensions.Logging;

			namespace Logos.Common.Logging
			{
				public static class Extensions
				{
					public static void Debug(this ILogger logger, string message) => throw new System.NotImplementedException();
					public static void Info(this ILogger logger, string message) => throw new System.NotImplementedException();
					public static void Warn(this ILogger logger, string message) => throw new System.NotImplementedException();
					public static void Error(this ILogger logger, string message) => throw new System.NotImplementedException();
					public static void Fatal(this ILogger logger, string message) => throw new System.NotImplementedException();
				}
			}

			namespace Microsoft.Extensions.Logging
			{
				public interface ILogger { }
			}

			namespace TestApplication
			{
				public class TestClass
				{
					private readonly ILogger _logger;

					public void TestMethod()
					{
						_logger.{{replacementMethod}}("Test message");
					}
				}
			}
			""";

		VerifyCSharpFix(invalidProgram, fixedProgram, 0);
	}

	[Test]
	public void ObsoleteLoggingMethodWithFormattedMessage()
	{
		const string invalidProgram = """
			using Logos.Common.Logging;
			using Microsoft.Extensions.Logging;

			namespace Logos.Common.Logging
			{
				public static class Extensions
				{
					public static void Debug(this ILogger logger, string format, params object[] args) => throw new System.NotImplementedException();
				}
			}

			namespace Microsoft.Extensions.Logging
			{
				public interface ILogger { }
			}

			namespace TestApplication
			{
				public class TestClass
				{
					private readonly ILogger _logger;

					public void TestMethod()
					{
						_logger.Debug("Value: {0}", 42);
					}
				}
			}
			""";

		var expected = new DiagnosticResult
		{
			Id = ObsoleteLoggingExtensionsAnalyzer.DiagnosticId,
			Message = "Replace obsolete 'Debug' method with 'LogDebug'",
			Severity = DiagnosticSeverity.Error,
			Locations = [new DiagnosticResultLocation("Test0.cs", 25, 4)],
		};

		VerifyCSharpDiagnostic(invalidProgram, expected);

		const string fixedProgram = """
			using Microsoft.Extensions.Logging;

			namespace Logos.Common.Logging
			{
				public static class Extensions
				{
					public static void Debug(this ILogger logger, string format, params object[] args) => throw new System.NotImplementedException();
				}
			}

			namespace Microsoft.Extensions.Logging
			{
				public interface ILogger { }
			}

			namespace TestApplication
			{
				public class TestClass
				{
					private readonly ILogger _logger;

					public void TestMethod()
					{
						_logger.LogDebug("Value: {0}", 42);
					}
				}
			}
			""";

		VerifyCSharpFix(invalidProgram, fixedProgram, 0);
	}

	[Test]
	public void MultipleObsoleteMethods()
	{
		const string invalidProgram = """
			using Logos.Common.Logging;
			using Microsoft.Extensions.Logging;

			namespace Logos.Common.Logging
			{
				public static class Extensions
				{
					public static void Debug(this ILogger logger, string message) => throw new System.NotImplementedException();
					public static void Info(this ILogger logger, string message) => throw new System.NotImplementedException();
					public static void Error(this ILogger logger, string message) => throw new System.NotImplementedException();
				}
			}

			namespace Microsoft.Extensions.Logging
			{
				public interface ILogger { }
			}

			namespace TestApplication
			{
				public class TestClass
				{
					private readonly ILogger _logger;

					public void TestMethod()
					{
						_logger.Debug("Debug message");
						_logger.Info("Info message");
						_logger.Error("Error message");
					}
				}
			}
			""";

		var expectedResults = new[]
		{
			new DiagnosticResult
			{
				Id = ObsoleteLoggingExtensionsAnalyzer.DiagnosticId,
				Message = "Replace obsolete 'Debug' method with 'LogDebug'",
				Severity = DiagnosticSeverity.Error,
				Locations = [new DiagnosticResultLocation("Test0.cs", 27, 4)],
			},
			new DiagnosticResult
			{
				Id = ObsoleteLoggingExtensionsAnalyzer.DiagnosticId,
				Message = "Replace obsolete 'Info' method with 'LogInformation'",
				Severity = DiagnosticSeverity.Error,
				Locations = [new DiagnosticResultLocation("Test0.cs", 28, 4)],
			},
			new DiagnosticResult
			{
				Id = ObsoleteLoggingExtensionsAnalyzer.DiagnosticId,
				Message = "Replace obsolete 'Error' method with 'LogError'",
				Severity = DiagnosticSeverity.Error,
				Locations = [new DiagnosticResultLocation("Test0.cs", 29, 4)],
			},
		};

		VerifyCSharpDiagnostic(invalidProgram, expectedResults);
	}

	protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new ObsoleteLoggingExtensionsAnalyzer();

	protected override CodeFixProvider GetCSharpCodeFixProvider() => new ObsoleteLoggingExtensionsCodeFixProvider();
}
