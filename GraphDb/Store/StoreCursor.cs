using System;
using System.Text;

namespace GraphRoots.GraphDb;

static class StoreCursor
{
    const char Sep = '\u001f';

    public static string Encode(params string[] parts)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join(Sep, parts)));
    }

    public static string[] Decode(string cursor)
    {
        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return raw.Split(Sep);
        }
        catch (FormatException)
        {
            throw GraphStoreException.InvalidArgument("Invalid cursor.");
        }
    }
}
