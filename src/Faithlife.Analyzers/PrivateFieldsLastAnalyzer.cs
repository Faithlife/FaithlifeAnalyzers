using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Faithlife.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PrivateFieldsLastAnalyzer : DiagnosticAnalyzer
{
	public const string DiagnosticId = "FL0025";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterSyntaxNodeAction(AnalyzeType, SyntaxKind.ClassDeclaration, SyntaxKind.StructDeclaration, SyntaxKind.RecordDeclaration, SyntaxKind.RecordStructDeclaration);
	}

	private static void AnalyzeType(SyntaxNodeAnalysisContext context)
	{
		var typeDeclaration = (TypeDeclarationSyntax) context.Node;
		if (typeDeclaration.Members.Count < 2)
			return;

		var leadingPrivateFieldCount = GetLeadingPrivateFieldCount(typeDeclaration.Members, member => IsPrivateField(context.SemanticModel, member, context.CancellationToken));
		if (leadingPrivateFieldCount is 0 || leadingPrivateFieldCount == typeDeclaration.Members.Count)
			return;

		context.ReportDiagnostic(Diagnostic.Create(s_rule, typeDeclaration.Members[0].GetLocation()));
	}

	internal static int GetLeadingPrivateFieldCount(SyntaxList<MemberDeclarationSyntax> members, Func<MemberDeclarationSyntax, bool> isPrivateField)
	{
		var privateFieldCount = 0;
		while (privateFieldCount < members.Count && isPrivateField(members[privateFieldCount]))
			privateFieldCount++;

		return privateFieldCount;
	}

	internal static bool IsPrivateField(SemanticModel semanticModel, MemberDeclarationSyntax member, CancellationToken cancellationToken)
	{
		if (member is not FieldDeclarationSyntax { Declaration.Variables.Count: > 0 } field)
			return false;
		if (field.GetLeadingTrivia().Any(static trivia => trivia.GetStructure() is DirectiveTriviaSyntax))
			return false;

		return semanticModel.GetDeclaredSymbol(field.Declaration.Variables[0], cancellationToken) is IFieldSymbol
		{
			DeclaredAccessibility: Accessibility.Private,
			IsStatic: false,
		};
	}

	private static readonly DiagnosticDescriptor s_rule = new(
		id: DiagnosticId,
		title: "Private fields should be defined last",
		messageFormat: "Move private fields to the end of the type",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/blob/-/docs/{DiagnosticId}.md");
}
