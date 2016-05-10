using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Symbols;

namespace Dapper_syntax_checker
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class Dapper_syntax_checkerAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "Dapper_syntax_checker";

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        internal static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        internal static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        internal static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        internal const string Category = "Naming";

        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);
        private int lastfieldPosition = 0;
        private Dictionary<string, string> Fields = new Dictionary<string, string>();

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Field);
            context.RegisterSyntaxNodeAction(AnalyzeObjectInitializer, SyntaxKind.FieldDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeArgument, SyntaxKind.Argument);
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);

        }
        private  string[] DapperInvocationMethodNames = new []{
            "Execute",
            "Query"
        };

        private void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var method = context.Node as InvocationExpressionSyntax;
            var methodName = ((method.Expression) as MemberAccessExpressionSyntax).Name.Identifier.Text;
            if (!DapperInvocationMethodNames.Contains(methodName)) return;
            var arguments = (method.ArgumentList as ArgumentListSyntax).Arguments;
            var querystryng = string.Empty;
            foreach (var arg in arguments)//argumentSyntax Identifier.Text
            {
                var queryStringNameSyntax = arg.Expression as IdentifierNameSyntax;
                
                if (queryStringNameSyntax != null)
                {
                  querystryng = queryStringNameSyntax.Identifier.Text;
                }

                var anonymousObj = arg.Expression as AnonymousObjectCreationExpressionSyntax;
                if (anonymousObj != null)
                {
                    foreach (var property in anonymousObj.Initializers)
                    {
                        var propName = property.NameEquals.Name.ToString();
                        var query = Fields[querystryng];
                        if (!query.Contains(propName))
                        {
                            var diagnostic = Diagnostic.Create(Rule, property.GetLocation(), propName);

                            context.ReportDiagnostic(diagnostic);
                        }
                    }
                }
            }
            
        }

        private void AnalyzeArgument(SyntaxNodeAnalysisContext obj)
        {
            var arg = obj.Node as ArgumentSyntax;
            var expr = arg.Expression as AnonymousObjectCreationExpressionSyntax ;
           // var name = expr.Initializers[0].NameEquals.Name.ToString();
        }

        private void AnalyzeObjectInitializer(SyntaxNodeAnalysisContext context)
        {
            var fieldDeclaration = context.Node as FieldDeclarationSyntax;
            if (fieldDeclaration != null)
            {
                if (fieldDeclaration.FullSpan.Start < lastfieldPosition) Fields = new Dictionary<string, string>();
                lastfieldPosition = fieldDeclaration.FullSpan.Start;
                var value = fieldDeclaration.Declaration.Variables.First().Initializer.Value.ChildTokens().First();
                Fields.Add(fieldDeclaration.Declaration.Variables.First().Identifier.Text, value.Text);
            }
            // var initialiser = fieldDeclaration.Declaration.Variables.First().Initializer.Value.ChildTokens().First().Text;

            
        }
        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            // TODO: Replace the following code with your own analysis, generating Diagnostic objects for any issues you find
            var namedTypeSymbol =(IFieldSymbol) context.Symbol;
            var g= namedTypeSymbol.ToDisplayParts();
            var type = namedTypeSymbol.GetType().GetTypeInfo();
            //var prop = type.DeclaredProperties.First(p => p.Name== "VariableDeclaratorSyntax");
            //object value = prop.GetValue(namedTypeSymbol, null);

            //Microsoft.CodeAnalysis.CSharp.Syntax.VariableDeclaratorSyntax
            // Find just those named type symbols with names containing lowercase letters.
            if (namedTypeSymbol.Name.ToCharArray().Any(char.IsLower))
            {
                // For all such symbols, produce a diagnostic.
                var diagnostic = Diagnostic.Create(Rule, namedTypeSymbol.Locations[0], namedTypeSymbol.Name);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
