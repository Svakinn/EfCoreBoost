// EFB0003 – Detect synchronous EF LINQ execution inside async methods
// Copyright © 2026  Sveinn S. Erlendsson
// Licensed under the MIT License.
//
// Purpose:
// --------
// This analyzer enforces proper async usage of Entity Framework Core query execution inside async methods. 
// It detects cases where a developer calls a synchronous LINQ terminal operation (e.g. ToList(), First(), Single(), Count()) on an EF
// IQueryable — inside an async method — instead of using the async equivalents (e.g. ToListAsync(), FirstAsync(), SingleAsync(), CountAsync()).
//
// Why this matters:
// -----------------
// Synchronous blocking in async code prevents EF Core from taking advantage of true asynchronous I/O, reduces scalability,
// and has caused deadlocks in real ASP.NET production environments.
//
// When it triggers:
// -----------------
// ✔ Inside async methods
// ✔ On EF/IQueryable (or UoW/Repo query wrappers)
// ✔ When a sync terminal operation is used that has an async counterpart
//
// When it does NOT trigger:
// -------------------------
// ✘ Outside async methods
// ✘ On in-memory collections (IEnumerable<T>)
// ✘ When the async version is already used
//
// Example of violation:
//     var list = query.Where(x => x.IsActive).ToList();  // EFB0003 warning
//
// Recommended fix:
//     var list = await query.Where(x => x.IsActive).ToListAsync();
//
// This rule reinforces EF Boost’s goal: async all the way down.
//

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace BoostAnalyzer.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class SyncLinqInAsyncContextAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "EFB0003";
        const string Category = "EfBoost.Async";

        static readonly LocalizableString Title =
            new LocalizableResourceString(nameof(Resources.SyncLinqInAsyncTitle), Resources.ResourceManager, typeof(Resources));
        static readonly LocalizableString MessageFormat =
            new LocalizableResourceString(nameof(Resources.SyncLinqInAsyncMessageFormat), Resources.ResourceManager, typeof(Resources));
        static readonly LocalizableString Description =
            new LocalizableResourceString(nameof(Resources.SyncLinqInAsyncDescription), Resources.ResourceManager, typeof(Resources));
        static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Info, isEnabledByDefault: true, description: Description);

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
            if (!(context.ContainingSymbol is IMethodSymbol containingMethod) || !containingMethod.IsAsync) return;
            var invocation = (InvocationExpressionSyntax)context.Node;
            if (!(invocation.Expression is MemberAccessExpressionSyntax memberAccess)) return;
            var model = context.SemanticModel;
            if (!(model.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol symbol)) return;
            if (!BoostQueryHelpers.SyncQueryTerminalMethods.Contains(symbol.Name)) return;
            if (!BoostQueryHelpers.IsEfBoostQueryable(memberAccess.Expression, model, context.CancellationToken)) return;
            var diag = Diagnostic.Create(Rule, memberAccess.Name.GetLocation(), symbol.Name);
            context.ReportDiagnostic(diag);
        }
    }
}
