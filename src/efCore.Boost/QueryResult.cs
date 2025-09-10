using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbRepo
{
    // QueryResult.cs (API or Api.Contracts)
    public class QueryResult<T> where T : class
    {
        public IList<T> Results { get; set; } = new List<T>();
        public long? InlineCount { get; set; } = 0;
        public int PageNo { get; set; } = 0;
        public int ErrorNo { get; set; } = 0;
        public string ErrorMessage { get; set; } = string.Empty;
        public string ErrorDetails { get; set; } = string.Empty;

        public QueryResult() { }
        public QueryResult(Exception e) => FillException(e);

        static string ExceptRecurse(Exception e)
            => e.Message + " \r\n" + (e.InnerException != null ? "Inner exception: " + ExceptRecurse(e.InnerException) : "");

        public void FillException(Exception e) { ErrorNo = 1; ErrorMessage = e.Message; ErrorDetails = ExceptRecurse(e); }
        public void ThrowIfError(string context) { if (ErrorNo != 0) throw new InvalidOperationException($"{context}: {ErrorMessage}\n{ErrorDetails}"); }
    }
}
