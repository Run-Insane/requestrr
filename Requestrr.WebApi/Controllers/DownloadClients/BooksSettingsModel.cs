using Requestrr.WebApi.Controllers.DownloadClients.Chaptarr;
using System.ComponentModel.DataAnnotations;

namespace Requestrr.WebApi.Controllers.DownloadClients
{
    public class BooksSettingsModel
    {
        [Required]
        public string Client { get; set; }

        [Required]
        public ChaptarrSettingsModel Chaptarr { get; set; }
    }
}
