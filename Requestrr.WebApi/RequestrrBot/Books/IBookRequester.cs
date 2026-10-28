using System.Threading.Tasks;

namespace Requestrr.WebApi.RequestrrBot.Books
{
    public interface IBookRequester
    {
        Task<BookRequestResult> RequestBookAsync(BookRequest request, Book book, BookFormat format);
    }

    public class BookRequestResult
    {
        public bool WasSuccess { get; set; }

        /// <summary>True when Chaptarr queued the book as a pending import (metadata server still preparing it).</summary>
        public bool WasPending { get; set; }

        /// <summary>Human readable failure reason when WasSuccess is false.</summary>
        public string Error { get; set; }
    }
}
