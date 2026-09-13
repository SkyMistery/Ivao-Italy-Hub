using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Content;

/// <summary>
/// The media library: the metadata are the generic CRUD engine like everything else, and the two
/// things a file needs that a row does not — being uploaded and being downloaded — are the two
/// pieces written here (design M1 section 2).
/// <para>The upload is the one endpoint of M1 written by hand, and why is worth keeping in view:
/// <c>MapCrud</c> speaks JSON, and a multipart request is not a JSON payload with one more field.
/// What was <b>not</b> done is moving it to a second address and leaving <c>POST /api/media</c> as
/// a JSON create: a row without a file must not be able to exist, so the resource declares that it
/// has no JSON create at all (<c>CrudOptions.MapCreate</c>).</para>
/// </summary>
public static class MediaEndpoints
{
    /// <summary>Where the resource lives; the generated client picks the path up from OpenAPI.</summary>
    public const string Pattern = "/api/media";

    public static RouteGroupBuilder MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var mapper = new MediaMapper();

        var group = app.MapCrud<MediaAsset, MediaListDto, MediaDetailDto, MediaWriteDto>(
            Pattern,
            options =>
            {
                options.PermissionArea = CorePermissions.MediaArea;

                // The library reads newest first: somebody uploading a file is about to use it.
                options.DefaultOrder = media => media.CreatedAt;

                options.Sortable.Add(nameof(MediaAsset.FileName));
                options.Sortable.Add(nameof(MediaAsset.ByteSize));
                options.Sortable.Add(nameof(MediaAsset.Category));
                options.Sortable.Add(nameof(MediaAsset.CreatedAt));
                options.Sortable.Add(nameof(MediaAsset.UpdatedAt));

                options.Filterable.Add(nameof(MediaAsset.OwnerDepartment));
                options.Filterable.Add(nameof(MediaAsset.Visibility));
                options.Filterable.Add(nameof(MediaAsset.Category));
                options.Filterable.Add(nameof(MediaAsset.ContentType));

                options.SearchFields.Add(media => media.FileName);
                options.SearchFields.Add(media => media.Alt);

                // A deleted file is out of the library and out of the picker; the row survives the
                // deletion, because a page that was already published names the identifier.
                options.Source = database => CrudSource.BackOffice<MediaAsset>(database)
                    .Where(media => media.DeletedAt == null);

                // No JSON create: a media is born from an upload, with its file already on disk.
                options.MapCreate = false;

                options.Delete = DeleteAsync;

                options.ToList = mapper.ToList;
                options.ToDetail = mapper.ToDetail;
                options.Apply = mapper.Apply;
            });

        group.MapPost("/", UploadAsync)
            .WithName("MediaUpload")
            .Produces<MediaDetailDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .RequireAuthorization(CorePermissions.MediaEdit)
            // Our own client proves it is ours with X-Requested-With, checked before authentication
            // for every write; the antiforgery token a browser form would carry is not part of how
            // this hub talks to itself.
            .DisableAntiforgery();

        return group;
    }

    /// <summary>
    /// The file itself, straight from Kestrel. Anonymous, because most of a public site is, and the
    /// global query filter is what decides: a staff-only file simply is not found by somebody who
    /// may not see it, which is 404 and not 403 — a 403 would confirm that it exists.
    /// </summary>
    public static void MapMediaFileEndpoint(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(MediaUrl.Pattern, ServeAsync)
            .WithName("MediaFile")
            .WithTags(CorePermissions.MediaArea)
            .ExcludeFromDescription()
            .AllowAnonymous();
    }

    private static async Task<IResult> UploadAsync(
        [FromForm] IFormFile file,
        [FromForm] Department ownerDepartment,
        [FromForm] Visibility? visibility,
        HubDbContext database,
        MediaStorage storage,
        IOptions<MediaOptions> media,
        IAuthorizationService authorization,
        ICurrentUser currentUser,
        LocaleCatalog catalog,
        IClock clock,
        HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(file);

        var limits = media.Value;

        if (file.Length <= 0 || file.Length > limits.MaxBytes)
        {
            return Refused(catalog, currentUser, "errors.media.tooLarge");
        }

        // What the browser called it is decoration. What the first bytes say it is decides both
        // whether it is allowed in and what it will be served as: a part of a multipart request
        // carries whatever content type its sender wrote.
        await using var content = file.OpenReadStream();
        var head = new byte[ImageHeader.ProbeBytes];
        var read = await ReadAsMuchAsPossibleAsync(content, head, http.RequestAborted);

        var format = MediaFormats.Detect(head.AsSpan(0, read));
        if (format is null || !limits.AllowedContentTypes.Contains(format.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return Refused(catalog, currentUser, "errors.media.typeNotAllowed");
        }

        var entity = new MediaAsset
        {
            OwnerDepartment = ownerDepartment,
            // Staff by default: a file becomes public because somebody said so, never by arriving.
            Visibility = visibility ?? Visibility.Staff,
            FileName = Path.GetFileName(file.FileName ?? string.Empty),
            ContentType = format.ContentType,
            ByteSize = file.Length,
        };

        if (!(await authorization.AuthorizeAsync(http.User, entity, CorePermissions.MediaEdit)).Succeeded)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: catalog.Resolve(currentUser.Locale, CrudProblems.ForbiddenTitleKey));
        }

        if (format.IsImage && ImageHeader.Read(head.AsSpan(0, read)) is { } size)
        {
            entity.Width = size.Width;
            entity.Height = size.Height;
        }

        // The bytes first and the row second: a row whose file is missing is a broken page, while
        // a file no row names is a few bytes nobody reads.
        var stored = await storage.SaveAsync(
            new HeadThenRestStream(head.AsMemory(0, read), content),
            format,
            clock.UtcNow,
            http.RequestAborted);

        // The same bytes, already in this department's library and still there: the file just
        // written goes, and the answer is the row that exists — 200 and not 201, which is how the
        // client tells the two apart (decision note of 12 September 2026). Per department and never
        // across: another department's file has its own visibility and its own alternative text.
        // Read past the query filter on purpose, so that a row this member happens not to see is
        // still not written twice.
        var existing = await CrudSource.BackOffice<MediaAsset>(database)
            .AsNoTracking()
            .Where(media => media.OwnerDepartment == ownerDepartment
                && media.Sha256 == stored.Sha256
                && media.DeletedAt == null
                && media.HasFile)
            .OrderBy(media => media.Id)
            .FirstOrDefaultAsync(http.RequestAborted);

        if (existing is not null)
        {
            storage.Delete(stored.StoredName);
            return Results.Ok(new MediaMapper().ToDetail(existing));
        }

        entity.StoredName = stored.StoredName;
        entity.Sha256 = stored.Sha256;

        database.Media.Add(entity);
        await database.SaveChangesAsync(http.RequestAborted);

        return Results.Created($"{Pattern}/{entity.Id}", new MediaMapper().ToDetail(entity));
    }

    /// <summary>
    /// Deleting a media takes the file, not the row. The row is what an already published page
    /// names, so it is marked and stays; the file goes only when no published version still shows
    /// it, because removing it under a published page would break a page already printed.
    /// </summary>
    private static async Task DeleteAsync(MediaAsset media, IServiceProvider services, CancellationToken cancellationToken)
    {
        var database = services.GetRequiredService<HubDbContext>();
        var storage = services.GetRequiredService<MediaStorage>();

        media.DeletedAt = services.GetRequiredService<IClock>().UtcNow;

        var stillShown = await database.ContentVersions
            .ShowingMedia(media.Id)
            .AnyAsync(cancellationToken);

        if (!stillShown && storage.Delete(media.StoredName))
        {
            media.HasFile = false;
        }
    }

    private static async Task<IResult> ServeAsync(
        long id,
        string name,
        HubDbContext database,
        MediaStorage storage,
        HttpContext http)
    {
        // No IgnoreQueryFilters and no visibility written here: the global query filter has already
        // decided, which is the whole point of it being global.
        var media = await database.Media
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == id, http.RequestAborted);

        if (media is null || !media.HasFile || storage.Find(media.StoredName) is not { } file)
        {
            return Results.NotFound();
        }

        // The address carries the identifier and a file never changes its bytes — a replacement is
        // a new upload — so it may be cached for a long time. Only a public file may be kept by
        // anything other than the browser that asked for it.
        http.Response.Headers.CacheControl = media.Visibility == Visibility.Public
            ? "public, max-age=31536000, immutable"
            : "private, max-age=3600";

        return Results.File(file.FullName, media.ContentType, enableRangeProcessing: true);
    }

    /// <summary>
    /// A refusal shaped exactly like the CRUD engine's, so the client maps it onto the field with
    /// the code it already has. The message is an i18n key, never a sentence.
    /// </summary>
    private static IResult Refused(LocaleCatalog catalog, ICurrentUser currentUser, string key) =>
        Results.ValidationProblem(
            new Dictionary<string, string[]> { ["file"] = [key] },
            title: catalog.Resolve(currentUser.Locale, CrudProblems.ValidationTitleKey));

    /// <summary>Fills as much of the buffer as the stream has, without demanding all of it.</summary>
    private static async Task<int> ReadAsMuchAsPossibleAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var got = await stream.ReadAsync(buffer.AsMemory(read), cancellationToken);
            if (got == 0)
            {
                break;
            }

            read += got;
        }

        return read;
    }

    /// <summary>
    /// The bytes already read in order to recognise the format, then the rest of the upload. The
    /// alternative was holding the whole file in memory in order to look at its first sixteen
    /// bytes, which is the same as having no size limit at all.
    /// </summary>
    private sealed class HeadThenRestStream(ReadOnlyMemory<byte> head, Stream rest) : Stream
    {
        private int _headOffset;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            if (_headOffset >= head.Length)
            {
                return rest.Read(buffer);
            }

            var take = Math.Min(buffer.Length, head.Length - _headOffset);
            head.Span.Slice(_headOffset, take).CopyTo(buffer);
            _headOffset += take;
            return take;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_headOffset >= head.Length)
            {
                return await rest.ReadAsync(buffer, cancellationToken);
            }

            var take = Math.Min(buffer.Length, head.Length - _headOffset);
            head.Slice(_headOffset, take).CopyTo(buffer);
            _headOffset += take;
            return take;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
