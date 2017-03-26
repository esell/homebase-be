#r "Newtonsoft.Json"

using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Bson.IO;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using Twilio;


public class Foo
{
    public dynamic CallRestService(string uri, string method)
    {
        Console.Write("SHIT");
        dynamic result;

        var req = HttpWebRequest.Create(uri);
        req.Method = method;
        req.ContentType = "application/json";
        // long timeout to account for function "warm-up"
        req.Timeout = 15000;

        using (var resp = req.GetResponse())
        {
            var results = new StreamReader(resp.GetResponseStream()).ReadToEnd();
            result = Newtonsoft.Json.Linq.JObject.Parse(results);
        }
        return result;
    }
}

public static void Run(TimerInfo myTimer, TraceWriter log)
{
    var client = new MongoClient(System.Environment.GetEnvironmentVariable("DBSTRING", EnvironmentVariableTarget.Process));
    var database = client.GetDatabase("esellIot");
    var moistureCollection = database.GetCollection<BsonDocument>("esellMoisture");
    var thresholdCollection = database.GetCollection<BsonDocument>("alertThresholds");
    var existingAlertsCollection = database.GetCollection<BsonDocument>("activeAlerts");
    var readingURL = "https://esell-iot.azurewebsites.net/api/SoilCurrent?code=5XRuUaAN45m8q8UM8NeZipu7zLgEWX/7FzxmBPYMMUX9qyW5u/U8fQ==&device=";
    var count = thresholdCollection.Count(new BsonDocument());
    var twilioAccountSid = System.Environment.GetEnvironmentVariable("TWILIO_ACCT_SID", EnvironmentVariableTarget.Process);
    var twilioAuthToken = System.Environment.GetEnvironmentVariable("TWILIO_AUTH_TOKEN", EnvironmentVariableTarget.Process);
    var twilioTo = System.Environment.GetEnvironmentVariable("TWILIO_TO", EnvironmentVariableTarget.Process);
    var twilioFrom = System.Environment.GetEnvironmentVariable("TWILIO_FROM", EnvironmentVariableTarget.Process);

    log.Info("collection count: " + count);

    var filter = new BsonDocument();
    using (var cursor = thresholdCollection.Find(filter).ToCursor()) {
        while (cursor.MoveNext())
        {
            foreach (var doc in cursor.Current)
            {
            // do something with the documents
                Foo foo = new Foo();
	         log.Info(doc.ToString());
                // get current reading
                var deviceID = doc.GetValue("deviceid").ToString();
                var rawReading = foo.CallRestService(readingURL + deviceID, "GET");
                var currentReading = rawReading.SelectToken("moisture").ToObject<int>();
                log.Info("reading: " + currentReading);
                
                // compare
                if (doc.GetValue("alertlevel").ToInt32() > currentReading) {
                    log.Info("alert! " + deviceID);
                    // alert already active? if not insert into db
                    var alertFilter = Builders<BsonDocument>.Filter.Eq("deviceid", deviceID);
                    var document = existingAlertsCollection.Find(alertFilter).Count();

                    if (document > 0) {
                        // previous alert exists
                        //TODO: is it over 24 hours old?

                        log.Info("alert exists, skipping");
                    } else {
                        var now = DateTime.UtcNow.ToString();
                        var tempDoc = new BsonDocument
                        {
                            { "name", now },
                            { "deviceid", deviceID }
                        };

                        existingAlertsCollection.InsertOne(tempDoc);

                        // send text
                        log.Info("sending alert for " + deviceID);
                        var twilioClient = new TwilioRestClient(twilioAccountSid, twilioAuthToken);
 
                        twilioClient.SendMessage(
                            twilioFrom,
                            twilioTo,
                            deviceID + " needs a drink ASAP!"            
                        );
                    }
                } else {
                    // if existing alert exists, delete it
		    var alertFilter = Builders<BsonDocument>.Filter.Eq("deviceid", deviceID);
                    var document = existingAlertsCollection.Find(alertFilter).Count();

		    if (document > 0) {
                        var deleteResult = existingAlertsCollection.DeleteMany(alertFilter);
                        log.Info("delete count: " + deleteResult.DeletedCount);
		    }
                }
            }
        }
    }
}
