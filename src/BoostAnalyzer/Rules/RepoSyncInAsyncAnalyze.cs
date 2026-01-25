// EFB0006 – Avoid synchronous Repo methods in async code.
// Copyright © 2026 
// MIT License

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;

namespace BoostAnalyzer.Rules
{
    /// <summary>
    /// EFB0006 – Avoid synchronous repository methods in async methods.
    ///
    /// Detects calls to synchronous Repo methods (e.g. ByKeySynchronized, BulkInsertSynchronized)
    /// when used inside async methods. These block execution threads and can cause deadlocks —
    /// the async counterparts should be used instead.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class RepoSyncInAsyncAnalyze : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "EFB0006";
        private const string Category = "EfBoost.Async";

        static readonly string[] SyncMethods = new[]
        {
            "ByKeySynchronized",
            "ByKeyNoTrackSynchronized",
            "FirstNoTrackSynchronized",
            "QueryNoTrackSynchronized",
            "AnyNoTrackSynchronized",
            "CountSynchronized",
            "ApplyOdataFilterSynchronized",
            "GetBoolScalarSynchronized",
            "GetLongScalarSynchronized",
            "GetDecimalScalarSynchronized",
            "FirstSynchronized",
            "DeleteWhereSynchronized",
            "BulkDeleteByIdsSynchronized",
            "BulkInsertSynchronized",
            "QueryWithODataSynchronized"
        };

        static readonly LocalizableString Title =
            new LocalizableResourceString(nameof(Resources.RepoSyncInAsyncTitle), Resources.ResourceManager, typeof(Resources));
        static readonly LocalizableString MessageFormat =
            new LocalizableResourceString(nameof(Resources.RepoSyncInAsyncMessageFormat), Resources.ResourceManager, typeof(Resources));
        static readonly LocalizableString Description =
            new LocalizableResourceString(nameof(Resources.RepoSyncInAsyncDescription), Resources.ResourceManager, typeof(Resources));
        static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>  ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        }

        static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            if (!(context.ContainingSymbol is IMethodSymbol method) || !method.IsAsync) return;
            var invocation = (InvocationExpressionSyntax)context.Node;
            if (!(context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol symbol)) return;
            if (Array.IndexOf(SyncMethods, symbol.Name) < 0) return;
            var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
            var location = memberAccess?.Name?.GetLocation() ?? invocation.GetLocation();
            context.ReportDiagnostic(Diagnostic.Create(Rule, location, symbol.Name));
        }
    }
}
