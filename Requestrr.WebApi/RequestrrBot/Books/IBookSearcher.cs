using System.Collections.Generic;
using System.Threading.Tasks;

namespace Requestrr.WebApi.RequestrrBot.Books
{
    public interface IBookSearcher
    {
        /// <param name="provider">Metadata source to search; null uses the configured default.</param>
        Task<IReadOnlyList<Book>> SearchBooksAsync(BookRequest request, string searchTerm, string provider = null);
        Task<Book> SearchBookByIdAsync(BookRequest request, string foreignBookId);
    }
}
