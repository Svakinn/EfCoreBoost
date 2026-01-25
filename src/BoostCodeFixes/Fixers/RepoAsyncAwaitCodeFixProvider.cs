// EFB0005 CodeFix – Async repository methods must be awaited
// Example: _uow.Customers.ByKeyAsync(13);  ->  await _uow.Customers.ByKeyAsync(13);
using BoostAnalyzer.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace BoostAnalyzer.Fixers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RepoAsyncAwaitCodeFixProvider))]
    [Shared]
    public sealed class RepoAsyncAwaitCodeFixProvider : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(BoostAnalyzer.Rules.RepoAsyncAwaitAnalyze.DiagnosticId); }
        }

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var document = context.Document;
            var cancellationToken = context.CancellationToken;
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root == null) return;

            foreach (var diagnostic in context.Diagnostics)
            {
                var span = diagnostic.Location.SourceSpan;
                var node = root.FindNode(span);

                // Diagnostic is on the IdentifierName (METHOD), walk up to invocation
                var invocation = node as InvocationExpressionSyntax ?? node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
                if (invocation == null)
                    continue;

                context.RegisterCodeFix(
                    CodeAction.Create(
                        CodeFixResources.CodeFixTitleRepoAwait,
                        ct => BoostFixHelpers.AddAwaitAsync(document, invocation, ct),
                        equivalenceKey: CodeFixResources.CodeFixTitleRepoAwait),
                    diagnostic);
            }
        }
    }
}
