using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomResultError.Common;

public readonly struct PaginatedItemsList<T>

{
    public List<T> Items { get; init; }

    public int Page { get; init; }

    public int Count { get; init; }

    public int TotalCount { get; init; }

    public int TotalPages => (int)Math.Ceiling((double)TotalCount / Count);
}