using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

using HtmlAgilityPack;

namespace edim2.ingest
{
          
    public static class invoiceskillsetparser
    {
         #region Class used to deserialize the request
        private class InputRecord
        {
            public class InputRecordData
            {
                public string Filename { get; set; }
            }

            public string RecordId { get; set; }
            public InputRecordData Data { get; set; }
        }

        private class WebApiRequest
        {
            public List<InputRecord> Values { get; set; }
        }
        #endregion

        #region Classes used to serialize the response

        private class OutputRecord
        {
            public class OutputRecordData
            {
                public string CompanyCode { get; set; } = "";
                public string CompanyName { get; set; } = "";
                public List<string> DocumentReferences { get; set; } = new List<string>();
            }

            public string RecordId { get; set; }
            public OutputRecordData Data { get; set; }
        }

        private class WebApiResponse
        {
            public List<OutputRecord> Values { get; set; }
        }
        #endregion
        
        [FunctionName("invoiceskillsetparser")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "")] HttpRequest req,
            Binder binder,
            ILogger log)
        {
            //Parse input            
            string body = new StreamReader(req.Body).ReadToEnd();
            var data = JsonConvert.DeserializeObject<WebApiRequest>(body);            

            // Do some schema validation
            if (data == null)
            {
                return new BadRequestObjectResult("The request schema does not match expected schema.");
            }
            if (data.Values == null)
            {
                return new BadRequestObjectResult("The request schema does not match expected schema. Could not find values array.");
            }  

            var response = new WebApiResponse
            {
                Values = new List<OutputRecord>()
            };

            var path = Environment.GetEnvironmentVariable("blob-container"); 
            
            foreach (var record in data.Values)
            {
                if (record == null || record.RecordId == null) continue;

                //Bind blob to filename using dynamic binding
                var blob = await binder.BindAsync<string>(new BlobAttribute(path + "/" + record.Data.Filename));
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(blob);               

                OutputRecord responseRecord = new OutputRecord
                {
                    RecordId = record.RecordId
                };
                responseRecord.Data = new OutputRecord.OutputRecordData();
                
                //Add values from first table
                var table = htmlDoc.DocumentNode.SelectSingleNode("//table");
                var elements = table.Elements("td").ToList();

                for(int i = 0; i<elements.Count; i++)
                {
                    var element = elements[i];
                    if (element.NodeType == HtmlNodeType.Element)
                    {                        
                        if( element.InnerText.Equals("Company") )
                        {
                            log.LogInformation($"{elements[i].InnerText}: {elements[i+1].InnerText}");
                            responseRecord.Data.CompanyCode = elements[++i].InnerText;
                        }
                        else if( element.InnerText.Equals("Company name") )
                        {
                            log.LogInformation($"{elements[i].InnerText}: {elements[i+1].InnerText}");
                            responseRecord.Data.CompanyName = elements[++i].InnerText;
                        }                        
                    }
                }                 

                //Find document references and add them to response
                var references = htmlDoc.DocumentNode.SelectNodes("//a");
                if( references != null ){
                    foreach (var node in references)
                    {  
                        log.LogInformation($"Adding doc ref: {node.InnerText}");
                        responseRecord.Data.DocumentReferences.Add(node.InnerText);
                    }
                }
                response.Values.Add(responseRecord);
            }

            return (ActionResult)new OkObjectResult(response);
        }
    }
}