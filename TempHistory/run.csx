#r "Newtonsoft.Json"

using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Bson.IO;
using System.Net;
using Newtonsoft.Json;

struct TempSample
{
    public int temp;
    public string sampleDate;
}

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    log.Info("starting TempHistory function...");

    var client = new MongoClient(System.Environment.GetEnvironmentVariable("DBSTRING", EnvironmentVariableTarget.Process));
    var database = client.GetDatabase("esellIot");
    var collection = database.GetCollection<BsonDocument>("esellTemp");

    // parse query parameter
    string deviceName = req.GetQueryNameValuePairs()
	.FirstOrDefault(q => string.Compare(q.Key, "device", true) == 0)
	.Value;

    var filter = Builders<BsonDocument>.Filter.Eq("deviceid", deviceName);
    var sort = Builders<BsonDocument>.Sort.Descending("utc");

    List<TempSample> allSamples = new List<TempSample>();
    using (var cursor = collection.Find(filter).Sort(sort).Limit(168).ToCursor()) {
        while (await cursor.MoveNextAsync())
        {
            foreach (var doc in cursor.Current)
            {
                TempSample tempSample = new TempSample();
                // we don't need a float here...
                tempSample.temp = doc.GetValue("temp").ToInt32();
                tempSample.sampleDate =  doc.GetValue("utc").ToString();
                allSamples.Add(tempSample);
            }
        }
    }
    string json = Newtonsoft.Json.JsonConvert.SerializeObject(allSamples);
    log.Info("all: " + json);
    
    return deviceName == null
	? req.CreateResponse(HttpStatusCode.BadRequest, "Error")
	: new HttpResponseMessage()
	{
	    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
	};
}

