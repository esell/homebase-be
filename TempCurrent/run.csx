using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Bson.IO;
using System.Net;

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    log.Info("starting TempCurrent function...");

    var client = new MongoClient(System.Environment.GetEnvironmentVariable("DBSTRING", EnvironmentVariableTarget.Process));
    var database = client.GetDatabase("esellIot");
    var collection = database.GetCollection<BsonDocument>("esellTemp");

    // parse query parameter
    string deviceName = req.GetQueryNameValuePairs()
        .FirstOrDefault(q => string.Compare(q.Key, "device", true) == 0)
        .Value;

    var filter = Builders<BsonDocument>.Filter.Eq("deviceid", deviceName);
    var sort = Builders<BsonDocument>.Sort.Descending("utc");

    var document = collection.Find(filter).Sort(sort).First();

    log.Info("returning: " + document.ToJson());

    return deviceName == null
        ? req.CreateResponse(HttpStatusCode.BadRequest, "Error")
        : new HttpResponseMessage()
        {
            Content = new StringContent(document.ToString(), System.Text.Encoding.UTF8, "application/json")
        };
}
