using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests;

[TestFixture]
public class GetOrAddValueTests : CodeFixVerifier
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
			var x = new Dictionary<int, List<int>>();
			x.GetOrAddValue(0);
		}
	}
}";

		VerifyCSharpDiagnostic(validProgram);
	}

	[TestCase("localDictionary.GetOrAddValue(0);")]
	[TestCase("DictionaryFromFunction().GetOrAddValue(0);")]
	[TestCase("DictionaryFromProperty.GetOrAddValue(0);")]
	public void InvalidUsage(string invalidCall)
	{
		var brokenProgram = c_preamble + @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static void UtilityMethod()
		{
			var localDictionary = new ConcurrentDictionary<int, List<int>>();
			" + invalidCall + @"
		}

		public static ConcurrentDictionary<int, List<int>> DictionaryFromFunction() => new ConcurrentDictionary<int, List<int>>();

		public static ConcurrentDictionary<int, List<int>> DictionaryFromProperty => new ConcurrentDictionary<int, List<int>>();
	}
}";

		var expected = new DiagnosticResult
		{
			Id = GetOrAddValueAnalyzer.DiagnosticId,
			Message = "GetOrAddValue() is not threadsafe and should not be used with ConcurrentDictionary; use GetOrAdd() instead",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", s_preambleLength + 8, invalidCall.Length - "GetOrAddValue".Length)],
		};

		VerifyCSharpDiagnostic(brokenProgram, expected);
	}

	[TestCase("localDictionary?.GetOrAddValue(0);")]
	[TestCase("DictionaryFromFunction()?.GetOrAddValue(0);")]
	[TestCase("DictionaryFromProperty?.GetOrAddValue(0);")]
	public void InvalidNullableUsage(string invalidCall)
	{
		var brokenProgram = c_preamble + @"
namespace TestApplication
{
	internal static class TestClass
	{
		public static void UtilityMethod()
		{
			var localDictionary = new ConcurrentDictionary<int, List<int>>();
			" + invalidCall + @"
		}

		public static ConcurrentDictionary<int, List<int>> DictionaryFromFunction() => new ConcurrentDictionary<int, List<int>>();

		public static ConcurrentDictionary<int, List<int>> DictionaryFromProperty => new ConcurrentDictionary<int, List<int>>();
	}
}";

		var expected = new DiagnosticResult
		{
			Id = GetOrAddValueAnalyzer.DiagnosticId,
			Message = "GetOrAddValue() is not threadsafe and should not be used with ConcurrentDictionary; use GetOrAdd() instead",
			Severity = DiagnosticSeverity.Warning,
			Locations = [new DiagnosticResultLocation("Test0.cs", s_preambleLength + 8, invalidCall.Length - "GetOrAddValue".Length)],
		};

		VerifyCSharpDiagnostic(brokenProgram, expected);
	}

	protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new GetOrAddValueAnalyzer();

	private const string c_preamble = @"using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Libronix.Utility;

namespace Libronix.Utility
{
	public static class DictionaryUtility
	{
		public static TValue GetOrAddValue<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key) where TValue : new() => throw new NotImplementedException();
		public static TValue GetOrAddValue<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TValue> creator) => throw new NotImplementedException();
		public static TValue GetOrAddValue<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, Func<TKey, TValue> creator) => throw new NotImplementedException();
	}
}
";

	private static readonly int s_preambleLength = c_preamble.Split('\n').Length;
}
