using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Faithlife.Analyzers
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class OverloadWithStringComparisonAnalyzer : DiagnosticAnalyzer
	{
		public override void Initialize(AnalysisContext context)
		{
			// NOTE: some parts of this implementation derived from https://github.com/dotnet/roslyn-analyzers/blob/7a2540618fc32c5b38bdb43bc3a70ba6401ed135/src/Microsoft.NetCore.Analyzers/Core/Runtime/UseOrdinalStringComparison.cs
			context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
			{
				var stringComparisonType = compilationStartAnalysisContext.Compilation.GetTypeByMetadataName("System.StringComparison");
				if (stringComparisonType != null)
				{
					compilationStartAnalysisContext.RegisterOperationAction(operationContext =>
					{
						var operation = (IInvocationOperation) operationContext.Operation;
						var methodSymbol = operation.TargetMethod;
						if (methodSymbol?.ContainingType.SpecialType == SpecialType.System_String && s_affectedMethods.Contains(methodSymbol.Name))
						{
							if (!IsAcceptableOverload(methodSymbol, stringComparisonType))
							{
								// wrong overload
								var rule = (methodSymbol.Name == "Equals" && ((methodSymbol.Parameters.Length == 1 && !methodSymbol.IsStatic) || (methodSymbol.Parameters.Length == 2 && methodSymbol.IsStatic))) ?
									s_avoidStringEqualsRule : s_useStringComparisonRule;
								operationContext.ReportDiagnostic(Diagnostic.Create(rule, GetMethodNameLocation(operation.Syntax)));
							}
							else if (methodSymbol.Name == "Equals" && ((methodSymbol.Parameters.Length == 2 && !methodSymbol.IsStatic) || (methodSymbol.Parameters.Length == 3 && methodSymbol.IsStatic)))
							{
								var lastArgument = operation.Arguments.Last();
								if (lastArgument.Value.Kind == OperationKind.FieldReference)
								{
									var fieldSymbol = ((IFieldReferenceOperation) lastArgument.Value).Field;
									if (fieldSymbol?.ContainingType == stringComparisonType && fieldSymbol.Name == "Ordinal")
									{
										// right overload, wrong value
										operationContext.ReportDiagnostic(Diagnostic.Create(s_avoidStringEqualsRule, GetMethodNameLocation(operation.Syntax)));
									}
								}
							}
						}
					}, OperationKind.Invocation);
				}
			});
		}

		public const string UseStringComparisonDiagnosticId = "FL0002";
		public const string AvoidStringEqualsDiagnosticId = "FL0004";

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => s_rules;

		static readonly ISet<string> s_affectedMethods = new HashSet<string>(new[] { "Equals", "Compare", "IndexOf", "LastIndexOf", "StartsWith", "EndsWith" });

		private static bool IsAcceptableOverload(IMethodSymbol methodSymbol, INamedTypeSymbol stringComparisonType)
		{
			switch ((methodSymbol.IsStatic, methodSymbol.Name, methodSymbol.Parameters.Length))
			{
			case var t when t == (true, "Compare", 3):
				// static string.Compare(string, string, StringComparison)
				return methodSymbol.Parameters[0].Type.SpecialType == SpecialType.System_String &&
					methodSymbol.Parameters[1].Type.SpecialType == SpecialType.System_String &&
					methodSymbol.Parameters[2].Type.Equals(stringComparisonType);

			case var t when t == (true, "Compare", 6):
				// static string.Compare(string, int, string, int, int, StringComparison)
				return methodSymbol.Parameters[0].Type.SpecialType == SpecialType.System_String &&
					methodSymbol.Parameters[1].Type.SpecialType == SpecialType.System_Int32 &&
					methodSymbol.Parameters[2].Type.SpecialType == SpecialType.System_String &&
					methodSymbol.Parameters[3].Type.SpecialType == SpecialType.System_Int32 &&
					methodSymbol.Parameters[4].Type.SpecialType == SpecialType.System_Int32 &&
					methodSymbol.Parameters[5].Type.Equals(stringComparisonType);

			case var t when t == (false, "EndsWith", 1) || t == (false, "StartsWith", 1):
				// string.EndsWith(string, StringComparison)
				return methodSymbol.Parameters[0].Type.SpecialType == SpecialType.System_Char;

			case var t when t == (false, "EndsWith", 2) || t == (false, "StartsWith", 2):
				// string.EndsWith(string, StringComparison)
				return methodSymbol.Parameters[0].Type.SpecialType == SpecialType.System_String &&
					methodSymbol.Parameters[1].Type.Equals(stringComparisonType);

			case var t when t == (false, "Equals", 1):
				// string.Equals(object)
				return methodSymbol.Parameters[0].Type.SpecialType == SpecialType.System_Object;

			case var t when t == (false, "Equals", 2):
				// string.Equals(string, StringComparison)
				return methodSymbol.Parameters[0].Type.SpecialType == SpecialType.System_String &&
					methodSymbol.Parameters[1].Type.Equals(stringComparisonType);

			case var t when t == (true, "Equals", 3):
				// static string.Equals(string, string, StringComparison)
				return methodSymbol.Parameters[0].Type.SpecialType == SpecialType.System_String &&
					methodSymbol.Parameters[1].Type.SpecialType == SpecialType.System_String &&
					methodSymbol.Parameters[2].Type.Equals(stringComparisonType);

			case var t when t == (false, "IndexOf", 1) || t == (false, "LastIndexOf", 1):
				// string.IndexOf(char)
				return methodSymbol.Parameters[0].Type.SpecialType == SpecialType.System_Char;

			case var t when t == (false, "IndexOf", 2) || t == (false, "LastIndexOf", 2):
				// string.IndexOf(char, int); string.IndexOf(string, StringComparison)
				return (methodSymbol.Parameters[0].Type.SpecialType == SpecialType.System_Char && methodSymbol.Parameters[1].Type.SpecialType == SpecialType.System_Int32) ||
						(methodSymbol.Parameters[0].Type.SpecialType == SpecialType.System_String && methodSymbol.Parameters[1].Type.Equals(stringComparisonType));

			case var t when t == (false, "IndexOf", 3) || t == (false, "LastIndexOf", 3):
				// string.IndexOf(char, int, int); string.IndexOf(string, int, StringComparison)
				return (methodSymbol.Parameters[0].Type.SpecialType == SpecialType.System_Char &&
					methodSymbol.Parameters[1].Type.SpecialType == SpecialType.System_Int32 &&
					methodSymbol.Parameters[2].Type.SpecialType == SpecialType.System_Int32) ||
					(methodSymbol.Parameters[0].Type.SpecialType == SpecialType.System_String &&
					methodSymbol.Parameters[1].Type.SpecialType == SpecialType.System_Int32 &&
					methodSymbol.Parameters[2].Type.Equals(stringComparisonType));

			case var t when t == (false, "IndexOf", 4) || t == (false, "LastIndexOf", 4):
				// string.IndexOf(string, int, int, StringComparison)
				return methodSymbol.Parameters[0].Type.SpecialType == SpecialType.System_String &&
					methodSymbol.Parameters[1].Type.SpecialType == SpecialType.System_Int32 &&
					methodSymbol.Parameters[2].Type.SpecialType == SpecialType.System_Int32 &&
					methodSymbol.Parameters[3].Type.Equals(stringComparisonType);
			}
			return false;
		}

		private static Location GetMethodNameLocation(SyntaxNode invocationNode)
		{
			switch (((InvocationExpressionSyntax) invocationNode).Expression)
			{
			case MemberAccessExpressionSyntax memberAccessExpression:
				return memberAccessExpression.Name.GetLocation();
			case ConditionalAccessExpressionSyntax conditionalAccessExpression:
				return conditionalAccessExpression.WhenNotNull.GetLocation();
			default:
				return ((InvocationExpressionSyntax) invocationNode).GetLocation();
			}
		}

		static readonly DiagnosticDescriptor s_useStringComparisonRule = new DiagnosticDescriptor(
			id: UseStringComparisonDiagnosticId,
			title: "Use StringComparison overload",
			messageFormat: "Use an overload that takes a StringComparison.",
			category: "Usage",
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			description: "The desired StringComparison must be explicitly specified.",
			helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{UseStringComparisonDiagnosticId}");

		static readonly DiagnosticDescriptor s_avoidStringEqualsRule = new DiagnosticDescriptor(
			id: AvoidStringEqualsDiagnosticId,
			title: "Avoid string.Equals(string, string)",
			messageFormat: "Use operator== or a non-ordinal StringComparison.",
			category: "Usage",
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{AvoidStringEqualsDiagnosticId}");

		static readonly ImmutableArray<DiagnosticDescriptor> s_rules = ImmutableArray.Create(s_useStringComparisonRule, s_avoidStringEqualsRule);
	}
}
