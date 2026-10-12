using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Requestrr.WebApi.Extensions;
using Requestrr.WebApi.RequestrrBot.Books;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Requestrr.WebApi.RequestrrBot.DownloadClients.Chaptarr
{
    public class ChaptarrClientV1 : IBookSearcher, IBookRequester
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ChaptarrClient> _logger;
        private readonly ChaptarrSettingsProvider _settingsProvider;
        private ChaptarrSettings Settings => _settingsProvider.Provide();

        private string BaseURL => GetBaseURL(Settings);

        private const string PendingImportMessage = "The author and requested book are being prepared by the metadata server. Chaptarr saved your request and will add it automatically when it becomes available.";

        public ChaptarrClientV1(IHttpClientFactory httpClientFactory, ILogger<ChaptarrClient> logger, ChaptarrSettingsProvider settingsProvider)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _settingsProvider = settingsProvider;
        }


        /// <summary>
        /// Used to test if Chaptarr service can be found (GET /api/v1/health).
        /// </summary>
        public static async Task TestConnectionAsync(HttpClient httpClient, ILogger<ChaptarrClient> logger, ChaptarrSettings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.BaseUrl) && !settings.BaseUrl.StartsWith("/"))
            {
                throw new Exception("Invalid base URL, must start with /");
            }

            try
            {
                HttpResponseMessage response = await HttpGetAsync(httpClient, settings, $"{GetBaseURL(settings)}/health");

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new Exception("Invalid api key");
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new Exception("Incorrect api version");
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception("Invalid host and/or port");
                }
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "Error while testing Chaptarr connection: " + ex.Message);
                throw new Exception("Invalid host and/or port");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error while testing Chaptarr connection: " + ex.Message);

                if (ex.GetType() == typeof(Exception))
                {
                    throw;
                }

                throw new Exception("Invalid host and/or port");
            }
        }


        public async Task<IReadOnlyList<Book>> SearchBooksAsync(BookRequest request, string searchTerm)
        {
            try
            {
                string term = Uri.EscapeDataString(searchTerm.Trim());
                HttpResponseMessage response = await HttpGetAsync($"{BaseURL}/book/lookup?term={term}");
                await response.ThrowIfNotSuccessfulAsync("ChaptarrBookLookup failed", x => x.error);

                string jsonResponse = await response.Content.ReadAsStringAsync();
                List<JSONBook> jsonBooks = JsonConvert.DeserializeObject<List<JSONBook>>(jsonResponse);

                return MergeBooks(jsonBooks.Where(x => x != null).Select(x => ConvertToBook(x)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while searching for books with Chaptarr: " + ex.Message);
            }

            throw new Exception("An error occurred while searching for books with Chaptarr");
        }


        public async Task<Book> SearchBookByIdAsync(BookRequest request, string foreignBookId)
        {
            try
            {
                string term = Uri.EscapeDataString(foreignBookId.Trim());
                HttpResponseMessage response = await HttpGetAsync($"{BaseURL}/book/lookup?term={term}");
                await response.ThrowIfNotSuccessfulAsync("ChaptarrBookLookup failed", x => x.error);

                string jsonResponse = await response.Content.ReadAsStringAsync();
                List<JSONBook> jsonBooks = JsonConvert.DeserializeObject<List<JSONBook>>(jsonResponse);

                IReadOnlyList<Book> books = MergeBooks(jsonBooks.Where(x => x != null).Select(x => ConvertToBook(x)));

                if (books.Any())
                    return books.First();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while searching for book by Id \"{foreignBookId}\" with Chaptarr: {ex.Message}");
            }

            throw new Exception("An error occurred while searching for a book by Id with Chaptarr");
        }


        public async Task<BookRequestResult> RequestBookAsync(BookRequest request, Book book, BookFormat format)
        {
            try
            {
                var importBody = new Dictionary<string, object>
                {
                    ["foreignBookId"] = book.ForeignBookId,
                    ["foreignAuthorId"] = book.ForeignAuthorId,
                    ["mediaType"] = format == BookFormat.Audiobook ? "audiobook" : "ebook",
                    ["authorMonitoring"] = new Dictionary<string, object>
                    {
                        ["monitor"] = format == BookFormat.Audiobook ? "audiobooks" : "ebooks",
                        ["searchForMissing"] = Settings.SearchNewRequests
                    }
                };

                if (!string.IsNullOrWhiteSpace(Settings.RootFolder))
                {
                    importBody["rootFolder"] = Settings.RootFolder;
                }

                HttpResponseMessage response = await HttpPostAsync($"{BaseURL}/book/import", JsonConvert.SerializeObject(importBody));

                if (response.StatusCode == HttpStatusCode.Accepted)
                {
                    return new BookRequestResult { WasSuccess = true, WasPending = true };
                }

                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    string conflictBody = await response.Content.ReadAsStringAsync();
                    return new BookRequestResult { WasSuccess = false, Error = "That book matches several providers on Chaptarr; ask the admin to disambiguate it." };
                }

                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    string errorBody = await response.Content.ReadAsStringAsync();

                    if (errorBody.Contains("being prepared by the metadata server"))
                    {
                        return new BookRequestResult { WasSuccess = true, WasPending = true };
                    }

                    string error = null;

                    try
                    {
                        dynamic jsonError = JObject.Parse(errorBody);
                        error = (string)jsonError.error;

                        if (error == null && jsonError.modelState != null)
                        {
                            error = errorBody;
                        }
                    }
                    catch
                    {
                        error = errorBody;
                    }

                    _logger.LogWarning($"Chaptarr rejected the book import for \"{book.Title}\": {error}");
                    return new BookRequestResult { WasSuccess = false, Error = error };
                }

                await response.ThrowIfNotSuccessfulAsync("ChaptarrBookImport failed", x => x.error);

                return new BookRequestResult { WasSuccess = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error while requesting book \"{book.Title}\" from Chaptarr: {ex.Message}");
                return new BookRequestResult { WasSuccess = false, Error = ex.Message };
            }
        }


        private static IReadOnlyList<Book> MergeBooks(IEnumerable<Book> books)
        {
            // Chaptarr returns one row per media type per work; merge them into one entry per work.
            return books
                .GroupBy(x => x.ForeignBookId)
                .Select(g =>
                {
                    Book merged = g.First();

                    foreach (Book other in g.Skip(1))
                    {
                        foreach (BookFormat fmt in other.AvailableFormats)
                        {
                            if (!merged.AvailableFormats.Contains(fmt))
                                merged.AvailableFormats.Add(fmt);
                        }

                        merged.AudiobookInLibrary |= other.AudiobookInLibrary;
                        merged.EbookInLibrary |= other.EbookInLibrary;

                        if (string.IsNullOrEmpty(merged.MediaType) && !string.IsNullOrEmpty(other.MediaType))
                            merged.MediaType = other.MediaType;
                    }

                    return merged;
                })
                .ToList();
        }


        private Book ConvertToBook(JSONBook jsonBook)
        {
            var book = new Book
            {
                ForeignBookId = jsonBook.ForeignBookId,
                ForeignAuthorId = jsonBook.Author?.ForeignAuthorId,
                Title = jsonBook.Title,
                AuthorTitle = jsonBook.AuthorTitle,
                Overview = jsonBook.Overview,
                RemoteCover = jsonBook.RemoteCover,
                MediaType = jsonBook.MediaType,
                ReleaseDate = jsonBook.ReleaseDate,
                AudiobookInLibrary = jsonBook.LocalAudiobookBooks != null && jsonBook.LocalAudiobookBooks.Any(),
                EbookInLibrary = jsonBook.LocalEbookBooks != null && jsonBook.LocalEbookBooks.Any()
            };

            if (jsonBook.Formats != null && jsonBook.Formats.Any())
            {
                foreach (string format in jsonBook.Formats)
                {
                    if (string.Equals(format, "audiobook", StringComparison.OrdinalIgnoreCase) && !book.AvailableFormats.Contains(BookFormat.Audiobook))
                        book.AvailableFormats.Add(BookFormat.Audiobook);
                    else if (string.Equals(format, "ebook", StringComparison.OrdinalIgnoreCase) && !book.AvailableFormats.Contains(BookFormat.Ebook))
                        book.AvailableFormats.Add(BookFormat.Ebook);
                }
            }

            if (book.AvailableFormats.Count == 0)
            {
                // Chaptarr did not tell us which formats this work offers; assume both are requestable.
                book.AvailableFormats.Add(BookFormat.Audiobook);
                book.AvailableFormats.Add(BookFormat.Ebook);
            }

            return book;
        }


        private Task<HttpResponseMessage> HttpGetAsync(string url)
        {
            return HttpGetAsync(_httpClientFactory.CreateClient(), Settings, url);
        }


        private static async Task<HttpResponseMessage> HttpGetAsync(HttpClient client, ChaptarrSettings settings, string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("X-Api-Key", settings.ApiKey);

            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
            {
                return await client.SendAsync(request, cts.Token);
            }
        }


        private async Task<HttpResponseMessage> HttpPostAsync(string url, string content)
        {
            StringContent postRequest = new StringContent(content);
            postRequest.Headers.Clear();
            postRequest.Headers.Add("Content-Type", "application/json");
            postRequest.Headers.Add("X-Api-Key", Settings.ApiKey);

            HttpClient client = _httpClientFactory.CreateClient();
            return await client.PostAsync(url, postRequest);
        }


        private static string GetBaseURL(ChaptarrSettings settings)
        {
            var protocol = settings.UseSSL ? "https" : "http";
            var baseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl) ? string.Empty : settings.BaseUrl.TrimEnd('/');

            return $"{protocol}://{settings.Hostname}:{settings.Port}{baseUrl}/api/v1";
        }


        private class JSONBook
        {
            public string Title { get; set; }
            public string AuthorTitle { get; set; }
            public string Overview { get; set; }
            public string ForeignBookId { get; set; }
            public string MediaType { get; set; }
            public DateTime? ReleaseDate { get; set; }
            public string RemoteCover { get; set; }
            public List<string> Formats { get; set; }
            public JSONAuthor Author { get; set; }
            public List<object> LocalAudiobookBooks { get; set; }
            public List<object> LocalEbookBooks { get; set; }
        }

        private class JSONAuthor
        {
            public string ForeignAuthorId { get; set; }
        }
    }
}
