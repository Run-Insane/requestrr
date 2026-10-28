using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Requestrr.WebApi.config;
using Requestrr.WebApi.Controllers.DownloadClients.Chaptarr;
using Requestrr.WebApi.RequestrrBot;
using Requestrr.WebApi.RequestrrBot.Books;
using Requestrr.WebApi.RequestrrBot.DownloadClients;
using Requestrr.WebApi.RequestrrBot.DownloadClients.Chaptarr;
using System.Threading.Tasks;

namespace Requestrr.WebApi.Controllers.DownloadClients
{
    [ApiController]
    [Authorize]
    [Route("/api/books")]
    public class BooksDownloadClientController : ControllerBase
    {
        private readonly BooksSettings _booksSettings;
        private readonly DownloadClientsSettings _downloadClientsSettings;

        public BooksDownloadClientController(
            BooksSettingsProvider booksSettingsProvider,
            DownloadClientsSettingsProvider downloadClientsSettingsProvider)
        {
            _booksSettings = booksSettingsProvider.Provide();
            _downloadClientsSettings = downloadClientsSettingsProvider.Provide();
        }


        [HttpGet()]
        public async Task<IActionResult> GetAsync()
        {
            ChaptarrSettings chaptarr = _downloadClientsSettings.Chaptarr;

            return Ok(new BooksSettingsModel
            {
                Client = _booksSettings.Client,
                Chaptarr = new ChaptarrSettingsModel
                {
                    Hostname = chaptarr?.Hostname ?? string.Empty,
                    BaseUrl = chaptarr?.BaseUrl ?? string.Empty,
                    Port = (int)(chaptarr?.Port ?? 8789),
                    ApiKey = chaptarr?.ApiKey ?? string.Empty,
                    UseSSL = chaptarr?.UseSSL ?? false,
                    RootFolder = chaptarr?.RootFolder ?? string.Empty,
                    SearchNewRequests = chaptarr?.SearchNewRequests ?? true,
                    SearchProvider = chaptarr?.SearchProvider ?? ChaptarrSearchProviders.Goodreads
                }
            });
        }


        [HttpPost("disable")]
        public async Task<IActionResult> SaveAsync()
        {
            _booksSettings.Client = DownloadClient.Disabled;

            SettingsFile.Write(settings =>
            {
                settings.Books.Client = _booksSettings.Client;
            });

            return Ok(new { ok = true });
        }
    }
}
