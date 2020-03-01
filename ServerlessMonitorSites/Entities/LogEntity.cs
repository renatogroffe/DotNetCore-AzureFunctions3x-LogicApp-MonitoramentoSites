using Microsoft.WindowsAzure.Storage.Table;

namespace ServerlessMonitorSites.Entities
{
    public class LogEntity : TableEntity
    {
        public LogEntity(string site, string horario)
        {
            this.PartitionKey = site;
            this.RowKey = horario;
        }

        public LogEntity() { }

        public string Site { get; set; }
        public string Status { get; set; }
        public string DescricaoErro { get; set; }
    }
}