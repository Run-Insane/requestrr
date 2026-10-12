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


        public async Task<IReadOnlyList<Book>> SearchBooksAsync(BookRequest request, string searchTerm)
        {
            try
            {
                List<JSONBook> jsonBooks = await LookupAsync(searchTerm.Trim(), null);

                // Free-text rows carry no reliable media type, so formats are resolved by id once a book is picked.
                return jsonBooks
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
                        ForeignAuthorId = row.Author?.ForeignAuthorId
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

                var importBody = new Dictionary<string, object>
                {
                    ["foreignBookId"] = ids.ForeignBookId,
                    ["foreignAuthorId"] = ids.ForeignAuthorId,
                    ["mediaType"] = format == BookFormat.Audiobook ? "audiobook" : "ebook",
                    ["authorMonitoring"] = new Dictionary<string, object>
                    {
                        ["monitor"] = "specificBook",
                        ["searchForMissing"] = Settings.SearchNewRequests
                    }
                };

                if (!string.IsNullOrWhiteSpace(Settings.RootFolder))
                {
                    importBody["rootFolder"] = Settings.RootFolder;
                }

                HttpResponseMessage response = await HttpPostAsync($"{BaseURL}/book/import", JsonConvert.SerializeObject(importBody));

                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    return new BookRequestResult { WasSuccess = false, Error = "That book matches several providers on Chaptarr; ask the admin to disambiguate it." };
                }

                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    string errorBody = await response.Content.ReadAsStringAsync();

                    // Chaptarr saves the request and adds it later when the metadata server is still preparing it.
                    if (errorBody.Contains("being prepared by the metadata server"))
                    {
                        return new BookRequestResult { WasSuccess = true, WasPending = true };
                    }

                    string error = ParseErrorMessage(errorBody);
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
        /// Chaptarr returns validation errors as [{propertyName, errorMessage}] and other errors as {error|message}.
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

        private class JSONAuthor
        {
            public string ForeignAuthorId { get; set; }
        }
    }
}
