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
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;

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

        // api/AddData?name=Madoka&power=1024
        [FunctionName("AddData")]
        public static async Task<HttpResponseMessage> AddData(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)]
            HttpRequestMessage req,
            [Queue("my-test-queue")] IAsyncCollector<string> queue,
            TraceWriter log
        )
        {
            try
            {
                var str = ConfigurationManager
                    .ConnectionStrings["sqldb_connection"]
                    .ConnectionString;

                // URLのクエリ文字列から値を抽出
                var name = req.GetQueryNameValuePairs()
                    .FirstOrDefault(q => string.Compare(q.Key, "name", true) == 0)
                    .Value;

                var power = int.Parse(s: req
                                  .GetQueryNameValuePairs()
                                  .FirstOrDefault(q => string.Compare(strA: q.Key, strB: "power", ignoreCase: true) == 0)
                                  .Value
                                 );

                // Sample user properties
                var user = new { Name = name, Power = power };

                using (SqlConnection conn = new SqlConnection(str))
                {
                    conn.Open();

                    var text = "INSERT INTO TestTable (Name, Power) " +
                            "VALUES(@Name, @Power);" +
                            "SELECT @@IDENTITY;";// 新しく追加されたアイテムのID（自動採番で振られたやつ）
                    using (SqlCommand cmd = new SqlCommand(text, conn))
                    {
                        cmd.Parameters.AddWithValue("@Power", user.Power);
                        cmd.Parameters.AddWithValue("@Name", user.Name);

                        // 新しく追加されたアイテムのID（自動採番で振られたやつ）
                        var newItemID = await cmd.ExecuteScalarAsync();

                        log.Info($"newItemID: {newItemID}");

                        // 新しく追加されたアイテムのIDをキューに送る
                        await queue.AddAsync($"{newItemID}");
                        await queue.FlushAsync();
                        return req.CreateResponse(HttpStatusCode.OK, "pineapple", "application/json");
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


        // Queue trigger
        // SQL DB のキューを見て、もしあったら、1つずつ取ってきて、引数my-test-queueに突っ込む
        // 処理が終わったらキューを空っぽにする
        [FunctionName("QueueTrigger")]
        public static async Task QueueTrigger([QueueTrigger("my-test-queue")] string myQueueItem, TraceWriter log)
        {
            var str = ConfigurationManager
                    .ConnectionStrings["sqldb_connection"]
                    .ConnectionString;
            
            using (SqlConnection conn = new SqlConnection(str))
            {
                conn.Open();

                var sqlQuery = "select ID, Name, power " +
                                "from TestTable " +
                                "where ID = @ID; ";

                using (SqlCommand cmd = new SqlCommand(sqlQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@ID", int.Parse(myQueueItem));

                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                        {
                            var data = new TestTable
                            {
                                ID = r.GetInt32(0),
                                Name = r.GetString(1),
                                Power = r.GetInt32(2)
                            };
                            log.Info(JsonConvert.SerializeObject(data));
                        }
                    }

                }
            }

            log.Info($"C# function processed: {myQueueItem}");
        }


        // Azure Functions から  Azure Storage キューにメッセージを追加
        // HTTP trigger with queue output binding
        [FunctionName("QueueOutput")]
        public static async Task<HttpResponseMessage> QueueOutput
        (
            [HttpTrigger] HttpRequestMessage req,
            [Queue("my-test-queue")] IAsyncCollector<string> queue,
            TraceWriter log
        )
        {
            await queue.AddAsync("popopo-");
            await queue.FlushAsync();
            return req.CreateResponse(HttpStatusCode.OK, "pineapple", "application/json");

        }

    }


    public class TableRow : TableEntity
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Sum { get; set; }
    }

    public class TestTable
    {
        [JsonProperty(PropertyName = "id")]
        public int ID { get; set; }
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
        [JsonProperty(PropertyName = "power")]
        public int Power { get; set; }
    }
}
