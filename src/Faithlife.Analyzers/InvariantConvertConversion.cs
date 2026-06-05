using Microsoft.CodeAnalysis;

namespace Faithlife.Analyzers;

internal sealed class InvariantConvertConversion
{
	public InvariantConvertConversion(SpecialType specialType, string parseMethodName, string tryParseMethodName, string numberStyleName)
	{
		SpecialType = specialType;
		ParseMethodName = parseMethodName;
		TryParseMethodName = tryParseMethodName;
		NumberStyleName = numberStyleName;
	}

	public SpecialType SpecialType { get; }

	public string ParseMethodName { get; }

	public string TryParseMethodName { get; }

	public string NumberStyleName { get; }
}
