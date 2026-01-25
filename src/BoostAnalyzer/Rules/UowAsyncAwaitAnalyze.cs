// Copyright © 2026  Sveinn S. Erlendsson
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace BoostAnalyzer.Rules
{
    /// <summary>
    /// EFB0001 – Async UOW methods must be awaited.
    /// 
    /// Flags calls to important database write or transactional async methods
    /// (e.g. SaveChangesAsync, CommitTransactionAsync, ExecSqlScriptAsync, etc.)
    /// that are invoked without being awaited. Fire-and-forget calls may cause
    /// data loss or hide database errors.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class UowAsyncAwaitAnalyze : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "EFB0001";
        private const string Category = "EfBoost.Async";
        static readonly string[] TargetNames = new[] {
            "CommitTransactionAsync",
            "ExecSqlScriptAsync",
            "ExecSqlCmdAsync",
            "ExecuteInTransactionAsync",
            "RollbackTransactionAsync",
            "BeginTransactionAsync",
            "SaveChangesAsync",
            "SaveChangesAndNewAsync"
        };

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, title: Title, messageFormat: MessageFormat, category: Category, defaultSeverity: DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            // Quick syntax-level short-circuit:
            // If this call is already under an 'await', we don't care.
            if (invocation.Parent is AwaitExpressionSyntax) return;
            // We only care about invocations used as a top-level statement i.e.:
            //   SaveChangesAsync();       // bad
            // NOT:
            //   var t = SaveChangesAsync();       // we'll ignore that for this first rule
            //   return SaveChangesAsync();        // also ignore since acceptable pattern
            if (!(invocation.Parent is ExpressionStatementSyntax)) return;
            var semanticModel = context.SemanticModel;
            var cancellationToken = context.CancellationToken;
            var symbolInfo = semanticModel.GetSymbolInfo(invocation, cancellationToken);
            if (!(symbolInfo.Symbol is IMethodSymbol methodSymbol)) return;
            // Method name must be SaveChangesAsync
            var name = methodSymbol.Name;
            if (Array.IndexOf(TargetNames, name) < 0) return;
            // Must return Task or Task<T>
            if (!BoostQueryHelpers.IsTaskLike(methodSymbol.ReturnType)) return;
            // At this point we know:
            // - someMethod.SaveChangesAsync(...)
            // - returns Task or Task<T>
            // - used as a bare statement, not awaited
            var diagnostic = Diagnostic.Create(descriptor: Rule, location: invocation.GetLocation(), messageArgs: new object[] { methodSymbol.Name });
            context.ReportDiagnostic(diagnostic);
        }
    }
}

