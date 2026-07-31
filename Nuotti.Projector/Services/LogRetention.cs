using System;
using System.Collections.Generic;

namespace Nuotti.Projector.Services;

public static class LogRetention
{
    public const int DefaultMaximumRows = 200;

    public static void Append(IList<string> rows, string row, int maximumRows = DefaultMaximumRows)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumRows, 1);

        rows.Add(row);
        while (rows.Count > maximumRows)
        {
            rows.RemoveAt(0);
        }
    }
}
