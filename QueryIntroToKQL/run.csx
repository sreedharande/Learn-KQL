#r "Newtonsoft.Json"

using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

private static ILogger defaultLog = null;
string storageAccountName = Environment.GetEnvironmentVariable("StorageAccountName");
string storageAccountContainerName = Environment.GetEnvironmentVariable("StorageContainerName");

public static async Task<IActionResult> Run(HttpRequest req, ILogger log)
{
    defaultLog = log;
    var requestType = req.Query["type"].ToString();
    log.LogInformation($"RequestType: {requestType}");

    if(requestType.ToLower() == "query")
    {
        var token = await GetToken();
        
        string query = await new StreamReader(req.Body).ReadToEndAsync();
        var result = await QueryLogAnalytics(query, token);

        dynamic res = JsonConvert.DeserializeObject<System.Dynamic.ExpandoObject>(result, new ExpandoObjectConverter());

        return new JsonResult(res);
    }
    else if(requestType.ToLower() == "exquery")
    {
        var token = await GetToken();

        string requestQuery = req.Query["ex"].ToString();
        string tab = req.Query["tab"].ToString();
        string section = req.Query["section"].ToString();
        var exerciseList = await GetExercises(tab, section);
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        defaultLog.LogInformation(requestBody);
        string query = "";
        query = exerciseList.Where(a => a[0] == requestQuery).First()[2].ToString();
        var result = await QueryLogAnalytics(query,token);
        dynamic res = JsonConvert.DeserializeObject<System.Dynamic.ExpandoObject>(result, new ExpandoObjectConverter());

        return new JsonResult(res);
    }

    else if(requestType.ToLower() == "check")
    {     
        var token = await GetToken();

        string requestQuery = req.Query["Ex"].ToString();
        string tab = req.Query["tab"].ToString();
        string section = req.Query["section"].ToString();
        var exerciseList = await GetExercises(tab, section);
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        defaultLog.LogInformation(requestBody);
        string query = "";
        query = exerciseList.Where(a => a[0] == requestQuery).First()[2].ToString();
        var result = await QueryLogAnalytics(requestBody,token);
        var result2 = await QueryLogAnalytics(query,token);

        var error = result.ToString().StartsWith("{\"error\":");
        var eq = result == result2;
        
        var message = "";
        if(error) 
        {
            log.LogInformation(result);
            message = "error";
        }
        else
        {
            log.LogInformation(result);
            log.LogInformation(result2);
            log.LogInformation(eq.ToString());
            message = eq ? "success" : "failed";
        }
        return new OkObjectResult($"{message}");
    }  

    return null;        
}

private static async Task<string> RefreshToken()
    {
        var token = "";
        using (var client = new HttpClient())
        {
            Dictionary<string, string> dictionary = new Dictionary<string, string>();
            dictionary.Add("grant_type", "client_credentials");
            dictionary.Add("client_id", Environment.GetEnvironmentVariable("ClientId"));
            dictionary.Add("redirect_uri", "https://localhost/SentinelTraining");
            dictionary.Add("resource", "https://api.loganalytics.io");
            dictionary.Add("client_secret", Environment.GetEnvironmentVariable("ClientSecret"));

            var logonBody = new FormUrlEncodedContent(dictionary);
			var tenantId = Environment.GetEnvironmentVariable("TenantId")
            var logonUri = "https://login.microsoftonline.com/{tenantId}/oauth2/token";
            client.DefaultRequestHeaders.Add("ContentType","application/x-www-form-urlencoded");
            var authResponse = await client.PostAsync(logonUri,logonBody);
            var authResult = await authResponse.Content.ReadAsStringAsync();
            
            token = authResult.Substring(authResult.IndexOf("\"access_token\":\"")+16).Replace("\"}","");
        }
        return token;
    }

    private static async Task<string> GetToken()
    {
        var tokenDate = new DateTime();
        var token = Environment.GetEnvironmentVariable("token");
        DateTime.TryParse(Environment.GetEnvironmentVariable("tokenDate"),out tokenDate);
        if(tokenDate < DateTime.Now - new TimeSpan(0,5,0) && token != "")
        { 
            defaultLog.LogInformation("--Refreshing cache--");
            token = await RefreshToken();
            Environment.SetEnvironmentVariable("token",token);
            Environment.SetEnvironmentVariable("tokenDate",DateTime.Now.ToLongTimeString());
        }
        else
        {
            defaultLog.LogInformation("--Cache hit--");
        }
        return token;
    }

    private static async Task<List<string[]>> GetExercises(string Tab, string Section)
    {
        var blobSource = await GetBlob(Tab,Section);
        
        List<string[]> exerciseList = new List<string[]>();
        foreach(var ex in blobSource.Split(Environment.NewLine))
        {
            exerciseList.Add(ex.Split(",,,"));
        }

        return exerciseList;
    }

    private static async Task<string> QueryLogAnalytics(string query, string token)
    {
        using (var client = new HttpClient())
        {
            query = query.Replace("\"","\\\"").Replace(Environment.NewLine," ");
            string queryBody = string.Format("{{\"query\": \"{0}\"}}",query);
            defaultLog.LogInformation(queryBody);
            
            var requestData = new StringContent(queryBody, Encoding.UTF8, "application/json");
            client.DefaultRequestHeaders.Add("Authorization",string.Format("Bearer {0}",token));
			defaultLog.LogInformation("1");
			var workspaceId = Environment.GetEnvironmentVariable("WorkspaceId")
            var response = await client.PostAsync("https://api.loganalytics.io/v1/workspaces/{workspaceId}/query", requestData);
            var result = await response.Content.ReadAsStringAsync();

            return result;
        }
    }

private static async Task<string> GetBlob()
{
    string exercises = "";
    using(System.Net.WebClient c = new System.Net.WebClient())
    {
        defaultLog.LogInformation("2");
        exercises = c.DownloadString($"https://{storageAccountName}.blob.core.windows.net/{storageAccountContainerName}/Exercises.csv");
    }
    return exercises;
}

private static async Task<string> GetBlob(string Tab, string Section)
{
    string exercises = "";
    using(System.Net.WebClient c = new System.Net.WebClient())
    {
        defaultLog.LogInformation("3");
        exercises = c.DownloadString($"https://{storageAccountName}.blob.core.windows.net/{storageAccountContainerName}/{Tab}/{Section}.csv");
    }
    return exercises;
}
