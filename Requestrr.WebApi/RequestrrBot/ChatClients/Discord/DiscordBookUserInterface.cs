using DSharpPlus;
using DSharpPlus.Entities;
using Requestrr.WebApi.RequestrrBot.Books;
using Requestrr.WebApi.RequestrrBot.Locale;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Requestrr.WebApi.RequestrrBot.ChatClients.Discord
{
    public class DiscordBookUserInterface : IBookUserInterface
    {
        private readonly DiscordInteraction _interactionContext;

        public DiscordBookUserInterface(DiscordInteraction interactionContext)
        {
            _interactionContext = interactionContext;
        }


        public async Task ShowBookSelectionAsync(BookRequest request, IReadOnlyList<Book> books, string searchTerm)
        {
            List<DiscordSelectComponentOption> options = books.Take(15).Select(x => new DiscordSelectComponentOption(GetFormattedBookName(x), $"{request.CategoryId}/{x.ForeignBookId}")).ToList();
            DiscordSelectComponent select = new DiscordSelectComponent($"BkRS/{_interactionContext.User.Id}/{request.CategoryId}", LimitStringSize(Language.Current.DiscordCommandBookRequestHelpDropdown), options);

            await _interactionContext.EditOriginalResponseAsync(new DiscordWebhookBuilder().AddComponents(select).WithContent(Language.Current.DiscordCommandBookRequestHelp.ReplaceTokens(LanguageTokens.SearchTerm, searchTerm)));
        }


        public async Task WarnNoBookFoundAsync(string searchTerm)
        {
            DiscordWebhookBuilder builder = await AddPreviousDropdownsAsync(new DiscordWebhookBuilder(), null);
            await _interactionContext.EditOriginalResponseAsync(builder.WithContent(Language.Current.DiscordCommandBookNotFound.ReplaceTokens(LanguageTokens.BookTitle, searchTerm)));
        }


        public async Task ShowBookFormatSelectionAsync(BookRequest request, Book book)
        {
            var options = book.AvailableFormats
                .Where(f => !book.InLibrary(f))
                .Select(f => new DiscordSelectComponentOption(GetFormatName(f), $"{request.CategoryId}/{book.ForeignBookId}/{(int)f}"))
                .ToList();

            DiscordSelectComponent select = new DiscordSelectComponent($"BkRF/{_interactionContext.User.Id}/{request.CategoryId}", LimitStringSize(Language.Current.DiscordCommandBookFormatHelpDropdown), options);

            string content = Language.Current.DiscordCommandBookFormatHelp.ReplaceTokens(LanguageTokens.BookTitle, book.Title);
            DiscordWebhookBuilder builder = await AddPreviousDropdownsAsync(new DiscordWebhookBuilder(), book);
            await _interactionContext.EditOriginalResponseAsync(builder.AddComponents(select).WithContent(content));
        }


        public async Task DisplayBookDetailsAsync(BookRequest request, Book book, BookFormat format)
        {
            DiscordButtonComponent requestButton = new DiscordButtonComponent(ButtonStyle.Primary, $"BkRC/{_interactionContext.User.Id}/{request.CategoryId}/{(int)format}/{book.ForeignBookId}", Language.Current.DiscordCommandRequestButton);
            string content = Language.Current.DiscordCommandBookRequestConfirm
                .ReplaceTokens(LanguageTokens.BookTitle, book.Title)
                .ReplaceTokens(LanguageTokens.BookFormat, GetFormatName(format));

            DiscordWebhookBuilder builder = await AddPreviousDropdownsAsync(new DiscordWebhookBuilder().AddEmbed(GenerateBookDetails(book)), book, format);
            await _interactionContext.EditOriginalResponseAsync(builder.AddComponents(requestButton).WithContent(content));
        }


        public async Task DisplayBookRequestSuccessAsync(Book book, BookFormat format)
        {
            DiscordButtonComponent successButton = new DiscordButtonComponent(ButtonStyle.Success, "0/1/0", Language.Current.DiscordCommandRequestButtonSuccess);
            string content = Language.Current.DiscordCommandBookRequestSuccess
                .ReplaceTokens(LanguageTokens.BookTitle, book.Title)
                .ReplaceTokens(LanguageTokens.BookFormat, GetFormatName(format));

            await _interactionContext.EditOriginalResponseAsync(new DiscordWebhookBuilder().AddEmbed(GenerateBookDetails(book)).AddComponents(successButton).WithContent(content));
        }


        public async Task DisplayBookRequestPendingAsync(Book book, BookFormat format)
        {
            DiscordButtonComponent pendingButton = new DiscordButtonComponent(ButtonStyle.Primary, "0/1/0", GetFormatName(format), true);
            string content = Language.Current.DiscordCommandBookRequestPending.ReplaceTokens(LanguageTokens.BookTitle, book.Title);

            await _interactionContext.EditOriginalResponseAsync(new DiscordWebhookBuilder().AddEmbed(GenerateBookDetails(book)).AddComponents(pendingButton).WithContent(content));
        }


        public async Task DisplayBookRequestDeniedAsync(Book book, BookFormat format, string reason)
        {
            DiscordButtonComponent deniedButton = new DiscordButtonComponent(ButtonStyle.Danger, "0/1/0", Language.Current.DiscordCommandRequestButtonDenied);
            string content = Language.Current.DiscordCommandBookRequestDenied
                .ReplaceTokens(LanguageTokens.BookTitle, book.Title)
                .ReplaceTokens(LanguageTokens.Error, string.IsNullOrWhiteSpace(reason) ? "unknown error" : reason);

            await _interactionContext.EditOriginalResponseAsync(new DiscordWebhookBuilder().AddEmbed(GenerateBookDetails(book)).AddComponents(deniedButton).WithContent(content));
        }


        public async Task WarnBookAlreadyInLibraryAsync(Book book, BookFormat format)
        {
            string content = Language.Current.DiscordCommandBookAlreadyInLibrary.ReplaceTokens(LanguageTokens.BookTitle, book.Title);
            DiscordWebhookBuilder builder = await AddPreviousDropdownsAsync(new DiscordWebhookBuilder().AddEmbed(GenerateBookDetails(book)), book);
            await _interactionContext.EditOriginalResponseAsync(builder.WithContent(content));
        }


        public static DiscordEmbed GenerateBookDetails(Book book)
        {
            DiscordEmbedBuilder embedBuilder = new DiscordEmbedBuilder()
                .WithTitle(book.Title)
                .WithTimestamp(DateTime.Now)
                .WithFooter("Powered by Requestrr");

            if (!string.IsNullOrWhiteSpace(book.AuthorTitle))
                embedBuilder.AddField($"__{Language.Current.DiscordEmbedBookAuthor}__", book.AuthorTitle, true);

            if (book.AvailableFormats.Any())
                embedBuilder.AddField($"__{Language.Current.DiscordEmbedBookFormat}__", string.Join(", ", book.AvailableFormats.Select(GetFormatName)), true);

            if (!string.IsNullOrWhiteSpace(book.Overview))
                embedBuilder.WithDescription(book.Overview.Substring(0, Math.Min(book.Overview.Length, 255)) + "(...)");

            if (!string.IsNullOrWhiteSpace(book.RemoteCover) && book.RemoteCover.StartsWith("http", StringComparison.InvariantCultureIgnoreCase))
                embedBuilder.WithImageUrl(book.RemoteCover);

            return embedBuilder.Build();
        }


        /// <summary>
        /// Keeps the book dropdown (and the format dropdown when it belongs to the same book) on the message,
        /// so the user can pick again until they confirm with the Request button.
        /// </summary>
        private async Task<DiscordWebhookBuilder> AddPreviousDropdownsAsync(DiscordWebhookBuilder builder, Book book, BookFormat? format = null)
        {
            List<DiscordSelectComponent> selectors = (await _interactionContext.GetOriginalResponseAsync()).FilterComponents<DiscordSelectComponent>().ToList();

            DiscordSelectComponent bookSelector = selectors.FirstOrDefault(x => x.CustomId.StartsWith("BkRS", StringComparison.OrdinalIgnoreCase));

            if (bookSelector != null)
            {
                string placeholder = book != null ? GetFormattedBookName(book) : bookSelector.Placeholder;
                builder.AddComponents(new DiscordSelectComponent(bookSelector.CustomId, placeholder, bookSelector.Options));
            }

            if (book != null && format.HasValue)
            {
                DiscordSelectComponent formatSelector = selectors.FirstOrDefault(x =>
                    x.CustomId.StartsWith("BkRF", StringComparison.OrdinalIgnoreCase) &&
                    x.Options.Any(o => o.Value.Contains($"/{book.ForeignBookId}/")));

                if (formatSelector != null)
                {
                    builder.AddComponents(new DiscordSelectComponent(formatSelector.CustomId, GetFormatName(format.Value), formatSelector.Options));
                }
            }

            return builder;
        }


        private string GetFormattedBookName(Book book)
        {
            string name = book.Title;

            if (!string.IsNullOrWhiteSpace(book.AuthorTitle))
                name = $"{name} ({book.AuthorTitle})";

            return LimitStringSize(name);
        }


        private static string GetFormatName(BookFormat format)
        {
            return format == BookFormat.Audiobook ? "Audiobook" : "Ebook";
        }


        private string LimitStringSize(string value, int limit = 100)
        {
            return value.Count() > limit ? value[..(limit - 3)] + "..." : value;
        }
    }
}
