using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
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

        [FunctionName("Process")]
        [return: Table("Results")]
        public static TableRow Process(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "Process/{x:int}/{y:int}")]
            HttpRequestMessage req,
            int x,
            int y,
            TraceWriter log, 
            [Table("Results", "sums", "{x}_{y}")]
            TableRow tableRow
        )
        {
            if (tableRow != null)
            {
                log.Info($"{x} + {y} already exists");
                return null;
            }
            log.Info($"Processing {x} + {y}");

            return new TableRow()
            { 
                PartitionKey = "sums",
                RowKey = $"{x}_{y}",
                X = x,
                Y = y,
                Sum = x + y
            };
        }


        [FunctionName("List")]
        public static HttpResponseMessage List(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)]
            HttpRequestMessage req,
            [Table("Results", "sums")]
            IQueryable<TableRow> table,
            TraceWriter log
        )
        {
            return req.CreateResponse(HttpStatusCode.OK, table, "application/json");
        }

        [FunctionName("AddData")]
        public static async Task<HttpResponseMessage> AddData(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)]
            HttpRequestMessage req,
            TraceWriter log
        )
        {
            try
            {
                var str = ConfigurationManager
                    .ConnectionStrings["sqldb_connection"]
                    .ConnectionString;

                using (SqlConnection conn = new SqlConnection(str))
                {
                    conn.Open();

                    var text = "INSERT INTO Contact (Id, LASTNAME, FIRSTNAME) " +
                            "VALUES( 5, 'Chiyoda', 'Madoka')";
                    using (SqlCommand cmd = new SqlCommand(text, conn))
                    {
                        // Execute the command and log the # rows affected.
                        var rows = await cmd.ExecuteNonQueryAsync();
                        log.Info($"{rows} rows were updated");
                    }
                    return req.CreateResponse(HttpStatusCode.OK, "test", "application/json");
                }
            }
            catch (System.Exception ex)
            {
                log.Info(message: $"しんでしまうとは　なさけない！　{ex}");
            }
            return null;
        }

    }


    public class TableRow : TableEntity
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Sum { get; set; }
    }
}
