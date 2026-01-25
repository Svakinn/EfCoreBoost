// EFB0004 CodeFix – Replace blocking EF async query usage with await
// Examples:
//   query.ToListAsync().Result           -> await query.ToListAsync()
//   query.ToListAsync().Wait()           -> await query.ToListAsync()
//   query.ToListAsync().GetAwaiter().GetResult() -> await query.ToListAsync()
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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(BlockingEfQueryTaskWaitCodeFixProvider))]
    [Shared]
    public sealed class BlockingEfQueryTaskWaitCodeFixProvider : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(BlockingEfQueryTaskWaitAnalyzer.DiagnosticId); }
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
                        CodeFixResources.CodeFixTitleBlockingEfQuery,
                        ct => ApplyFixAsync(document, node, ct),
                        equivalenceKey: CodeFixResources.CodeFixTitleBlockingEfQuery),
                    diagnostic);
            }
        }

        static async Task<Document> ApplyFixAsync(
            Document document,
            SyntaxNode node,
            CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

            // node should be IdentifierName: "Result", "Wait", or "GetResult"
            var identifier = node as IdentifierNameSyntax;
            if (identifier == null) return document;

            if (identifier.Identifier.Text == "Result")
            {
                // pattern: <asyncInvocation>.Result
                var memberAccess = identifier.Parent as MemberAccessExpressionSyntax;
                if (memberAccess == null) return document;

                var asyncInvocation = memberAccess.Expression as InvocationExpressionSyntax;
                if (asyncInvocation == null) return document;

                var awaitExpr =
                    SyntaxFactory.AwaitExpression(asyncInvocation.WithoutTrivia())
                        .WithLeadingTrivia(memberAccess.GetLeadingTrivia())
                        .WithTrailingTrivia(memberAccess.GetTrailingTrivia())
                        .WithAdditionalAnnotations(Formatter.Annotation);

                editor.ReplaceNode(memberAccess, awaitExpr);
            }
            else if (identifier.Identifier.Text == "Wait")
            {
                // pattern: <asyncInvocation>.Wait()
                var memberAccess = identifier.Parent as MemberAccessExpressionSyntax;
                if (memberAccess == null) return document;

                var waitInvocation = memberAccess.Parent as InvocationExpressionSyntax;
                if (waitInvocation == null) return document;

                var asyncInvocation = memberAccess.Expression as InvocationExpressionSyntax;
                if (asyncInvocation == null) return document;

                var awaitExpr =
                    SyntaxFactory.AwaitExpression(asyncInvocation.WithoutTrivia())
                        .WithLeadingTrivia(waitInvocation.GetLeadingTrivia())
                        .WithTrailingTrivia(waitInvocation.GetTrailingTrivia())
                        .WithAdditionalAnnotations(Formatter.Annotation);

                editor.ReplaceNode(waitInvocation, awaitExpr);
            }
            else if (identifier.Identifier.Text == "GetResult")
            {
                // pattern: <asyncInvocation>.GetAwaiter().GetResult()
                // AST: GetResult identifier -> MemberAccess (GetResult) -> Invocation(GetResult)
                var getResultMember = identifier.Parent as MemberAccessExpressionSyntax;
                if (getResultMember == null) return document;

                var getResultInvocation = getResultMember.Parent as InvocationExpressionSyntax;
                if (getResultInvocation == null) return document;

                var getAwaiterCall = getResultMember.Expression as InvocationExpressionSyntax;
                if (getAwaiterCall == null) return document;

                var getAwaiterMember = getAwaiterCall.Expression as MemberAccessExpressionSyntax;
                if (getAwaiterMember == null) return document;

                var asyncInvocation = getAwaiterMember.Expression as InvocationExpressionSyntax;
                if (asyncInvocation == null) return document;

                var awaitExpr =
                    SyntaxFactory.AwaitExpression(asyncInvocation.WithoutTrivia())
                        .WithLeadingTrivia(getResultInvocation.GetLeadingTrivia())
                        .WithTrailingTrivia(getResultInvocation.GetTrailingTrivia())
                        .WithAdditionalAnnotations(Formatter.Annotation);

                editor.ReplaceNode(getResultInvocation, awaitExpr);
            }

            return editor.GetChangedDocument();
        }
    }
}
