using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Sys.Services;
using Microsoft.AspNetCore.Mvc;

namespace AidlyErp.Api.Controllers.Sys;

/// <summary>
/// Dedicated controller for the SYS1203 File/Image Manager.
///
/// <code>
/// GET    /files?entity_type=&amp;entity_no=   – metadata only, never bytes
/// POST   /files                            – multipart upload
/// GET    /files/{fileNo}/content           – stream the bytes
/// DELETE /files/{fileNo}                   – soft-delete
/// </code>
///
/// <para>Both the SYS1203 form route and the shorter <c>/api/v1/sys</c> alias are mapped, matching
/// the Java controller — the alias is what <c>serve_path</c> points at.</para>
/// </summary>
[ApiController]
[Route("api/v1/sys/forms/sys1203")]
[Route("api/v1/sys")]
public class Sys1203Controller : ApiControllerBase
{
    private readonly ISys1203Service _service;

    public Sys1203Controller(ISys1203Service service) => _service = service;

    [HttpGet("files")]
    public async Task<IActionResult> GetFiles([FromQuery(Name = "entity_type")] short? entityType,
                                              [FromQuery(Name = "entity_no")] long? entityNo,
                                              CancellationToken cancellationToken) =>
        OkResponse(await _service.GetFilesAsync(entityType, entityNo, cancellationToken));

    [HttpPost("files")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile? file,
                                            [FromForm(Name = "entity_type")] short? entityType,
                                            [FromForm(Name = "entity_no")] long? entityNo,
                                            [FromForm(Name = "is_primary")] short? isPrimary,
                                            CancellationToken cancellationToken)
    {
        var uploaded = await ReadAsync(file, cancellationToken);
        return OkResponse(await _service.UploadAsync(uploaded, entityType, entityNo, isPrimary, cancellationToken),
            "File uploaded");
    }

    [HttpGet("files/{fileNo:long}/content")]
    public async Task<IActionResult> GetContent(long fileNo, CancellationToken cancellationToken)
    {
        var content = await _service.GetContentAsync(fileNo, cancellationToken);
        return File(content.Bytes, content.ContentType ?? "application/octet-stream", content.FileName);
    }

    [HttpDelete("files/{fileNo:long}")]
    public async Task<IActionResult> Delete(long fileNo, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(fileNo, cancellationToken);
        return OkResponse<object?>(null, "File deleted");
    }

    [HttpPut("files/replace")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> ReplaceForEntity(IFormFile? file,
                                                      [FromForm(Name = "entity_type")] short? entityType,
                                                      [FromForm(Name = "entity_no")] long? entityNo,
                                                      CancellationToken cancellationToken)
    {
        var uploaded = await ReadAsync(file, cancellationToken);
        return OkResponse(await _service.ReplaceForEntityAsync(entityType, entityNo, uploaded, cancellationToken),
            "File replaced");
    }

    private static async Task<UploadedFile> ReadAsync(IFormFile? file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
        {
            throw new ValidationException("No file provided");
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);

        return new UploadedFile(file.FileName, file.ContentType, buffer.ToArray());
    }
}
