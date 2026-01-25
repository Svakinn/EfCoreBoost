using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EfCore.Boost.UOW
{
    internal static class SqlScriptSplitters
    {
        // PostgreSQL: split by ';' but ignore semicolons inside:
        // - single quotes: '...'
        // - double quotes: "..." (identifiers)
        // - line comments: -- ...
        // - block comments: /* ... */
        // - dollar quotes: $$...$$ or $tag$...$tag$
        public static IEnumerable<string> SplitPostgres(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql)) yield break;
            var sb = new StringBuilder();
            var i = 0;
            var n = sql.Length;
            bool inS = false, inDq = false, inLine = false, inBlock = false;
            string? dqTag = null;

            while (i < n)
            {
                var c = sql[i];

                if (inLine)
                {
                    sb.Append(c);
                    if (c == '\n') inLine = false;
                    i++;
                    continue;
                }

                if (inBlock)
                {
                    sb.Append(c);
                    if (c == '*' && i + 1 < n && sql[i + 1] == '/')
                    {
                        sb.Append('/');
                        i += 2;
                        inBlock = false;
                        continue;
                    }
                    i++;
                    continue;
                }

                if (dqTag != null)
                {
                    sb.Append(c);
                    if (c == '$' && MatchesAt(sql, i, dqTag))
                    {
                        for (var k = 1; k < dqTag.Length; k++) sb.Append(sql[i + k]);
                        i += dqTag.Length;
                        dqTag = null;
                        continue;
                    }
                    i++;
                    continue;
                }

                if (inS)
                {
                    sb.Append(c);
                    if (c == '\'')
                    {
                        if (i + 1 < n && sql[i + 1] == '\'') { sb.Append('\''); i += 2; continue; } // '' escape
                        inS = false;
                    }
                    i++;
                    continue;
                }

                if (inDq)
                {
                    sb.Append(c);
                    if (c == '"')
                    {
                        if (i + 1 < n && sql[i + 1] == '"') { sb.Append('"'); i += 2; continue; } // "" escape (ident)
                        inDq = false;
                    }
                    i++;
                    continue;
                }

                // Enter comments
                if (c == '-' && i + 1 < n && sql[i + 1] == '-') { sb.Append("--"); i += 2; inLine = true; continue; }
                if (c == '/' && i + 1 < n && sql[i + 1] == '*') { sb.Append("/*"); i += 2; inBlock = true; continue; }

                // Enter quotes
                if (c == '\'') { sb.Append(c); inS = true; i++; continue; }
                if (c == '"') { sb.Append(c); inDq = true; i++; continue; }

                // Enter dollar quote: $tag$ ... $tag$
                if (c == '$')
                {
                    var tag = TryReadPgDollarTag(sql, i);
                    if (tag != null)
                    {
                        sb.Append(tag);
                        i += tag.Length;
                        dqTag = tag;
                        continue;
                    }
                }

                // Split at semicolon
                if (c == ';')
                {
                    var stmt = sb.ToString().Trim();
                    sb.Clear();
                    i++;
                    if (stmt.Length > 0) yield return stmt;
                    continue;
                }

                sb.Append(c);
                i++;
            }

            var last = sb.ToString().Trim();
            if (last.Length > 0) yield return last;
        }

        // MySQL: supports DELIMITER changes and splits by current delimiter (default ';').
        // Ignores delimiters inside:
        // - single quotes: '...'
        // - double quotes: "..." (strings depending on sql_mode, but we treat as string for safety)
        // - backticks: `...` (identifiers)
        // - line comments: -- ... , # ...
        // - block comments: /* ... */
        // DELIMITER directives must appear alone on a line (common mysql-cli style):
        //   DELIMITER $$
        //   CREATE PROCEDURE ... $$ 
        //   DELIMITER ;
        public static IEnumerable<string> SplitMySql(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql)) yield break;

            var sb = new StringBuilder();
            var i = 0;
            var n = sql.Length;
            bool inS = false, inDq = false, inBt = false, inLine = false, inBlock = false;
            string delimiter = ";";

            while (i < n)
            {
                // Detect DELIMITER directive at line start (ignoring whitespace), only when not in any state.
                if (!inS && !inDq && !inBt && !inLine && !inBlock)
                {
                    if (TryReadDelimiterDirective(sql, ref i, out var newDelim))
                    {
                        delimiter = newDelim;
                        // do not add DELIMITER line to script output
                        continue;
                    }
                }

                var c = sql[i];

                if (inLine)
                {
                    sb.Append(c);
                    if (c == '\n') inLine = false;
                    i++;
                    continue;
                }

                if (inBlock)
                {
                    sb.Append(c);
                    if (c == '*' && i + 1 < n && sql[i + 1] == '/')
                    {
                        sb.Append('/');
                        i += 2;
                        inBlock = false;
                        continue;
                    }
                    i++;
                    continue;
                }

                if (inS)
                {
                    sb.Append(c);
                    if (c == '\'')
                    {
                        if (i + 1 < n && sql[i + 1] == '\'') { sb.Append('\''); i += 2; continue; } // '' escape
                        if (i > 0 && sql[i - 1] == '\\') { i++; continue; } // \'
                        inS = false;
                    }
                    i++;
                    continue;
                }

                if (inDq)
                {
                    sb.Append(c);
                    if (c == '"')
                    {
                        if (i > 0 && sql[i - 1] == '\\') { i++; continue; } // \"
                        inDq = false;
                    }
                    i++;
                    continue;
                }

                if (inBt)
                {
                    sb.Append(c);
                    if (c == '`')
                    {
                        if (i + 1 < n && sql[i + 1] == '`') { sb.Append('`'); i += 2; continue; } // `` escape
                        inBt = false;
                    }
                    i++;
                    continue;
                }

                // Enter comments
                if (c == '#') { sb.Append(c); i++; inLine = true; continue; }
                if (c == '-' && i + 1 < n && sql[i + 1] == '-')
                {
                    // MySQL treats '-- ' (dash dash space) as comment (mysql-cli style). We'll accept '--' if followed by whitespace or EOL.
                    var next = i + 2 < n ? sql[i + 2] : '\n';
                    if (char.IsWhiteSpace(next) || next == '\r' || next == '\n')
                    {
                        sb.Append("--");
                        i += 2;
                        inLine = true;
                        continue;
                    }
                }
                if (c == '/' && i + 1 < n && sql[i + 1] == '*') { sb.Append("/*"); i += 2; inBlock = true; continue; }

                // Enter quotes
                if (c == '\'') { sb.Append(c); inS = true; i++; continue; }
                if (c == '"') { sb.Append(c); inDq = true; i++; continue; }
                if (c == '`') { sb.Append(c); inBt = true; i++; continue; }

                // Split at current delimiter (supports multi-char delimiters like $$, //, etc.)
                if (delimiter.Length > 0 && MatchesAt(sql, i, delimiter))
                {
                    var stmt = sb.ToString().Trim();
                    sb.Clear();
                    i += delimiter.Length;
                    if (stmt.Length > 0) yield return stmt;
                    continue;
                }

                sb.Append(c);
                i++;
            }

            var last = sb.ToString().Trim();
            if (last.Length > 0) yield return last;
        }


        static readonly Regex GoRegex = new(@"^\s*GO(?:\s+(?<count>\d+))?\s*(?:--.*)?$",  RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // MsSQL: split by lines containing only "GO" (case insensitive)
        public static IEnumerable<string> SplitMsSql(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql)) yield break;

            var sb = new StringBuilder();
            using var reader = new StringReader(sql);
            string? line;

            while ((line = reader.ReadLine()) != null)
            {
                var m = GoRegex.Match(line);
                if (!m.Success) { sb.AppendLine(line); continue; }

                var batchText = sb.ToString();
                sb.Clear();

                if (!string.IsNullOrWhiteSpace(batchText))
                {
                    var repeat = 1;
                    if (m.Groups["count"].Success && int.TryParse(m.Groups["count"].Value, out var n) && n > 0) repeat = n;
                    for (var i = 0; i < repeat; i++) yield return batchText;
                }
            }

            var last = sb.ToString();
            if (!string.IsNullOrWhiteSpace(last)) yield return last;
        }

        static bool MatchesAt(string s, int idx, string token)
        {
            if (idx + token.Length > s.Length) return false;
            for (var k = 0; k < token.Length; k++)
                if (s[idx + k] != token[k]) return false;
            return true;
        }

        static string? TryReadPgDollarTag(string s, int idx)
        {
            // Valid forms: $$ or $tag$ where tag is [A-Za-z_][A-Za-z0-9_]* (Postgres is permissive, but this is good)
            if (idx >= s.Length || s[idx] != '$') return null;
            var j = idx + 1;
            if (j < s.Length && s[j] == '$') return "$$";
            if (j >= s.Length) return null;
            if (!(char.IsLetter(s[j]) || s[j] == '_')) return null;
            j++;
            while (j < s.Length && (char.IsLetterOrDigit(s[j]) || s[j] == '_')) j++;
            if (j < s.Length && s[j] == '$') return s.Substring(idx, j - idx + 1);
            return null;
        }

        static bool TryReadDelimiterDirective(string s, ref int i, out string newDelimiter)
        {
            newDelimiter = "";
            // Must be at start of a line ignoring whitespace, and match: DELIMITER <token>
            var n = s.Length;
            var lineStart = FindLineStart(s, i);
            var j = lineStart;
            while (j < n && (s[j] == ' ' || s[j] == '\t' || s[j] == '\r')) j++;
            if (j + 9 > n) return false;
            if (!EqualsAsciiInsensitive(s, j, "DELIMITER")) return false;
            j += 9;
            if (j < n && !(s[j] == ' ' || s[j] == '\t')) return false;
            while (j < n && (s[j] == ' ' || s[j] == '\t')) j++;
            if (j >= n) return false;

            // Read delimiter token up to end-of-line (trim right)
            var k = j;
            while (k < n && s[k] != '\n') k++;
            var token = s.Substring(j, k - j).Trim();
            if (token.Length == 0) return false;

            // Advance i to char after line (consume directive line)
            i = k < n ? k + 1 : n;
            newDelimiter = token;
            return true;
        }

        static int FindLineStart(string s, int idx)
        {
            var i = Math.Min(idx, s.Length);
            while (i > 0)
            {
                var c = s[i - 1];
                if (c == '\n') break;
                i--;
            }
            return i;
        }

        static bool EqualsAsciiInsensitive(string s, int idx, string kw)
        {
            if (idx + kw.Length > s.Length) return false;
            for (var i = 0; i < kw.Length; i++)
            {
                var a = s[idx + i];
                var b = kw[i];
                if (a == b) continue;
                if (a >= 'a' && a <= 'z') a = (char)(a - 32);
                if (b >= 'a' && b <= 'z') b = (char)(b - 32);
                if (a != b) return false;
            }
            return true;
        }
    }

}