using System.Linq;
using System.Net;
using System.Net.Http;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Table;

namespace AzureFunctionsLab
{
    public class Functions
    {
        [FunctionName("Add")] // このメソッドを Azure Function としてマークする FunctionName 属性. 実際のメソッド名自体と一致する必要は無い
        public static int Add(
            // このメソッドが HTTP リクエストによって呼び出されるを示します。
            // このメソッドのパスはデフォルトで /api/Add になります。
            [HttpTrigger(authLevel: AuthorizationLevel.Function, methods: "get", Route = null)]
            HttpRequestMessage req,
            // 診断とエラーのメッセージを記録するために使用できる TraceWriter 
            TraceWriter log
        )
        {
            // URLのクエリ文字列から値を抽出し、提供されたパラメータに基づいて加算を動的に実行できるように
            int x = int.Parse(s: req
                              .GetQueryNameValuePairs()
                              .FirstOrDefault(q => string.Compare(strA: q.Key, strB: "x", ignoreCase: true) == 0)
                              .Value
                             );
            int y = int.Parse(s: req
                              .GetQueryNameValuePairs()
                              .FirstOrDefault(q => string.Compare(q.Key, "y", true) == 0)
                              .Value
                             );
            return x + y;
        }

        public static int Add2(
            HttpRequestMessage req,
            int x,
            int y,
            TraceWriter log
        )
        {
            return x + y;
        }
    }
}
