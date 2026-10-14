using DSharpPlus;
using DSharpPlus.Entities;
using Requestrr.WebApi.RequestrrBot.ChatClients.Discord;
using Requestrr.WebApi.RequestrrBot.DownloadClients;
using Requestrr.WebApi.RequestrrBot.DownloadClients.Chaptarr;
using System;

namespace Requestrr.WebApi.RequestrrBot.Books
{
    public class BooksWorkflowFactory
    {
        private readonly DiscordSettingsProvider _settingsProvider;
        private readonly ChaptarrClient _chaptarrClient;

        public BooksWorkflowFactory(
            DiscordSettingsProvider settingsProvider,
            ChaptarrClient chaptarrClient
        )
        {
            _settingsProvider = settingsProvider;
            _chaptarrClient = chaptarrClient;
        }

        public BookRequestingWorkflow CreateRequestingWorkflow(DiscordInteraction interaction, int categoryId = 0)
        {
            DiscordSettings settings = _settingsProvider.Provide();
            return new BookRequestingWorkflow(
                new BookUserRequester(
                    interaction.User.Id.ToString(),
                    interaction.User.Username
                    ),
                categoryId,
                GetBooksClient<IBookSearcher>(settings),
                GetBooksClient<IBookRequester>(settings),
                new DiscordBookUserInterface(interaction)
                );
        }

        private T GetBooksClient<T>(DiscordSettings settings) where T : class
        {
            if (settings.BooksDownloadClient == DownloadClient.Chaptarr)
            {
                return _chaptarrClient as T;
            }

            throw new Exception($"Invalid configured books download client {settings.BooksDownloadClient}");
        }
    }
}
