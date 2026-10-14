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


        public async Task<IReadOnlyList<Book>> SearchBooksAsync(BookRequest request, string searchTerm, string provider = null)
        {
            try
            {
                provider = string.IsNullOrWhiteSpace(provider) ? Settings.SearchProvider : provider.Trim().ToLower();

                string url = $"{BaseURL}/search?term={Uri.EscapeDataString(searchTerm.Trim())}&provider={Uri.EscapeDataString(provider)}";
                HttpResponseMessage response = await HttpGetAsync(url);
                await response.ThrowIfNotSuccessfulAsync("ChaptarrSearch failed", x => x.error);

                // Results mix authors and books; only book entries can be requested.
                List<JSONSearchResult> results = JsonConvert.DeserializeObject<List<JSONSearchResult>>(await response.Content.ReadAsStringAsync()) ?? new List<JSONSearchResult>();

                // Search rows carry no reliable media type, so formats are resolved by id once a book is picked.
                return results
                    .Select(x => x?.Book)
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.ForeignBookId))
                    .GroupBy(x => x.ForeignBookId)
                    .Select(g => ConvertToBook(g.First()))
                    .ToList();
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
                string id = foreignBookId.Trim();
                Book book = null;

                foreach (BookFormat format in new[] { BookFormat.Audiobook, BookFormat.Ebook })
                {
                    List<JSONBook> rows = (await LookupAsync(id, format == BookFormat.Audiobook ? "audiobook" : "ebook"))
                        .Where(x => x != null && !string.IsNullOrWhiteSpace(x.ForeignBookId))
                        .ToList();

                    // Chaptarr matches local edition ids before work ids, so a row whose edition (not work) carries
                    // the requested id is a different book.
                    rows = rows
                        .Where(x => x.ForeignBookId == id || x.Editions == null || !x.Editions.Any(e => e?.ForeignEditionId == id))
                        .ToList();

                    JSONBook row = rows.FirstOrDefault(x => x.ForeignBookId == id) ?? rows.FirstOrDefault();

                    if (row == null)
                        continue;

                    if (book == null)
                    {
                        book = ConvertToBook(row);
                        book.ForeignBookId = id;
                    }
                    else
                    {
                        book.AudiobookInLibrary |= row.LocalAudiobookBooks != null && row.LocalAudiobookBooks.Any();
                        book.EbookInLibrary |= row.LocalEbookBooks != null && row.LocalEbookBooks.Any();
                    }

                    book.AvailableFormats.Add(format);
                    book.FormatIds[format] = new BookImportIds
                    {
                        ForeignBookId = row.ForeignBookId,
                        ForeignAuthorId = row.Author?.ForeignAuthorId,
                        AuthorName = row.Author?.AuthorName
                    };
                }

                return book;
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
                if (!book.FormatIds.TryGetValue(format, out BookImportIds ids))
                {
                    return new BookRequestResult { WasSuccess = false, Error = "Chaptarr has no edition of this book in the requested format." };
                }

                string mediaType = format == BookFormat.Audiobook ? "audiobook" : "ebook";

                var author = new Dictionary<string, object>
                {
                    ["foreignAuthorId"] = ids.ForeignAuthorId,
                    ["authorName"] = ids.AuthorName,
                    // Monitor only the requested book; the rest of the author's catalog stays unmonitored.
                    ["addOptions"] = new Dictionary<string, object>
                    {
                        ["monitor"] = "specificBook",
                        ["searchForMissingBooks"] = false
                    }
                };

                if (!string.IsNullOrWhiteSpace(Settings.RootFolder))
                {
                    author[format == BookFormat.Audiobook ? "audiobookRootFolderPath" : "ebookRootFolderPath"] = Settings.RootFolder;
                }

                // POST /book carries addOptions.searchForNewBook into Chaptarr's add logic, so the search also runs
                // when the request is queued as pending (/book/import drops it in that case).
                var addBody = new Dictionary<string, object>
                {
                    ["foreignBookId"] = ids.ForeignBookId,
                    ["title"] = book.Title,
                    ["mediaType"] = mediaType,
                    ["monitored"] = true,
                    ["addOptions"] = new Dictionary<string, object>
                    {
                        ["searchForNewBook"] = Settings.SearchNewRequests
                    },
                    ["author"] = author
                };

                HttpResponseMessage response = await HttpPostAsync($"{BaseURL}/book?mediaType={mediaType}", JsonConvert.SerializeObject(addBody));

                // Chaptarr saved the request; it adds (and searches) the book once the metadata server has prepared it.
                if (response.StatusCode == HttpStatusCode.Accepted)
                {
                    return new BookRequestResult { WasSuccess = true, WasPending = true };
                }

                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    return new BookRequestResult { WasSuccess = false, Error = "That book matches several providers on Chaptarr; ask the admin to disambiguate it." };
                }

                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    string error = ParseErrorMessage(await response.Content.ReadAsStringAsync());
                    _logger.LogWarning($"Chaptarr rejected adding \"{book.Title}\": {error}");
                    return new BookRequestResult { WasSuccess = false, Error = error };
                }

                await response.ThrowIfNotSuccessfulAsync("ChaptarrBookAdd failed", x => x.error);

                return new BookRequestResult { WasSuccess = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error while requesting book \"{book.Title}\" from Chaptarr: {ex.Message}");
                return new BookRequestResult { WasSuccess = false, Error = ex.Message };
            }
        }


        private async Task<List<JSONBook>> LookupAsync(string term, string mediaType)
        {
            string url = $"{BaseURL}/book/lookup?term={Uri.EscapeDataString(term)}";

            if (mediaType != null)
                url += $"&mediaType={mediaType}";

            HttpResponseMessage response = await HttpGetAsync(url);
            await response.ThrowIfNotSuccessfulAsync("ChaptarrBookLookup failed", x => x.error);

            string jsonResponse = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<JSONBook>>(jsonResponse) ?? new List<JSONBook>();
        }


        /// <summary>
        /// Chaptarr returns validation errors as [{propertyName, errorMessage}], other errors as {error|message} or a plain string.
        /// </summary>
        private static string ParseErrorMessage(string body)
        {
            try
            {
                JToken token = JToken.Parse(body);

                if (token is JArray array)
                {
                    List<string> messages = array
                        .Select(x => x.Type == JTokenType.Object ? (string)x["errorMessage"] : null)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .ToList();

                    if (messages.Any())
                        return string.Join(" ", messages);
                }
                else if (token.Type == JTokenType.String)
                {
                    string message = (string)token;

                    if (!string.IsNullOrWhiteSpace(message))
                        return message;
                }
                else if (token is JObject obj)
                {
                    string message = (string)obj["message"] ?? (string)obj["error"];

                    if (!string.IsNullOrWhiteSpace(message))
                        return message;
                }
            }
            catch (JsonReaderException)
            {
            }

            return body;
        }


        private static Book ConvertToBook(JSONBook jsonBook)
        {
            return new Book
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
            public JSONAuthor Author { get; set; }
            public List<JSONEdition> Editions { get; set; }
            public List<object> LocalAudiobookBooks { get; set; }
            public List<object> LocalEbookBooks { get; set; }
        }

        private class JSONEdition
        {
            public string ForeignEditionId { get; set; }
        }

        private class JSONSearchResult
        {
            public JSONBook Book { get; set; }
        }

        private class JSONAuthor
        {
            public string ForeignAuthorId { get; set; }
            public string AuthorName { get; set; }
        }
    }
}
