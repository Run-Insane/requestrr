using Requestrr.WebApi.RequestrrBot.DownloadClients.Lidarr;
using Requestrr.WebApi.RequestrrBot.DownloadClients.Ombi;
using Requestrr.WebApi.RequestrrBot.DownloadClients.Overseerr;
using Requestrr.WebApi.RequestrrBot.DownloadClients.Radarr;
using Requestrr.WebApi.RequestrrBot.DownloadClients.Sonarr;
using Requestrr.WebApi.RequestrrBot.DownloadClients.Chaptarr;

namespace Requestrr.WebApi.config
{
    public class DownloadClientsSettings
    {
        public OmbiSettings Ombi { get; set; }
        public OverseerrSettings Overseerr { get; set; }
        public RadarrSettings Radarr { get; set; }
        public SonarrSettings Sonarr { get; set; }

        public LidarrSettings Lidarr { get; set; }

        public ChaptarrSettings Chaptarr { get; set; }
    }
}