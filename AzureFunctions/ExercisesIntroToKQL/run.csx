#r "Newtonsoft.Json"

using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Linq;

string storageAccountName = Environment.GetEnvironmentVariable("StorageAccountName");
string storageAccountContainerName = Environment.GetEnvironmentVariable("StorageContainerName");

public static async Task<IActionResult> Run(HttpRequest req, ILogger log)
{
    var exerciseContent = "";
    List<dynamic> exerciseList = new List<dynamic>();
    var i = 0;
    var tab = req.Query["tab"].ToString();
    var section = req.Query["section"].ToString();

    if(!string.IsNullOrEmpty(tab) && !string.IsNullOrEmpty(section))
    {
        exerciseContent = await GetBlob(req.Query["tab"],req.Query["section"]);

        foreach(var ex in exerciseContent.Split(Environment.NewLine))
        {
            if(i>0)
            {
                var e = ex.Split(",,,");
                exerciseList.Add(new { Name=e[0], Question=e[1] });
            }
            i++;
        }
    }
    else
    {      
        exerciseContent = await GetBlob();
        foreach(var ex in exerciseContent.Split(Environment.NewLine))
        {
            if(i>0)
            {
                var e = ex.Split(",,,");
                exerciseList.Add(new { Tab=e[0],Section=e[1],Markdown="<br/><br/><br/>"+e[2] });
            }
            i++;
        }
    }
    

    return new JsonResult(exerciseList);
}

private static async Task<string> GetBlob()
{
    string exercises = "";

    using(System.Net.WebClient c = new System.Net.WebClient())
    {
        exercises = c.DownloadString($"https://{storageAccountName}.blob.core.windows.net/{storageAccountContainerName}/Exercises.csv");
    }

    return exercises;
}

private static async Task<string> GetBlob(string Tab, string Section)
{
    string exercises = "";

    using(System.Net.WebClient c = new System.Net.WebClient())
    {
        exercises = c.DownloadString($"https://{storageAccountName}.blob.core.windows.net/{storageAccountContainerName}/{Tab}/{Section}.csv");
    }

    return exercises;
}
