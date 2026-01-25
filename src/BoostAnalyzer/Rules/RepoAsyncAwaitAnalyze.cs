// EFB0005 – Async repository methods must be awaited
// Copyright © 2026 
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;

namespace BoostAnalyzer.Rules
{
    /// <summary>
    /// EFB0005 – Async repository methods must be awaited.
    ///
    /// Detects calls to async repository methods (e.g. ByKeyAsync, QueryNoTrackAsync,
    /// BulkInsertAsync, DeleteWhereAsync, etc.) that are invoked without being awaited.
    /// Fire-and-forget repo calls may cause data loss or hide database errors.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class RepoAsyncAwaitAnalyze : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "EFB0005";
        const string Category = "EfBoost.Async";

        // All async repo methods we require to be awaited
        static readonly string[] TargetNames = new[]
        {
            "ByKeyAsync",
            "ByKeyNoTrackAsync",
            "FirstNoTrackAsync",
            "QueryNoTrackAsync",
            "AnyNoTrackAsync",
            "CountAsync",
            "ApplyOdataFilterAsync",
            "GetBoolScalarAsync",
            "GetLongScalarAsync",
            "GetDecimalScalarAsync",
            "FirstAsync",
            "DeleteWhereAsync",
            "BulkDeleteByIdsAsync",
            "BulkInsertAsync",
            "QueryWithODataAsync"
        };

        static readonly LocalizableString Title =
            new LocalizableResourceString(nameof(Resources.RepoAsyncAwaitTitle), Resources.ResourceManager, typeof(Resources));
        static readonly LocalizableString MessageFormat =
            new LocalizableResourceString(nameof(Resources.RepoAsyncAwaitMessageFormat), Resources.ResourceManager, typeof(Resources));
        static readonly LocalizableString Description =
            new LocalizableResourceString(nameof(Resources.RepoAsyncAwaitDescription), Resources.ResourceManager, typeof(Resources));

        static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
                DiagnosticId,
                Title,
                MessageFormat,
                Category,
                DiagnosticSeverity.Warning,
                isEnabledByDefault: true,
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get { return ImmutableArray.Create(Rule); }
        }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        }

        static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;

            // Already awaited → ok
            if (invocation.Parent is AwaitExpressionSyntax) return;
            // Only bare fire-and-forget expression statements:
            //   repo.ByKeyAsync(...);   // bad
            if (!(invocation.Parent is ExpressionStatementSyntax)) return;
            var semanticModel = context.SemanticModel;
            if (!(semanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol methodSymbol)) return;
            // Filter by name
            var name = methodSymbol.Name;
            if (Array.IndexOf(TargetNames, name) < 0) return;
            // Must be Task / Task<T>
            if (!BoostQueryHelpers.IsTaskLike(methodSymbol.ReturnType)) return;
            // Nice squiggle on the method name if possible
            Location location = invocation.GetLocation();
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                location = memberAccess.Name.GetLocation();
            var diagnostic = Diagnostic.Create(Rule, location, name);
            context.ReportDiagnostic(diagnostic);
        }
    }
}
