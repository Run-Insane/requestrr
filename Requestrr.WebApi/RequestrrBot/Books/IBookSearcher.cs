using System.Collections.Generic;
using System.Threading.Tasks;

namespace Requestrr.WebApi.RequestrrBot.Books
{
    public interface IBookSearcher
    {
        Task<IReadOnlyList<Book>> SearchBooksAsync(BookRequest request, string searchTerm);
        Task<Book> SearchBookByIdAsync(BookRequest request, string foreignBookId);
    }
}
