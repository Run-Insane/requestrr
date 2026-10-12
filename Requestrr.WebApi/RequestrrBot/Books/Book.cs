using System.Collections.Generic;

namespace Requestrr.WebApi.RequestrrBot.Books
{
    public enum BookFormat
    {
        Audiobook,
        Ebook
    }

    public class Book
    {
        /// <summary>Chaptarr work-level provider id, prefixed (hc:/gr:/ol:/gb:/az:/isbn:).</summary>
        public string ForeignBookId { get; set; }

        /// <summary>Prefixed author provider id from lookup (book.author.foreignAuthorId).</summary>
        public string ForeignAuthorId { get; set; }

        public string Title { get; set; }
        public string AuthorTitle { get; set; }
        public string Overview { get; set; }
        public string RemoteCover { get; set; }
        public string MediaType { get; set; }
        public System.DateTime? ReleaseDate { get; set; }

        /// <summary>Formats Chaptarr can import for this work. Only filled by an id lookup.</summary>
        public List<BookFormat> AvailableFormats { get; set; } = new List<BookFormat>();

        /// <summary>Provider ids of the lookup row returned for each format, sent back on import.</summary>
        public Dictionary<BookFormat, BookImportIds> FormatIds { get; set; } = new Dictionary<BookFormat, BookImportIds>();

        /// <summary>True when the format already exists in the Chaptarr library (local instance).</summary>
        public bool AudiobookInLibrary { get; set; }
        public bool EbookInLibrary { get; set; }

        public bool InLibrary(BookFormat format)
        {
            return format == BookFormat.Audiobook ? AudiobookInLibrary : EbookInLibrary;
        }
    }

    public class BookImportIds
    {
        public string ForeignBookId { get; set; }
        public string ForeignAuthorId { get; set; }
    }
}
