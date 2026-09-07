using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace IvaoHub.Core.Data;

/// <summary>
/// Every instant this hub reads back from the database is UTC, and says so.
/// <para>MariaDB's <c>datetime</c> carries no time zone, so Pomelo hands every value back with
/// <see cref="DateTimeKind.Unspecified"/> — and <c>System.Text.Json</c> writes one of those with no
/// <c>Z</c> and no offset. A browser reads a bare date-time as **local**: an instant stored at 19:35
/// UTC arrived in the SPA as 19:35 local, which is 17:35 UTC, and every screen in the hub showed it
/// two hours early through a European summer. Lists, audit, calendar, published dates, contact
/// messages — all of them, because they all format the string the API sent.</para>
/// <para>The values in the database were right all along: what was missing was the model saying what
/// they are. The write side is deliberately left alone — everything the application stores is
/// <see cref="DateTime.UtcNow"/> — so this converter only stamps the kind on the way out, and a
/// value written with the wrong kind stays a bug somewhere else rather than being quietly
/// corrected here.</para>
/// <para>Found in G12 by somebody reading a date on screen and knowing what time it had been
/// (plan section 9.5, "UTC and the time zone of the division, both").</para>
/// </summary>
public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            stored => stored,
            read => DateTime.SpecifyKind(read, DateTimeKind.Utc))
    {
    }
}
