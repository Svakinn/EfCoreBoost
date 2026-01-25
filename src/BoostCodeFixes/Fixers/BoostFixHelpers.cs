// EfBoostFixHelpers.cs
// Shared helpers for code fixes
// Copyright © 2026  Sveinn S. Erlendsson
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

namespace BoostAnalyzer.Fixers
{
    internal static class BoostFixHelpers
    {
        /// <summary>
        /// Replaces 'invocation' with 'await invocation' keeping trivia and adding a formatter annotation.
        /// </summary>
        internal static async Task<Document> AddAwaitAsync(
            Document document,
            InvocationExpressionSyntax invocation,
            CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

            var awaitExpression =
                SyntaxFactory.AwaitExpression(invocation.WithoutTrivia())
                    .WithLeadingTrivia(invocation.GetLeadingTrivia())
                    .WithTrailingTrivia(invocation.GetTrailingTrivia())
                    .WithAdditionalAnnotations(Formatter.Annotation);

            editor.ReplaceNode(invocation, awaitExpression);
            return editor.GetChangedDocument();
        }
    }
}
