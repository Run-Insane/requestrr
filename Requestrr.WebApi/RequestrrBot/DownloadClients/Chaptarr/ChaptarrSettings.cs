namespace Requestrr.WebApi.RequestrrBot.DownloadClients.Chaptarr
{
    public class ChaptarrSettings
    {
        public string Hostname { get; set; }
        public uint Port { get; set; }
        public string ApiKey { get; set; }
        public bool UseSSL { get; set; }

        /// <summary>Chaptarr URL base (sub-path), always starts with / (empty string means "/").</summary>
        public string BaseUrl { get; set; }

        /// <summary>Default root folder for imports; empty lets Chaptarr use its configured default.</summary>
        public string RootFolder { get; set; }

        /// <summary>When true, imports trigger an immediate search for missing files.</summary>
        public bool SearchNewRequests { get; set; }
    }
}
