#r "Newtonsoft.Json"

using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Bson.IO;
using System.Net;
using Newtonsoft.Json;

struct SoilSample
{
    public int moistureLevel;
    public string sampleDate;
}

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    log.Info("starting SoilHistory function...");

    var client = new MongoClient(System.Environment.GetEnvironmentVariable("DBSTRING", EnvironmentVariableTarget.Process));
    var database = client.GetDatabase("esellIot");
    var collection = database.GetCollection<BsonDocument>("esellMoisture");

    // parse query parameter
    string deviceName = req.GetQueryNameValuePairs()
	.FirstOrDefault(q => string.Compare(q.Key, "device", true) == 0)
	.Value;

    var filter = Builders<BsonDocument>.Filter.Eq("deviceid", deviceName);
    var sort = Builders<BsonDocument>.Sort.Descending("utc");

    List<SoilSample> allSamples = new List<SoilSample>();
    using (var cursor = collection.Find(filter).Sort(sort).Limit(168).ToCursor()) {
        while (await cursor.MoveNextAsync())
        {
            foreach (var doc in cursor.Current)
            {
                SoilSample tempSample = new SoilSample();
                tempSample.moistureLevel = doc.GetValue("moisture").ToInt32();
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

