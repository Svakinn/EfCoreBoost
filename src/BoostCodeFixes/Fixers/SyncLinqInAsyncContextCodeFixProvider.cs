// EFB0003 CodeFix – Convert sync EF LINQ to async + await
// Suggestion: ToList() -> await ToListAsync()
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
using BoostAnalyzer.Rules;

namespace BoostAnalyzer.Fixers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SyncLinqInAsyncContextCodeFixProvider))]
    [Shared]
    public sealed class SyncLinqInAsyncContextCodeFixProvider : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(SyncLinqInAsyncContextAnalyzer.DiagnosticId); }
        }

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var title = CodeFixResources.CodeFixTitleSyncLinqInAsync;
            var document = context.Document;
            var cancellationToken = context.CancellationToken;
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root == null) return;

            foreach (var diagnostic in context.Diagnostics)
            {
                var span = diagnostic.Location.SourceSpan;
                var node = root.FindNode(span);
                if (!(node is IdentifierNameSyntax identifier)) continue;

                if (!(identifier.Parent is MemberAccessExpressionSyntax memberAccess)) continue;

                if (!(memberAccess.Parent is InvocationExpressionSyntax invocation)) continue;

                context.RegisterCodeFix(
                    CodeAction.Create(
                        title,
                        ct => ApplyAsyncFixAsync(document, root, invocation, memberAccess, identifier, ct),
                        equivalenceKey: title),
                    diagnostic);
            }
        }

        static async Task<Document> ApplyAsyncFixAsync(
            Document document,
            SyntaxNode root,
            InvocationExpressionSyntax invocation,
            MemberAccessExpressionSyntax memberAccess,
            IdentifierNameSyntax identifier,
            CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

            // Change ToList -> ToListAsync (or First -> FirstAsync, etc.)
            var asyncNameText = identifier.Identifier.Text + "Async";
            var asyncName = SyntaxFactory.IdentifierName(asyncNameText)
                .WithTriviaFrom(identifier);
            var asyncMemberAccess = memberAccess.WithName(asyncName);
            var asyncInvocation = invocation.WithExpression(asyncMemberAccess);

            // Build: await <asyncInvocation>
            var awaitExpression =
                SyntaxFactory.AwaitExpression(asyncInvocation.WithoutTrivia())
                    .WithLeadingTrivia(invocation.GetLeadingTrivia())
                    .WithTrailingTrivia(invocation.GetTrailingTrivia())
                    .WithAdditionalAnnotations(Formatter.Annotation);

            editor.ReplaceNode(invocation, awaitExpression);

            return editor.GetChangedDocument();
        }
    }
}
