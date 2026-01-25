// EFB0006 CodeFix – Replace synchronous repo methods with async + await
// Example: _uow.Customers.ByKeySynchronized(13) -> await _uow.Customers.ByKeyAsync(13);
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

namespace BoostAnalyzer.Fixers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RepoSyncInAsyncCodeFixProvider))]
    [Shared]
    public sealed class RepoSyncInAsyncCodeFixProvider : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(BoostAnalyzer.Rules.RepoSyncInAsyncAnalyze.DiagnosticId); }
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

                // Diagnostic is on IdentifierName or MemberAccess; walk up to the invocation
                var invocation = node as InvocationExpressionSyntax ?? node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
                if (invocation == null)
                    continue;

                context.RegisterCodeFix(
                    CodeAction.Create(
                        CodeFixResources.CodeFixTitleRepoSync,
                        ct => ApplyFixAsync(document, invocation, ct),
                        equivalenceKey: CodeFixResources.CodeFixTitleRepoSync),
                    diagnostic);
            }
        }

        static async Task<Document> ApplyFixAsync(Document document, InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (semanticModel == null)
                return document;

            if (!(semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol methodSymbol))
                return document;

            var syncName = methodSymbol.Name;
            if (!syncName.EndsWith("Synchronized"))
                return document;

            var asyncName = syncName.Substring(0, syncName.Length - "Synchronized".Length) + "Async";

            if (!(invocation.Expression is MemberAccessExpressionSyntax memberAccess))
                return document;

            var newName = SyntaxFactory.IdentifierName(asyncName).WithTriviaFrom(memberAccess.Name);
            var newMemberAccess = memberAccess.WithName(newName);
            var newInvocation = invocation.WithExpression(newMemberAccess);

            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

            var awaitExpression =
                SyntaxFactory.AwaitExpression(newInvocation.WithoutTrivia())
                    .WithLeadingTrivia(invocation.GetLeadingTrivia())
                    .WithTrailingTrivia(invocation.GetTrailingTrivia())
                    .WithAdditionalAnnotations(Formatter.Annotation);

            editor.ReplaceNode(invocation, awaitExpression);

            return editor.GetChangedDocument();
        }
    }
}
