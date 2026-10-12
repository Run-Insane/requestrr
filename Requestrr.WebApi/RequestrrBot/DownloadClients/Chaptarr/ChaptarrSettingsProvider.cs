namespace Requestrr.WebApi.RequestrrBot.DownloadClients.Chaptarr
{
    public class ChaptarrSettingsProvider
    {
        public ChaptarrSettings Provide()
        {
            dynamic settings = SettingsFile.Read();

            return new ChaptarrSettings
            {
                Hostname = (string)settings.DownloadClients.Chaptarr.Hostname,
                Port = (uint)settings.DownloadClients.Chaptarr.Port,
                ApiKey = (string)settings.DownloadClients.Chaptarr.ApiKey,
                UseSSL = (bool)settings.DownloadClients.Chaptarr.UseSSL,
                BaseUrl = (string)settings.DownloadClients.Chaptarr.BaseUrl,
                RootFolder = (string)settings.DownloadClients.Chaptarr.RootFolder,
                SearchNewRequests = (bool)settings.DownloadClients.Chaptarr.SearchNewRequests
            };
        }
    }
}
