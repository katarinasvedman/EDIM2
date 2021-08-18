using System;
using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace edim2.ingest
{
    public static class invoiceparser
    {
        [FunctionName("invoiceparser")]
        public static void Run([BlobTrigger("samples-workitems/{name}", Connection = "edim2poc_STORAGE")]Stream myBlob, string name, ILogger log)
        {
            log.LogInformation($"C# Blob trigger function Processed blob\n Name:{name} \n Size: {myBlob.Length} Bytes");
        }
    }
}
