using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Requestrr.WebApi.RequestrrBot.Books;
using Requestrr.WebApi.RequestrrBot.DownloadClients;
using Requestrr.WebApi.RequestrrBot.DownloadClients.Chaptarr;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Requestrr.WebApi.Controllers.DownloadClients.Chaptarr
{
    [ApiController]
    [Authorize]
    [Route("/api/books/chaptarr")]
    public class ChaptarrClientController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ChaptarrClient> _logger;

        public ChaptarrClientController(IHttpClientFactory httpClientFactory, ILogger<ChaptarrClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }


        /// <summary>
        /// Handles the testing of Chaptarr settings, pass the settings into a Chaptarr client
        /// </summary>
        [HttpPost("test")]
        public async Task<IActionResult> TestChaptarrSettings([FromBody] TestChaptarrSettingsModel model)
        {
            try
            {
                await ChaptarrClient.TestConnectionAsync(_httpClientFactory.CreateClient(), _logger, ConvertToChaptarrSettings(model));
                return Ok(new { ok = true });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }


        /// <summary>
        /// Handles the saving of Chaptarr settings
        /// </summary>
        [HttpPost()]
        public async Task<IActionResult> SaveAsync([FromBody] ChaptarrSettingsModel model)
        {
            string searchProvider = (model.SearchProvider ?? ChaptarrSearchProviders.Goodreads).Trim().ToLower();

            if (!ChaptarrSearchProviders.All.Contains(searchProvider))
            {
                return BadRequest("Invalid search provider.");
            }

            BooksSettings booksSettings = new BooksSettings
            {
                Client = DownloadClient.Chaptarr
            };

            ChaptarrSettingsModel chaptarrSettings = new ChaptarrSettingsModel
            {
                Hostname = model.Hostname.Trim(),
                ApiKey = model.ApiKey.Trim(),
                BaseUrl = string.IsNullOrWhiteSpace(model.BaseUrl) ? string.Empty : model.BaseUrl.Trim(),
                Port = model.Port,
                UseSSL = model.UseSSL,
                RootFolder = string.IsNullOrWhiteSpace(model.RootFolder) ? string.Empty : model.RootFolder.Trim(),
                SearchNewRequests = model.SearchNewRequests,
                SearchProvider = searchProvider
            };

            DownloadClientsSettingsRepository.SetChaptarr(booksSettings, chaptarrSettings);
            return Ok(new { ok = true });
        }


        private static ChaptarrSettings ConvertToChaptarrSettings(TestChaptarrSettingsModel model)
        {
            return new ChaptarrSettings
            {
                ApiKey = model.ApiKey.Trim(),
                Hostname = model.Hostname.Trim(),
                BaseUrl = string.IsNullOrWhiteSpace(model.BaseUrl) ? string.Empty : model.BaseUrl.Trim(),
                Port = (uint)model.Port,
                UseSSL = model.UseSSL
            };
        }
    }
}
