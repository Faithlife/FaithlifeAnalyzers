using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Faithlife.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InterpolatedStringAnalyzer : DiagnosticAnalyzer
{
	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
		{
			compilationStartAnalysisContext.RegisterOperationBlockStartAction(context =>
			{
				context.RegisterOperationAction(AnalyzeOperation, OperationKind.InterpolatedString);
			});
		});
	}

	public const string DiagnosticIdDollar = "FL0007";
	public const string DiagnosticIdUnnecessary = "FL0014";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_ruleDollar, s_ruleUnnecessary);

	private static void AnalyzeOperation(OperationAnalysisContext context)
	{
		var interpolatedStringOperation = (IInterpolatedStringOperation)context.Operation;
		var foundDollarSign = false;

		// Check if this interpolated string is being used with an interpolated string handler
		var isUsedWithHandler = IsUsedWithInterpolatedStringHandler(interpolatedStringOperation);

		bool hasInterpolations;
		if (isUsedWithHandler)
		{
			// When used with interpolated string handlers, the operation tree might not accurately
			// reflect interpolations, so check the syntax directly
			hasInterpolations = HasInterpolationsInSyntax(interpolatedStringOperation);
		}
		else
		{
			// For regular interpolated strings, use the operation tree check
			hasInterpolations = interpolatedStringOperation.Children.Any(child => child is IInterpolationOperation);
		}

		if (!hasInterpolations)
		{
			context.ReportDiagnostic(Diagnostic.Create(s_ruleUnnecessary, interpolatedStringOperation.Syntax.GetLocation()));
		}

		foreach (var child in interpolatedStringOperation.Children)
		{
			if ((child as IInterpolatedStringTextOperation)?.Text.Syntax.ToFullString().EndsWith("$", StringComparison.Ordinal) ?? false)
			{
				foundDollarSign = true;
			}
			else
			{
				if (child is IInterpolatedStringContentOperation && foundDollarSign)
					context.ReportDiagnostic(Diagnostic.Create(s_ruleDollar, child.Syntax.GetLocation()));
				foundDollarSign = false;
			}
		}
	}

	private static bool HasInterpolationsInSyntax(IInterpolatedStringOperation interpolatedStringOperation)
	{
		// Check the actual syntax for interpolations (expressions within {})
		var syntaxNode = interpolatedStringOperation.Syntax;
		var syntaxText = syntaxNode.ToString();

		// Look for { followed by } with content in between
		// This is a simple check - for more robust parsing we could use the syntax tree
		var openBraceIndex = 0;
		while ((openBraceIndex = syntaxText.IndexOf('{', openBraceIndex)) != -1)
		{
			// Skip escaped braces {{
			if (openBraceIndex + 1 < syntaxText.Length && syntaxText[openBraceIndex + 1] == '{')
			{
				openBraceIndex += 2;
				continue;
			}

			// Look for the closing brace
			var closeBraceIndex = syntaxText.IndexOf('}', openBraceIndex + 1);
			if (closeBraceIndex != -1)
			{
				// Check if there's content between the braces
				var content = syntaxText.Substring(openBraceIndex + 1, closeBraceIndex - openBraceIndex - 1).Trim();
				if (!string.IsNullOrEmpty(content))
				{
					return true;
				}
			}
			openBraceIndex++;
		}

		return false;
	}

	private static bool IsUsedWithInterpolatedStringHandler(IInterpolatedStringOperation interpolatedStringOperation)
	{
		// Check if the interpolated string is being passed to a method with an interpolated string handler parameter
		var parent = interpolatedStringOperation.Parent;

		// Walk up to find an argument operation
		while (parent != null && parent is not IArgumentOperation)
		{
			parent = parent.Parent;
		}

		if (parent is IArgumentOperation argumentOperation)
		{
			// Get the parameter that this argument corresponds to
			var parameter = argumentOperation.Parameter;
			if (parameter != null)
			{
				// Special case: Debug.Assert has interpolated string handler parameters in .NET 6+
				if (parameter.ContainingSymbol is IMethodSymbol method &&
					method.ContainingType.Name == "Debug" &&
					method.ContainingType.ContainingNamespace.ToDisplayString() == "System.Diagnostics" &&
					method.Name == "Assert")
				{
					return true;
				}

				// Check if the parameter type is an interpolated string handler
				// Look for types that end with "InterpolatedStringHandler" or have the InterpolatedStringHandlerAttribute
				var parameterTypeName = parameter.Type.Name;
				if (parameterTypeName.EndsWith("InterpolatedStringHandler", StringComparison.Ordinal))
				{
					return true;
				}

				// Also check for the InterpolatedStringHandlerAttribute on the parameter type
				if (parameter.Type is INamedTypeSymbol namedTypeSymbol)
				{
					foreach (var attribute in namedTypeSymbol.GetAttributes())
					{
						if (attribute.AttributeClass?.Name == "InterpolatedStringHandlerAttribute" ||
							attribute.AttributeClass?.ToDisplayString().Contains("InterpolatedStringHandler") == true)
						{
							return true;
						}
					}
				}

				// Check if the parameter itself has the InterpolatedStringHandlerAttribute
				foreach (var attribute in parameter.GetAttributes())
				{
					if (attribute.AttributeClass?.Name == "InterpolatedStringHandlerAttribute" ||
						attribute.AttributeClass?.ToDisplayString().Contains("InterpolatedStringHandler") == true)
					{
						return true;
					}
				}
			}
		}

		return false;
	}

	private static readonly DiagnosticDescriptor s_ruleDollar = new(
		id: DiagnosticIdDollar,
		title: "Unintentional ${} in interpolated strings",
		messageFormat: "Avoid using ${} in interpolated strings.",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{DiagnosticIdDollar}");

	private static readonly DiagnosticDescriptor s_ruleUnnecessary = new(
		id: DiagnosticIdUnnecessary,
		title: "Unnecessary interpolated string",
		messageFormat: "Avoid using an interpolated string where an equivalent literal string exists.",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{DiagnosticIdUnnecessary}");
}
