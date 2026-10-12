using Microsoft.Extensions.Logging;
using Requestrr.WebApi.RequestrrBot.Books;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Requestrr.WebApi.RequestrrBot.DownloadClients.Chaptarr
{
    public class ChaptarrClient : IBookSearcher, IBookRequester
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ChaptarrClient> _logger;
        private readonly ChaptarrSettingsProvider _settingsProvider;


        public ChaptarrClient(IHttpClientFactory httpClientFactory, ILogger<ChaptarrClient> logger, ChaptarrSettingsProvider settingsProvider)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _settingsProvider = settingsProvider;
        }


        /// <summary>
        /// Used to test a connection to Chaptarr
        /// </summary>
        public static Task TestConnectionAsync(HttpClient httpClient, ILogger<ChaptarrClient> logger, ChaptarrSettings settings)
        {
            return ChaptarrClientV1.TestConnectionAsync(httpClient, logger, settings);
        }


        public Task<IReadOnlyList<Book>> SearchBooksAsync(BookRequest request, string searchTerm)
        {
            return CreateInstance<IBookSearcher>().SearchBooksAsync(request, searchTerm);
        }

        public Task<Book> SearchBookByIdAsync(BookRequest request, string foreignBookId)
        {
            return CreateInstance<IBookSearcher>().SearchBookByIdAsync(request, foreignBookId);
        }

        public Task<BookRequestResult> RequestBookAsync(BookRequest request, Book book, BookFormat format)
        {
            return CreateInstance<IBookRequester>().RequestBookAsync(request, book, format);
        }


        private T CreateInstance<T>() where T : class
        {
            return new ChaptarrClientV1(_httpClientFactory, _logger, _settingsProvider) as T;
        }
    }
}
