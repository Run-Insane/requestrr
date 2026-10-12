using System.ComponentModel.DataAnnotations;

namespace Requestrr.WebApi.Controllers.DownloadClients.Chaptarr
{
    public class ChaptarrSettingsModel : TestChaptarrSettingsModel
    {
        public string RootFolder { get; set; }
        public bool SearchNewRequests { get; set; }
    }
}
