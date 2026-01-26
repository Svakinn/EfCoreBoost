// Copyright © 2026  Sveinn S. Erlendsson
// Licensed under the MIT License.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace BoostAnalyzer.Rules
{
    internal static class BoostQueryHelpers
    {
        internal static readonly ImmutableArray<string> SyncQueryTerminalMethods =
            ImmutableArray.Create(
                "ToList", "First", "FirstOrDefault", "Single", "SingleOrDefault", "Any", "All", "Count", "LongCount", "Max", "Min");

        internal static readonly ImmutableArray<string> AsyncQueryTerminalMethods =
            ImmutableArray.Create(
                "ToListAsync", "FirstAsync", "FirstOrDefaultAsync", "SingleAsync", "SingleOrDefaultAsync", "AnyAsync", "AllAsync", "CountAsync",
                "LongCountAsync", "MaxAsync", "MinAsync");

        internal static bool IsQueryableType(ITypeSymbol type)
        {
            if (type == null) return false;
            if ((type.Name == "IQueryable" || type.Name == "IOrderedQueryable") && type.ContainingNamespace.ToDisplayString() == "System.Linq")
                return true;
            foreach (var i in type.AllInterfaces)
            {
                if ((i.Name == "IQueryable" || i.Name == "IOrderedQueryable") && i.ContainingNamespace.ToDisplayString() == "System.Linq")
                    return true;
            }
            return false;
        }

        /// <summary>Returns true if expression is (or returns) an EFBoost/EF query (IQueryable-based).</summary>
        internal static bool IsEfBoostQueryable(ExpressionSyntax expr, SemanticModel model, CancellationToken ct)
        {
            var type = model.GetTypeInfo(expr, ct).Type;
            if (type != null && IsQueryableType(type)) return true;
            return false;
        }

        /// <summary>Returns true if invocation is something like query.ToListAsync() on an IQueryable.</summary>
        internal static bool IsAsyncEfQueryInvocation(InvocationExpressionSyntax invocation, SemanticModel model, CancellationToken ct)
        {
            if (!(model.GetSymbolInfo(invocation, ct).Symbol is IMethodSymbol symbol)) return false;
            if (!AsyncQueryTerminalMethods.Contains(symbol.Name)) return false;
            var expr = invocation.Expression;
            // instance-style: query.ToListAsync()
            if (expr is MemberAccessExpressionSyntax memberAccess)
            {
                if (!IsEfBoostQueryable(memberAccess.Expression, model, ct)) return false;
                return true;
            }
            // static-style: EntityFrameworkQueryableExtensions.ToListAsync(query)
            if (expr is IdentifierNameSyntax || expr is MemberAccessExpressionSyntax)
            {
                if (invocation.ArgumentList.Arguments.Count == 0) return false;
                var firstArg = invocation.ArgumentList.Arguments[0].Expression;
                return IsEfBoostQueryable(firstArg, model, ct);
            }
            return false;
        }

        internal static bool IsTaskLike(ITypeSymbol type)
        {
            if (type == null) return false;
            var display = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return display.StartsWith("global::System.Threading.Tasks.Task", StringComparison.Ordinal);
        }
    }
}
