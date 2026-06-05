using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Faithlife.Analyzers;

internal sealed class InvariantConvertContext
{
	public static InvariantConvertContext? TryCreate(Compilation compilation)
	{
		if (compilation.GetTypeByMetadataName("Libronix.Utility.Invariant.InvariantConvert") is null)
			return null;
		if (compilation.GetTypeByMetadataName("System.Globalization.CultureInfo") is not { } cultureInfoType)
			return null;
		if (compilation.GetTypeByMetadataName("System.Globalization.NumberStyles") is not { } numberStylesType)
			return null;
		if (compilation.GetTypeByMetadataName("System.IFormatProvider") is not { } formatProviderType)
			return null;

		return new InvariantConvertContext(cultureInfoType, numberStylesType, formatProviderType);
	}

	public InvariantConvertMatch? Match(InvocationExpressionSyntax invocation, SemanticModel semanticModel, CancellationToken cancellationToken = default)
	{
		if (semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method)
			return null;

		var typeName = GetSupportedTypeName(method.ContainingType);
		if (typeName is null)
			return null;

		return method.Name switch
		{
			"Parse" => MatchParse(invocation, method, typeName, semanticModel),
			"TryParse" => MatchTryParse(invocation, method, typeName, semanticModel),
			"ToString" => MatchToString(invocation, method, semanticModel),
			_ => null,
		};
	}

	private InvariantConvertMatch? MatchParse(InvocationExpressionSyntax invocation, IMethodSymbol method, string typeName, SemanticModel semanticModel)
	{
		if (!method.IsStatic)
			return null;
		if (!TryGetArguments(method, invocation.ArgumentList.Arguments, out var value, out var provider, out var style, out var outArgument))
			return null;
		if (value is null || outArgument is not null)
			return null;

		if (method.ContainingType.SpecialType == SpecialType.System_Boolean)
		{
			// bool.Parse has no culture-aware overload; only the single string argument is supported.
			if (provider is not null || style is not null)
				return null;
		}
		else
		{
			if (!IsInvariantCulture(provider, semanticModel) || !IsAllowedStyle(style, typeName, semanticModel))
				return null;
		}

		return new InvariantConvertMatch(InvariantConvertKind.Parse, "Parse" + typeName, value.Expression, null);
	}

	private InvariantConvertMatch? MatchTryParse(InvocationExpressionSyntax invocation, IMethodSymbol method, string typeName, SemanticModel semanticModel)
	{
		if (!method.IsStatic)
			return null;
		if (!TryGetArguments(method, invocation.ArgumentList.Arguments, out var value, out var provider, out var style, out var outArgument))
			return null;
		if (value is null || outArgument is null)
			return null;

		if (method.ContainingType.SpecialType == SpecialType.System_Boolean)
		{
			if (provider is not null || style is not null)
				return null;
		}
		else
		{
			if (!IsInvariantCulture(provider, semanticModel) || !IsAllowedStyle(style, typeName, semanticModel))
				return null;
		}

		return new InvariantConvertMatch(InvariantConvertKind.TryParse, "TryParse" + typeName, value.Expression, outArgument);
	}

	private InvariantConvertMatch? MatchToString(InvocationExpressionSyntax invocation, IMethodSymbol method, SemanticModel semanticModel)
	{
		if (method.IsStatic)
			return null;
		if (method.Parameters.Length != 1 || !SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, m_formatProviderType))
			return null;

		var arguments = invocation.ArgumentList.Arguments;
		if (arguments.Count != 1 || arguments[0].NameColon is not null)
			return null;
		if (!IsInvariantCulture(arguments[0], semanticModel))
			return null;
		if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
			return null;

		return new InvariantConvertMatch(InvariantConvertKind.ToString, "ToInvariantString", memberAccess.Expression, null);
	}

	private bool TryGetArguments(IMethodSymbol method, SeparatedSyntaxList<ArgumentSyntax> arguments, out ArgumentSyntax? value, out ArgumentSyntax? provider, out ArgumentSyntax? style, out ArgumentSyntax? outArgument)
	{
		value = null;
		provider = null;
		style = null;
		outArgument = null;

		if (arguments.Any(x => x.NameColon is not null))
			return false;
		if (arguments.Count != method.Parameters.Length)
			return false;

		foreach (var parameter in method.Parameters)
		{
			var argument = arguments[parameter.Ordinal];
			if (parameter.RefKind == RefKind.Out)
				outArgument = argument;
			else if (parameter.Type.SpecialType == SpecialType.System_String)
				value = argument;
			else if (SymbolEqualityComparer.Default.Equals(parameter.Type, m_numberStylesType))
				style = argument;
			else if (SymbolEqualityComparer.Default.Equals(parameter.Type, m_formatProviderType))
				provider = argument;
			else
				return false;
		}

		return true;
	}

	private bool IsInvariantCulture(ArgumentSyntax? argument, SemanticModel semanticModel)
	{
		if (argument is null)
			return false;

		return semanticModel.GetSymbolInfo(argument.Expression).Symbol is IPropertySymbol property &&
			property.Name == "InvariantCulture" &&
			SymbolEqualityComparer.Default.Equals(property.ContainingType, m_cultureInfoType);
	}

	private static bool IsAllowedStyle(ArgumentSyntax? argument, string typeName, SemanticModel semanticModel)
	{
		if (argument is null)
			return true;

		// NumberStyles.None == 0; NumberStyles.Integer == 7; NumberStyles.Float == 167.
		if (semanticModel.GetConstantValue(argument.Expression).Value is not int value)
			return false;

		return typeName == "Double" ? value is 0 or 167 : value is 0 or 7;
	}

	private static string? GetSupportedTypeName(INamedTypeSymbol type) => type.SpecialType switch
	{
		SpecialType.System_Boolean => "Boolean",
		SpecialType.System_Int32 => "Int32",
		SpecialType.System_Int64 => "Int64",
		SpecialType.System_Double => "Double",
		_ => null,
	};

	private InvariantConvertContext(INamedTypeSymbol cultureInfoType, INamedTypeSymbol numberStylesType, INamedTypeSymbol formatProviderType)
	{
		m_cultureInfoType = cultureInfoType;
		m_numberStylesType = numberStylesType;
		m_formatProviderType = formatProviderType;
	}

	private readonly INamedTypeSymbol m_cultureInfoType;
	private readonly INamedTypeSymbol m_numberStylesType;
	private readonly INamedTypeSymbol m_formatProviderType;
}
