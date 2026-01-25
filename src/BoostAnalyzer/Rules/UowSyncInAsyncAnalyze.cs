// EFB0005 – Avoid synchronous UOW methods in async code
// Copyright © 2026  Sveinn S. Erlendsson
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
    /// EFB0002 – Avoid synchronous UOW methods in async code.
    ///
    /// Detects calls to synchronous Unit-of-Work / DbContext methods such as
    /// SaveChangesSynchronized, CommitTransactionSynchronized, ExecSqlScriptSynchronized, etc.,
    /// when they are used inside async methods. These calls block the thread and should be
    /// replaced with the corresponding async methods.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class UowSyncInAsyncAnalyze : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "EFB0002";
        const string Category = "EfBoost.Async";

        static readonly string[] SyncMethods = new[]
        {
            "SaveChangesSynchronized",
            "SaveChangesAndNewSynchronized",
            "CommitTransactionSynchronized",
            "ExecSqlScriptSynchronized",
            "ExecSqlCmdSynchronized",
            "ExecuteInTransactionSynchronized",
            "RollbackTransactionSynchronized",
            "BeginTransactionSynchronized"
        };

        static readonly LocalizableString Title =
            new LocalizableResourceString(nameof(Resources.UowSyncInAsyncTitle), Resources.ResourceManager, typeof(Resources));
        static readonly LocalizableString MessageFormat =
            new LocalizableResourceString(nameof(Resources.UowSyncInAsyncMessageFormat), Resources.ResourceManager, typeof(Resources));
        static readonly LocalizableString Description =
            new LocalizableResourceString(nameof(Resources.UowSyncInAsyncDescription), Resources.ResourceManager, typeof(Resources));

        static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(
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
            if (!(context.ContainingSymbol is IMethodSymbol containingMethod) || !containingMethod.IsAsync) return; // we only care inside async methods
            var invocation = (InvocationExpressionSyntax)context.Node;
            var model = context.SemanticModel;
            if (!(model.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol methodSymbol)) return;
            var name = methodSymbol.Name;
            if (Array.IndexOf(SyncMethods, name) < 0) return;
            // For nicer squiggle, try to locate the method name token
            Location location = invocation.GetLocation();
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                location = memberAccess.Name.GetLocation();
            var diagnostic = Diagnostic.Create(Rule, location, name);
            context.ReportDiagnostic(diagnostic);
        }
    }
}
