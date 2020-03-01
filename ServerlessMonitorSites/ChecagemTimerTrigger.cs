using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using ServerlessMonitorSites.Entities;

namespace ServerlessMonitorSites
{
    public static class ChecagemTimerTrigger
    {
        [FunctionName("ChecagemTimerTrigger")]
        public static void Run([TimerTrigger("*/30 * * * * *")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation(
                $"DBCheckTimerTrigger - iniciando execução em: {DateTime.Now}");

            var storageAccount = CloudStorageAccount
                .Parse(Environment.GetEnvironmentVariable("BaseLog"));
            var logTable = storageAccount
                .CreateCloudTableClient().GetTableReference("LogTable");

            if (logTable.CreateIfNotExistsAsync().Result)
                log.LogInformation("Criando a tabela de log...");

            var hosts = Environment.GetEnvironmentVariable("Sites")
                .Split("|", StringSplitOptions.RemoveEmptyEntries);
            foreach (string host in hosts)
            {
                log.LogInformation(
                    $"Verificando a disponibilidade do host {host}");

                var dadosLog = new LogEntity("MonitoramentoSites",
                    DateTime.Now.ToString("yyyyMMddHHmmss"));
                dadosLog.Site = host;

                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri(host);
                    client.DefaultRequestHeaders.Accept.Clear();

                    try
                    {
                        // Envio da requisicao a fim de determinar se
                        // o site esta no ar
                        HttpResponseMessage response =
                            client.GetAsync("").Result;

                        dadosLog.Status = (int)response.StatusCode + " " +
                            response.StatusCode;
                        if (response.StatusCode != System.Net.HttpStatusCode.OK)
                            dadosLog.DescricaoErro = response.ReasonPhrase;
                    }
                    catch (Exception ex)
                    {
                        dadosLog.Status = "Exception";
                        dadosLog.DescricaoErro = ex.Message;
                    }
                }

                string jsonResultado =
                    JsonSerializer.Serialize(dadosLog);

                if (dadosLog.DescricaoErro == null)
                    log.LogInformation(jsonResultado);
                else
                {
                    log.LogError(jsonResultado);

                    using (var clientLogicAppSlack = new HttpClient())
                    {
                        clientLogicAppSlack.BaseAddress = new Uri(
                            Environment.GetEnvironmentVariable("UrlLogicAppAlerta"));
                        clientLogicAppSlack.DefaultRequestHeaders.Accept.Clear();
                        clientLogicAppSlack.DefaultRequestHeaders.Accept.Add(
                            new MediaTypeWithQualityHeaderValue("application/json"));

                        var requestMessage =
                              new HttpRequestMessage(HttpMethod.Post, String.Empty);

                        requestMessage.Content = new StringContent(
                            JsonSerializer.Serialize(new
                            {
                                recurso = dadosLog.Site,
                                descricaoErro = dadosLog.DescricaoErro,
                            }), Encoding.UTF8, "application/json");

                        var respLogicApp = clientLogicAppSlack
                            .SendAsync(requestMessage).Result;
                        respLogicApp.EnsureSuccessStatusCode();

                        log.LogError(
                            "Envio de alerta para Logic App de integração com o Slack");
                    }

                    var insertOperation = TableOperation.Insert(dadosLog);
                    var resultInsert = logTable.ExecuteAsync(insertOperation).Result;
                    string jsonResultInsert = JsonSerializer.Serialize(resultInsert);
                    log.LogInformation(jsonResultInsert);

                    Thread.Sleep(3000);
                }

                log.LogInformation(
                    $"ChecagemTimerTrigger - concluindo execução em: {DateTime.Now}");
            }
        }
    }
}
