// EFB0004 – Blocking on EF async query tasks (Result/Wait/GetAwaiter().GetResult) inside async methods
// Copyright © 2026  Sveinn S. Erlendsson
// Licensed under the MIT License.
//
// Purpose:
// --------
// This analyzer detects cases where code blocks on EF Core async query tasks
// (e.g. ToListAsync(), FirstAsync(), SingleAsync(), CountAsync()) by using
// Result, Wait(), or GetAwaiter().GetResult() inside an async method.
//
// Instead of:
//
//     var list = query.ToListAsync().Result;
//     query.ToListAsync().Wait();
//     var list = query.ToListAsync().GetAwaiter().GetResult();
//
// developers should use:
//
//     var list = await query.ToListAsync();
//
// Why this matters:
// -----------------
// Blocking on Task in async code:
//   - Defeats the purpose of async EF Core I/O
//   - Can cause thread pool starvation
//   - Has caused deadlocks in real-world ASP.NET applications
//
// This rule is scoped to EF/UoW/Repo query tasks to avoid noisy diagnostics
// on unrelated Task usage (e.g. HttpClient, timers, etc.).
//
// When it triggers:
// -----------------
// ✔ Inside async methods
// ✔ On EF async query invocations (e.g. query.ToListAsync())
// ✔ When using:
//      - .Result
//      - .Wait()
//      - .GetAwaiter().GetResult()
//
// When it does NOT trigger:
// -------------------------
// ✘ Outside async methods
// ✘ When simply awaiting the async query (await query.ToListAsync())
// ✘ On non-EF tasks that are outside the EfBoost/UoW query patterns
//
// Example of violation:
//     var list = query.Where(x => x.IsActive).ToListAsync().Result;  // EFB0004 warning
//
// Recommended fix:
//     var list = await query.Where(x => x.IsActive).ToListAsync();
//
// This rule supports EfBoost’s guideline: never block on async EF queries.
//

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace BoostAnalyzer.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class BlockingEfQueryTaskWaitAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "EFB0004";
        const string Category = "EfBoost.Async";

        static readonly LocalizableString Title =
            new LocalizableResourceString(nameof(Resources.BlockingEfQueryTaskWaitTitle), Resources.ResourceManager, typeof(Resources));
        static readonly LocalizableString MessageFormat =
            new LocalizableResourceString(nameof(Resources.BlockingEfQueryTaskWaitMessageFormat), Resources.ResourceManager, typeof(Resources));
        static readonly LocalizableString Description =
            new LocalizableResourceString(nameof(Resources.BlockingEfQueryTaskWaitDescription), Resources.ResourceManager, typeof(Resources));
        static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get { return ImmutableArray.Create(Rule); }
        }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
            context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
        }

        static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            if (!(context.ContainingSymbol is IMethodSymbol containingMethod) || !containingMethod.IsAsync) return;
            var invocation = (InvocationExpressionSyntax)context.Node;
            if (!(invocation.Expression is MemberAccessExpressionSyntax memberAccess)) return;
            var model = context.SemanticModel;
            if (!(model.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol symbol)) return;
            var name = symbol.Name;
            // case 1: query.ToListAsync().GetAwaiter().GetResult()
            if (name == "GetResult")
            {
                if (memberAccess.Expression is InvocationExpressionSyntax getAwaiterCall)
                {
                    if (getAwaiterCall.Expression is MemberAccessExpressionSyntax innerMember &&
                        innerMember.Name.Identifier.Text == "GetAwaiter")
                    {
                        if (innerMember.Expression is InvocationExpressionSyntax asyncQueryCall &&
                            BoostQueryHelpers.IsAsyncEfQueryInvocation(asyncQueryCall, model, context.CancellationToken))
                        {
                            var diag = Diagnostic.Create(Rule, memberAccess.Name.GetLocation(), "GetAwaiter().GetResult()");
                            context.ReportDiagnostic(diag);
                        }
                    }
                }
                return;
            }

            // case 2: query.ToListAsync().Wait()
            if (name == "Wait")
            {
                if (memberAccess.Expression is InvocationExpressionSyntax waitTarget &&
                    BoostQueryHelpers.IsAsyncEfQueryInvocation(waitTarget, model, context.CancellationToken))
                {
                    var diag = Diagnostic.Create(Rule, memberAccess.Name.GetLocation(), "Wait()");
                    context.ReportDiagnostic(diag);
                }
                return;
            }

            // (We could add Task.WaitAll/WaitAny on EF tasks, but they’re rare and more complex to detect.)
        }

        static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
        {
            if (!(context.ContainingSymbol is IMethodSymbol containingMethod) || !containingMethod.IsAsync) return;
            var memberAccess = (MemberAccessExpressionSyntax)context.Node;
            if (memberAccess.Name.Identifier.Text != "Result") return;
            var model = context.SemanticModel;
            // Only if expression is an async EF query invocation, e.g. query.ToListAsync().Result
            if (memberAccess.Expression is InvocationExpressionSyntax asyncQueryCall &&
                BoostQueryHelpers.IsAsyncEfQueryInvocation(asyncQueryCall, model, context.CancellationToken))
            {
                var diag = Diagnostic.Create(Rule, memberAccess.Name.GetLocation(), "Result");
                context.ReportDiagnostic(diag);
            }
        }
    }
}
