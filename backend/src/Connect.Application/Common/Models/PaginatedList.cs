namespace Connect.Application.Common.Models;

public record PaginatedList<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int TotalPages,
    int TotalCount,
    bool HasPreviousPage,
    bool HasNextPage
)
{
    public static PaginatedList<T> Create(IReadOnlyList<T> items, int count, int pageNumber, int pageSize)
    {
        var totalPages = (int)Math.Ceiling(count / (double)pageSize);
        return new PaginatedList<T>(items, pageNumber, totalPages, count, pageNumber > 1, pageNumber < totalPages);
    }
}
