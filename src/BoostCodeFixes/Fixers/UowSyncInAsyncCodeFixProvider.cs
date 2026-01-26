// EFB0002 CodeFix – Replace synchronous UOW method with async + await
// Example: SaveChangesSynchronized() -> await SaveChangesAsync()
using System.Collections.Generic;
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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UowSyncInAsyncCodeFixProvider))]
    [Shared]
    public sealed class UowSyncInAsyncCodeFixProvider : CodeFixProvider
    {

        static readonly ImmutableDictionary<string, string> AsyncCounterparts =
            new Dictionary<string, string>
            {
                { "SaveChangesSynchronized", "SaveChangesAsync" },
                { "SaveChangesAndNewSynchronized", "SaveChangesAndNewAsync" },
                { "CommitTransactionSynchronized", "CommitTransactionAsync" },
                { "ExecSqlScriptSynchronized", "ExecSqlScriptAsync" },
                { "ExecSqlCmdSynchronized", "ExecSqlCmdAsync" },
                { "ExecuteInTransactionSynchronized", "ExecuteInTransactionAsync" },
                { "RollbackTransactionSynchronized", "RollbackTransactionAsync" },
                { "BeginTransactionSynchronized", "BeginTransactionAsync" }
            }.ToImmutableDictionary();

        public override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(BoostAnalyzer.Rules.UowSyncInAsyncAnalyze.DiagnosticId); }
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
                context.RegisterCodeFix(
                    CodeAction.Create(
                        CodeFixResources.CodeFixTitleSyncInAsync,
                        ct => ApplyFixAsync(document, node, ct),
                        equivalenceKey: CodeFixResources.CodeFixTitleSyncInAsync),
                    diagnostic);
            }
        }

        static async Task<Document> ApplyFixAsync(
            Document document,
            SyntaxNode node,
            CancellationToken cancellationToken)
        {
            // The diagnostic might be on the IdentifierName or MemberAccess; walk up to the invocation
            var invocation = node as InvocationExpressionSyntax ?? node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
            if (invocation == null)
                return document;

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (semanticModel == null)
                return document;

            if (!(semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol methodSymbol))
                return document;

            if (!AsyncCounterparts.TryGetValue(methodSymbol.Name, out var asyncName) || string.IsNullOrEmpty(asyncName))
                return document;

            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

            if (!(invocation.Expression is MemberAccessExpressionSyntax memberAccess))
                return document;

            // Replace METHOD() with METHOD_ASYNC()
            var newName = SyntaxFactory.IdentifierName(asyncName)
                .WithTriviaFrom(memberAccess.Name);
            var newMemberAccess = memberAccess.WithName(newName);
            var newInvocation = invocation.WithExpression(newMemberAccess);

            // Wrap in await, preserving trivia
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
