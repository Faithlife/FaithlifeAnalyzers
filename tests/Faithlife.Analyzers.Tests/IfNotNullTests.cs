using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests
{
	[TestFixture]
	public sealed class IfNotNullTests : CodeFixVerifier
	{
		// An expression evaluating to a value type without a supplied default
		// needs to use the null coalescing to maintain the correct type.
		[TestCase(
			"new ReferenceThing()",
			"var result = possiblyNull.IfNotNull(x => x.ValueTypeProperty);",
			"var result = possiblyNull?.ValueTypeProperty ?? default(int);")]
		// Complex expressions of a value type still need the null coalescing operator
		[TestCase(
			"new ReferenceThing()",
			"var result = possiblyNull.IfNotNull(x => x.CalculateValue().ValueTypeProperty);",
			"var result = possiblyNull?.CalculateValue().ValueTypeProperty ?? default(int);")]
		// Most usages of the default parameter cannot be combined with the conditional operator,
		// but value types are fine.
		[TestCase(
			"new ReferenceThing()",
			"var result = possiblyNull.IfNotNull(x => x.ValueTypeProperty, () => 0);",
			"var result = possiblyNull?.ValueTypeProperty ?? 0;")]
		[TestCase(
			"new ReferenceThing()",
			"var result = IfNotNullExtensionMethod.IfNotNull(possiblyNull, x => x.ValueTypeProperty, () => 0);",
			"var result = possiblyNull?.ValueTypeProperty ?? 0;")]
		// A method invocation can be performed using the conditional operator.
		[TestCase(
			"new ReferenceThing()",
			"var result = possiblyNull.IfNotNull(x => x.CalculateValue());",
			"var result = possiblyNull?.CalculateValue();")]
		// The presences of conditional operators within the expression shouldn't prevent
		// the use of a new conditional operator.
		[TestCase(
			"new ReferenceThing()",
			"var result = possiblyNull.IfNotNull(x => x.RecursiveProperty?.CalculateValue());",
			"var result = possiblyNull?.RecursiveProperty?.CalculateValue();")]
		// Parentheses at the root will prevent the use of the conditional operator because
		// there are some weird edge cases, but it should still fall back to pattern matching
		// just fine.
		[TestCase(
			"new ReferenceThing()",
			"var result = possiblyNull.IfNotNull(x => (x.CalculateValue()));",
			"var result = possiblyNull is ReferenceThing x ? (x.CalculateValue()) : default(ReferenceThing);")]
		[TestCase(
			"new ReferenceThing()",
			"var result = possiblyNull.IfNotNull(x => (x).CalculateValue());",
			"var result = possiblyNull is ReferenceThing x ? (x).CalculateValue() : default(ReferenceThing);")]
		[TestCase(
			"new ReferenceThing()",
			"var result = possiblyNull.IfNotNull(x => (x.RecursiveProperty).CalculateValue());",
			"var result = possiblyNull is ReferenceThing x ? (x.RecursiveProperty).CalculateValue() : default(ReferenceThing);")]
		[TestCase(
			"new ReferenceThing()",
			"var result = possiblyNull.IfNotNull(x => ((x.RecursiveProperty).RecursiveProperty).CalculateValue());",
			"var result = possiblyNull is ReferenceThing x ? ((x.RecursiveProperty).RecursiveProperty).CalculateValue() : default(ReferenceThing);")]
		[TestCase(
			"new ReferenceThing()",
			"var result = possiblyNull.IfNotNull(x => (x.RecursiveProperty?.RecursiveProperty).CalculateValue());",
			"var result = possiblyNull is ReferenceThing x ? (x.RecursiveProperty?.RecursiveProperty).CalculateValue() : default(ReferenceThing);")]
		// When using pattern matching, the input type must be used for the declaration type.
		// (in most of these tests, the input type and output type are the same)
		[TestCase(
			"new ReferenceThing()",
			"var result = possiblyNull.IfNotNull(x => (x.ValueTypeProperty));",
			"var result = possiblyNull is ReferenceThing x ? (x.ValueTypeProperty) : default(int);")]
		// Conditional operators should also work with indexed access.
		[TestCase(
			"new ReferenceThing()",
			"var result = possiblyNull.IfNotNull(x => x[0]);",
			"var result = possiblyNull?[0];")]
		// Nothing fancy should happen if indexed access is later in the expression.
		[TestCase(
			"new ReferenceThing()",
			"var result = possiblyNull.IfNotNull(x => x.RecursiveProperty[0]);",
			"var result = possiblyNull?.RecursiveProperty[0];")]
		// A conditional operator for indexed access later in the expression shouldn't break anything.
		[TestCase(
			"new ReferenceThing()",
			"var result = possiblyNull.IfNotNull(x => x.RecursiveProperty?[0]);",
			"var result = possiblyNull?.RecursiveProperty?[0];")]
		// Passing delegate references should be transformed into appropriate invocations.
		[TestCase(
			"new ReferenceThing()",
			"var result = possiblyNull.IfNotNull(ReferenceThing.CalculateStatic);",
			"var result = possiblyNull is ReferenceThing value ? ReferenceThing.CalculateStatic(value) : default(ReferenceThing);")]
		[TestCase(
			"new ReferenceThing()",
			"var result = possiblyNull.IfNotNull(ReferenceThing.CalculateStatic, ReferenceThing.Factory);",
			"var result = possiblyNull is ReferenceThing value ? ReferenceThing.CalculateStatic(value) : ReferenceThing.Factory();")]
		// The results using Nullable<T> generally look the same, but they require special handling.
		[TestCase(
			"new ReferenceThing()",
			"var result = possiblyNull.IfNotNull(x => x.NullableProperty);",
			"var result = possiblyNull?.NullableProperty;")]
		[TestCase(
			"(ValueThing?) new ValueThing()",
			"var result = possiblyNull.IfNotNull((ValueThing x) => x.ValueTypeProperty);",
			"var result = possiblyNull?.ValueTypeProperty ?? default(int);")]
		[TestCase(
			"(ValueThing?) new ValueThing()",
			"var result = possiblyNull.IfNotNull((ValueThing x) => x.RecursiveProperty);",
			"var result = possiblyNull?.RecursiveProperty ?? default(ValueThing);")]
		[TestCase(
			"(ValueThing?) new ValueThing()",
			"var result = IfNotNullExtensionMethod.IfNotNull(possiblyNull, (ValueThing x) => x.ValueTypeProperty);",
			"var result = possiblyNull?.ValueTypeProperty ?? default(int);")]
		[TestCase(
			"(ValueThing?) new ValueThing()",
			"var result = possiblyNull.IfNotNull((ValueThing x) => x.ValueTypeProperty, 0);",
			"var result = possiblyNull?.ValueTypeProperty ?? 0;")]
		[TestCase(
			"(ValueThing?) new ValueThing()",
			"var result = IfNotNullExtensionMethod.IfNotNull(possiblyNull, (ValueThing x) => x.ValueTypeProperty, 0);",
			"var result = possiblyNull?.ValueTypeProperty ?? 0;")]
		[TestCase(
			"(ValueThing?) new ValueThing()",
			"var result = possiblyNull.IfNotNull((ValueThing x) => x.ValueTypeProperty, () => 0);",
			"var result = possiblyNull?.ValueTypeProperty ?? 0;")]
		[TestCase(
			"(ValueThing?) new ValueThing()",
			"var result = IfNotNullExtensionMethod.IfNotNull(possiblyNull, (ValueThing x) => x.ValueTypeProperty, () => 0);",
			"var result = possiblyNull?.ValueTypeProperty ?? 0;")]
		// Supplying a reference type default value requires falling back to pattern matching.
		[TestCase(
			"new ReferenceThing()",
			"var result = possiblyNull.IfNotNull(x => x.CalculateValue(), () => new ReferenceThing());",
			"var result = possiblyNull is ReferenceThing x ? x.CalculateValue() : new ReferenceThing();")]
		[TestCase(
			"new ReferenceThing()",
			"var result = possiblyNull.IfNotNull(x => x.CalculateValue(), new ReferenceThing());",
			"var result = possiblyNull is ReferenceThing x ? x.CalculateValue() : new ReferenceThing();")]
		// Calling IfNotNull on a delegate that is immediately invoked results in some special cases.
		[TestCase(
			"(Func<ReferenceThing>) new ReferenceThing().CalculateValue",
			"var result = possiblyNull.IfNotNull(x => x());",
			"var result = possiblyNull?.Invoke();")]
		[TestCase(
			"(Func<int>) new ReferenceThing().CalculateValueTypeValue",
			"var result = possiblyNull.IfNotNull(x => x(), 0);",
			"var result = possiblyNull?.Invoke() ?? 0;")]
		[TestCase(
			"(Func<int>) new ReferenceThing().CalculateValueTypeValue",
			"var result = possiblyNull.IfNotNull(x => x(), () => 1);",
			"var result = possiblyNull?.Invoke() ?? 1;")]
		[TestCase(
			"(Func<int>) new ReferenceThing().CalculateValueTypeValue",
			"var result = possiblyNull.IfNotNull(x => x(), possiblyNull);",
			"var result = possiblyNull?.Invoke() ?? possiblyNull();")]
		// Multiple usages of the parameter should force the usage of pattern matching.
		[TestCase(
			"new ReferenceThing()",
			"var result = possiblyNull.IfNotNull(x => x.CalculateValue(x));",
			"var result = possiblyNull is ReferenceThing x ? x.CalculateValue(x) : default(ReferenceThing);")]
		// This cast makes the call equivalent to the null-conditional operator, which means that
		// pattern matching is unnecessary and the cast can be discarded.
		[TestCase(
			"new ReferenceThing()",
			"var result = possiblyNull.IfNotNull(x => (int?) x.ValueTypeProperty);",
			"var result = possiblyNull?.ValueTypeProperty;")]
		// Anonymous types can sometimes prevent the code fixer from supplying a transformation, but
		// this pattern should work.
		[TestCase(
			"new { Property = \"value\" }",
			"var result = possiblyNull.IfNotNull(x => x.Property);",
			"var result = possiblyNull?.Property;")]
		// Invocations that return anonymous types often cannot be converted, but they work when a default value is explicitly
		// provided.
		[TestCase(
			"new ReferenceThing()",
			"var result = possiblyNull.IfNotNull(x => new { Property = \"value\" }, () => new { Property = \"other value\" });",
			"var result = possiblyNull is ReferenceThing x ? (new { Property = \"value\" }) : new { Property = \"other value\" };")]
		// A new expression with no default value can still be transformed if it is the left hand side of a null-coalescing operator.
		[TestCase(
			"new ReferenceThing()",
			"var result = possiblyNull.IfNotNull(x => new { Property = \"value\" }) ?? new { Property = \"other value\" };",
			"var result = possiblyNull is ReferenceThing x ? new { Property = \"value\" } : new { Property = \"other value\" };")]
		public void SimpleMethodCall(string possiblyNull, string call, string fixedCall)
		{
			string createProgram(string actualCall) =>
				c_preamble + @"
namespace TestProgram
{
	internal static class TestClass
	{
		public static void CallIfNotNull()
		{
			var possiblyNull = " + possiblyNull + @";
			" + actualCall + @"
		}
	}
}";

			var expected = new DiagnosticResult
			{
				Id = IfNotNullAnalyzer.DiagnosticId,
				Message = "Prefer modern language features over IfNotNull usage.",
				Severity = DiagnosticSeverity.Info,
				Locations = new[] { new DiagnosticResultLocation("Test0.cs", c_preambleLength + 8, 17) },
			};

			string invalidProgram = createProgram(call);

			VerifyCSharpDiagnostic(invalidProgram, expected);

			string validProgram = createProgram(fixedCall).Replace("using Libronix.Utility.IfNotNull;\n", "");

			VerifyCSharpFix(invalidProgram, validProgram);
		}

		[TestCase(
			"new ReferenceThing()",
			"var result = possiblyNull.IfNotNull(x => x.RecursiveProperty.IfNotNull(y => y.CalculateValue()));",
			"var result = possiblyNull?.RecursiveProperty?.CalculateValue();",
			17, 45)]
		[TestCase(
			"new ReferenceThing()",
			"var result = possiblyNull.IfNotNull(x => x.RecursiveProperty).IfNotNull(x => x.CalculateValue());",
			"var result = possiblyNull?.RecursiveProperty?.CalculateValue();",
			17, 17)]
		[TestCase(
			"new ReferenceThing()",
			"var result = possiblyNull.IfNotNull(x => ReferenceThing.CalculateStatic(x)).IfNotNull(x => ReferenceThing.CalculateStatic(x));",
			"var result = (possiblyNull is ReferenceThing x ? ReferenceThing.CalculateStatic(x) : default(ReferenceThing)) is ReferenceThing x1 ? ReferenceThing.CalculateStatic(x1) : default(ReferenceThing);",
			17, 17)]
		[TestCase(
			"new ReferenceThing()",
			"var result = possiblyNull.IfNotNull(ReferenceThing.CalculateStatic).IfNotNull(ReferenceThing.CalculateStatic);",
			"var result = (possiblyNull is ReferenceThing value1 ? ReferenceThing.CalculateStatic(value1) : default(ReferenceThing)) is ReferenceThing value ? ReferenceThing.CalculateStatic(value) : default(ReferenceThing);",
			17, 17)]
		public void MultipleMethodCalls(string possiblyNull, string call, string fixedCall, int firstColumn, int secondColumn)
		{
			string createProgram(string actualCall) =>
				c_preamble + @"
namespace TestProgram
{
	internal static class TestClass
	{
		public static void CallIfNotNull()
		{
			var possiblyNull = " + possiblyNull + @";
			" + actualCall + @"
		}
	}
}";

			DiagnosticResult CreateDiagnosticAtColumn(int column) =>
				new DiagnosticResult
				{
					Id = IfNotNullAnalyzer.DiagnosticId,
					Message = "Prefer modern language features over IfNotNull usage.",
					Severity = DiagnosticSeverity.Info,
					Locations = new[] { new DiagnosticResultLocation("Test0.cs", c_preambleLength + 8, column) },
				};

			string invalidProgram = createProgram(call);

			VerifyCSharpDiagnostic(invalidProgram, CreateDiagnosticAtColumn(firstColumn), CreateDiagnosticAtColumn(secondColumn));

			string validProgram = createProgram(fixedCall).Replace("using Libronix.Utility.IfNotNull;\n", "");

			VerifyCSharpFix(invalidProgram, validProgram);
		}

		[TestCase(
			"new ReferenceThing()",
			"possiblyNull.IfNotNull(x => x.Method());")]
		[TestCase(
			"new ReferenceThing()",
			"IfNotNullExtensionMethod.IfNotNull(possiblyNull, x => x.Method());")]
		[TestCase(
			"(ValueThing?) new ValueThing()",
			"possiblyNull.IfNotNull((ValueThing x) => x.Method());")]
		[TestCase(
			"(ValueThing?) new ValueThing()",
			"IfNotNullExtensionMethod.IfNotNull(possiblyNull, (ValueThing x) => x.Method());")]
		[TestCase(
			"new ReferenceThing()",
			"possiblyNull.IfNotNull(x => x.Method(), () => throw new InvalidOperationException());")]
		[TestCase(
			"new ReferenceThing()",
			"IfNotNullExtensionMethod.IfNotNull(possiblyNull, x => x.Method(), () => throw new InvalidOperationException());")]
		[TestCase(
			"(ValueThing?) new ValueThing()",
			"possiblyNull.IfNotNull((ValueThing x) => x.Method(), () => throw new InvalidOperationException());")]
		[TestCase(
			"(ValueThing?) new ValueThing()",
			"IfNotNullExtensionMethod.IfNotNull(possiblyNull, (ValueThing x) => x.Method(), () => throw new InvalidOperationException());")]
		public void VoidInvocation(string possiblyNull, string call)
		{
			string createProgram(string actualCall) =>
				c_preamble + @"
namespace TestProgram
{
	internal static class TestClass
	{
		public static void CallIfNotNull()
		{
			var possiblyNull = " + possiblyNull + @";
			" + actualCall + @"
		}
	}
}";

			var expected = new DiagnosticResult
			{
				Id = IfNotNullAnalyzer.DiagnosticId,
				Message = "Prefer modern language features over IfNotNull usage.",
				Severity = DiagnosticSeverity.Info,
				Locations = new[] { new DiagnosticResultLocation("Test0.cs", c_preambleLength + 8, 4) },
			};

			string invalidProgram = createProgram(call);

			VerifyCSharpDiagnostic(invalidProgram, expected);

			// The fixer should decline to make any modifications to these calls.
			VerifyCSharpFix(invalidProgram, invalidProgram);
		}

		[TestCase(
			"System.Threading.Tasks.Task.FromResult(default(ReferenceThing))",
			"var result = possiblyNull.IfNotNull(async x => await x);")]
		[TestCase(
			"new ReferenceThing()",
			"var result = possiblyNull.IfNotNull(x => { return x.CalculateValue(); });")]
		[TestCase(
			"new ReferenceThing()",
			"var result = possiblyNull.IfNotNull(x => new { Property = 5 });")]
		[TestCase(
			"new { Property = 5 }",
			"var result = possiblyNull.IfNotNull(x => new[] { x });")]
		public void UnhandledInvocation(string possiblyNull, string call)
		{
			string createProgram(string actualCall) =>
				c_preamble + @"
namespace TestProgram
{
	internal static class TestClass
	{
		public static void CallIfNotNull()
		{
			var possiblyNull = " + possiblyNull + @";
			" + actualCall + @"
		}
	}
}";

			var expected = new DiagnosticResult
			{
				Id = IfNotNullAnalyzer.DiagnosticId,
				Message = "Prefer modern language features over IfNotNull usage.",
				Severity = DiagnosticSeverity.Info,
				Locations = new[] { new DiagnosticResultLocation("Test0.cs", c_preambleLength + 8, 17) },
			};

			string invalidProgram = createProgram(call);

			VerifyCSharpDiagnostic(invalidProgram, expected);

			VerifyCSharpFix(invalidProgram, invalidProgram);
		}

		protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new IfNotNullAnalyzer();

		protected override CodeFixProvider GetCSharpCodeFixProvider() => new IfNotNullCodeFixProvider();

		private const string c_preamble = @"using System;
using Libronix.Utility.IfNotNull;
using TestProgram;

namespace Libronix.Utility.IfNotNull
{
	public static class IfNotNullExtensionMethod
	{
		public static TOutput IfNotNull<TInput, TOutput>(this TInput t, Func<TInput, TOutput> fn) where TInput : class => throw new NotImplementedException();
		public static TOutput IfNotNull<TInput, TOutput>(this TInput? t, Func<TInput, TOutput> fn) where TInput : struct => throw new NotImplementedException();
		public static TOutput IfNotNull<TInput, TOutput>(this TInput t, Func<TInput, TOutput> fn, TOutput def) where TInput : class => throw new NotImplementedException();
		public static TOutput IfNotNull<TInput, TOutput>(this TInput? t, Func<TInput, TOutput> fn, TOutput def) where TInput : struct => throw new NotImplementedException();
		public static TOutput IfNotNull<TInput, TOutput>(this TInput t, Func<TInput, TOutput> fn, Func<TOutput> def) where TInput : class => throw new NotImplementedException();
		public static TOutput IfNotNull<TInput, TOutput>(this TInput? t, Func<TInput, TOutput> fn, Func<TOutput> def) where TInput : struct => throw new NotImplementedException();
		public static void IfNotNull<TInput>(this TInput t, Action<TInput> fn) where TInput : class => throw new NotImplementedException();
		public static void IfNotNull<TInput>(this TInput? t, Action<TInput> fn) where TInput : struct => throw new NotImplementedException();
		public static void IfNotNull<TInput>(this TInput t, Action<TInput> fn, Action def) where TInput : class => throw new NotImplementedException();
		public static void IfNotNull<TInput>(this TInput? t, Action<TInput> fn, Action def) where TInput : struct => throw new NotImplementedException();
	}
}

namespace TestProgram
{
	internal sealed class ReferenceThing
	{
		public int ValueTypeProperty => throw new NotImplementedException();
		public int? NullableProperty => throw new NotImplementedException();
		public ReferenceThing RecursiveProperty => throw new NotImplementedException();
		public void Method() => throw new NotImplementedException();
		public ReferenceThing CalculateValue() => throw new NotImplementedException();
		public ReferenceThing CalculateValue(ReferenceThing input) => throw new NotImplementedException();
		public int CalculateValueTypeValue() => throw new NotImplementedException();
		public ReferenceThing this[int i] => throw new NotImplementedException();
		public static ReferenceThing CalculateStatic(ReferenceThing x) => throw new NotImplementedException();
		public static ReferenceThing Factory() => throw new NotImplementedException();
	}

	internal struct ValueThing
	{
		public int ValueTypeProperty => throw new NotImplementedException();
		public ReferenceThing ReferenceTypeProperty => throw new NotImplementedException();
		public ValueThing RecursiveProperty => throw new NotImplementedException();
		public void Method() => throw new NotImplementedException();
		public ValueThing CalculateValue() => throw new NotImplementedException();
	}
}
";

		private static readonly int c_preambleLength = c_preamble.Split('\n').Length;
	}
}
