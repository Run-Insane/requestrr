using System.Collections.Generic;
using System.Threading.Tasks;

namespace Requestrr.WebApi.RequestrrBot.Books
{
    public interface IBookUserInterface
    {
        Task ShowBookSelectionAsync(BookRequest request, IReadOnlyList<Book> books, string searchTerm);
        Task WarnNoBookFoundAsync(string searchTerm);
        Task ShowBookFormatSelectionAsync(BookRequest request, Book book);
        Task DisplayBookDetailsAsync(BookRequest request, Book book, BookFormat format);
        Task DisplayBookRequestSuccessAsync(Book book, BookFormat format);
        Task DisplayBookRequestPendingAsync(Book book, BookFormat format);
        Task DisplayBookRequestDeniedAsync(Book book, BookFormat format, string reason);
        Task WarnBookAlreadyInLibraryAsync(Book book, BookFormat format);
    }
}
