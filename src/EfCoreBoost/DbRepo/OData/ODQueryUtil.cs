using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EfCore.Boost.DbRepo.OData
{
    internal static class ODQueryUtil
    {
        internal static  void AddReport(List<string> report, string msg)
        {
            report.Add(msg);
        }

        internal static bool IsOrderByAllowed(string? rawOrderBy, string[] allowed)
        {
            if (string.IsNullOrWhiteSpace(rawOrderBy)) return true;
            if (allowed == null || allowed.Length == 0) return true;

            var set = new HashSet<string>(allowed.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()), StringComparer.OrdinalIgnoreCase);
            if (set.Count == 0) return true;

            foreach (var part in rawOrderBy.Split(','))
            {
                var p = part.Trim();
                if (p.Length == 0) continue;
                var prop = p.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                if (prop.Length == 0) continue;
                if (!set.Contains(prop)) return false;
            }
            return true;
        }

        internal static void ExtractIncludePaths(IEnumerable<SelectItem> items, HashSet<string> includes, HashSet<string>? allowedSet, int maxDepth, List<string> report, string? prefix = null, int depth = 0)
        {
            if (items == null) return;
            if (depth >= maxDepth) { AddReport(report, "ExpandIgnored.MaxDepth"); return; }

            foreach (var si in items)
            {
                if (si is not ExpandedNavigationSelectItem ex) continue;
                if (HasAnyInnerOptions(ex)) { AddReport(report, "ExpandIgnored.InnerOptions"); continue; }

                var segs = GetNavSegments(ex.PathToNavigationProperty);
                if (segs.Count == 0) { AddReport(report, "ExpandIgnored.NoNavSegments"); continue; }

                var full = JoinPath(prefix, segs);
                if (!IsExpandAllowed(full, segs, allowedSet)) { AddReport(report, "ExpandIgnored.NotAllowed"); continue; }

                includes.Add(full);

                var nested = ex.SelectAndExpand?.SelectedItems;
                if (nested != null && nested.Any())
                    ExtractIncludePaths(nested, includes, allowedSet, maxDepth, report, full, depth + 1);
            }
        }

        internal static bool HasAnyInnerOptions(ExpandedNavigationSelectItem ex)
        {
            // Include-mode: treat any non-trivial nested select/expand as "inner options" and ignore this expand item
            // Conservative by design; you can relax later if needed.
            var sae = ex.SelectAndExpand;
            if (sae == null) return false;
            foreach (var si in sae.SelectedItems)
                if (si is not PathSelectItem) return true;
            return false;
        }

        internal static List<string> GetNavSegments(ODataExpandPath? path)
        {
            var segs = new List<string>();
            if (path == null) return segs;
            foreach (var s in path)
                if (s is NavigationPropertySegment np) segs.Add(np.NavigationProperty.Name);
            return segs;
        }

        internal static string JoinPath(string? prefix, List<string> segs)
        {
            var p = string.Join(".", segs);
            return string.IsNullOrEmpty(prefix) ? p : $"{prefix}.{p}";
        }

        internal static bool IsExpandAllowed(string fullPath, List<string> segs, HashSet<string>? allowedSet)
        {
            if (allowedSet == null || allowedSet.Count == 0) return true;
            var root = segs.Count != 0 ? segs[0] : fullPath;
            return allowedSet.Contains(root) || allowedSet.Contains(fullPath);
        }

        internal static HashSet<string>? BuildAllowSet(string[]? allowed)
        {
            if (allowed == null || allowed.Length == 0) return null;
            var set = new HashSet<string>(allowed.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()), StringComparer.OrdinalIgnoreCase);
            return set.Count == 0 ? null : set;
        }

        internal static bool HasExpand(SelectExpandClause clause)
        {
            foreach (var si in clause.SelectedItems)
                if (si is ExpandedNavigationSelectItem) return true;
            return false;
        }

        internal static bool HasSelect(SelectExpandClause clause)
        {
            // PathSelectItem represents selected structural properties; if clause isn't "select all",
            // treat it as having select intent. This is a practical heuristic.
            foreach (var si in clause.SelectedItems)
                if (si is PathSelectItem) return true;
            return false;
        }

        internal static bool ExpandClauseAllowed(SelectExpandClause clause, HashSet<string> allowedSet, int maxDepth, List<string> report)
        {
            var includes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ExtractIncludePaths(clause.SelectedItems, includes, allowedSet, maxDepth, report);
            // ExtractIncludePaths only adds allowed paths and reports for disallowed.
            // We consider it "allowed" if there is no disallowed expand request that caused a report entry.
            // Minimal rule: if any expand requested but none allowed -> not allowed.
            if (includes.Count == 0) return false;
            return true;
        }

        internal static void ReportNestedExpandOptions(SelectExpandClause clause, List<string> report, string prefix = "")
        {
            foreach (var si in clause.SelectedItems)
            {
                if (si is not ExpandedNavigationSelectItem ex) continue;

                // Best-effort path string for reporting
                var seg = ex.PathToNavigationProperty?.FirstSegment as NavigationPropertySegment;
                var navName = seg?.NavigationProperty?.Name ?? "(unknown-nav)";
                var path = string.IsNullOrEmpty(prefix) ? navName : $"{prefix}.{navName}";

                // Nested options inside $expand
                if (ex.FilterOption != null) report.Add($"ExpandInnerFilterIgnored:{path}");
                if (ex.OrderByOption != null) report.Add($"ExpandInnerOrderByIgnored:{path}");
                if (ex.TopOption != null) report.Add($"ExpandInnerTopIgnored:{path}");
                if (ex.SkipOption != null) report.Add($"ExpandInnerSkipIgnored:{path}");
                if (ex.SelectAndExpand != null)
                {
                    // nested $select/$expand
                    report.Add($"ExpandInnerSelectExpandPresent:{path}");
                    ReportNestedExpandOptions(ex.SelectAndExpand, report, path);
                }
            }
        }
    }
}
