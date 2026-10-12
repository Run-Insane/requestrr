using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Requestrr.WebApi.RequestrrBot.Books
{
    public class BookRequestingWorkflow
    {
        private readonly int _categoryId;
        private readonly BookUserRequester _user;
        private readonly IBookSearcher _bookSearcher;
        private readonly IBookRequester _requester;
        private readonly IBookUserInterface _userInterface;

        public BookRequestingWorkflow(
            BookUserRequester user,
            int categoryId,
            IBookSearcher searcher,
            IBookRequester requester,
            IBookUserInterface userInterface
        )
        {
            _categoryId = categoryId;
            _user = user;
            _bookSearcher = searcher;
            _requester = requester;
            _userInterface = userInterface;
        }


        public async Task SearchBooksAsync(string searchTerm)
        {
            searchTerm = searchTerm.Replace(".", " ");
            IReadOnlyList<Book> books = await _bookSearcher.SearchBooksAsync(new BookRequest(_user, _categoryId), searchTerm);

            if (!books.Any())
            {
                await _userInterface.WarnNoBookFoundAsync(searchTerm);
                return;
            }

            if (books.Count > 1)
            {
                await _userInterface.ShowBookSelectionAsync(new BookRequest(_user, _categoryId), books);
                return;
            }

            await HandleBookSelectionAsync(books.Single());
        }


        public async Task HandleBookSelectionAsync(string foreignBookId)
        {
            Book book = await _bookSearcher.SearchBookByIdAsync(new BookRequest(_user, _categoryId), foreignBookId);
            await HandleBookSelectionAsync(book);
        }


        private async Task HandleBookSelectionAsync(Book book)
        {
            if (book == null)
            {
                await _userInterface.WarnNoBookFoundAsync("(unknown)");
                return;
            }

            var requestable = book.AvailableFormats.Where(f => !book.InLibrary(f)).ToList();

            if (requestable.Count == 0)
            {
                await _userInterface.WarnBookAlreadyInLibraryAsync(book, book.AvailableFormats.FirstOrDefault());
                return;
            }

            if (requestable.Count == 1)
            {
                await RequestBookAsync(book.ForeignBookId, requestable.Single());
                return;
            }

            await _userInterface.ShowBookFormatSelectionAsync(new BookRequest(_user, _categoryId), book);
        }


        public async Task RequestBookAsync(string foreignBookId, BookFormat format)
        {
            Book book = await _bookSearcher.SearchBookByIdAsync(new BookRequest(_user, _categoryId), foreignBookId);

            if (book == null)
            {
                await _userInterface.WarnNoBookFoundAsync(foreignBookId);
                return;
            }

            BookRequestResult result = await _requester.RequestBookAsync(new BookRequest(_user, _categoryId), book, format);

            if (!result.WasSuccess)
            {
                await _userInterface.DisplayBookRequestDeniedAsync(book, format, result.Error);
            }
            else if (result.WasPending)
            {
                await _userInterface.DisplayBookRequestPendingAsync(book, format);
            }
            else
            {
                await _userInterface.DisplayBookRequestSuccessAsync(book, format);
            }
        }
    }
}
