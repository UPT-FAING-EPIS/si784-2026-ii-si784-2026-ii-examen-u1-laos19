using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicStream.Api.Dtos;
using MusicStream.Api.Infrastructure;
using MusicStream.Api.Services;

namespace MusicStream.Api.Controllers;

[ApiController]
[Route("plans")]
public class PlansController : ControllerBase
{
    private readonly IPlanService _plans;

    public PlansController(IPlanService plans)
    {
        _plans = plans;
    }

    /// <summary>Planes disponibles. Los administradores pueden incluir planes inactivos.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<PlanDto>>> List([FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => Ok(await _plans.ListAsync(includeInactive && User.IsAdmin(), ct));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PlanDto>> Create([FromBody] UpsertPlanRequest request, CancellationToken ct)
    {
        var plan = await _plans.CreateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, plan);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PlanDto>> Update(Guid id, [FromBody] UpsertPlanRequest request, CancellationToken ct)
        => Ok(await _plans.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await _plans.DeactivateAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("users")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly IUserAdminService _users;

    public UsersController(IUserAdminService users)
    {
        _users = users;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> List([FromQuery] string? search, CancellationToken ct)
        => Ok(await _users.ListAsync(search, ct));

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<UserDto>> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
        => Ok(await _users.UpdateAsync(id, request, User.GetUserId(), ct));
}

[ApiController]
[Route("reports")]
[Authorize(Roles = "Admin")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reports;

    public ReportsController(IReportService reports)
    {
        _reports = reports;
    }

    /// <summary>Genera un reporte de uso, suscripciones o ingresos para un periodo.</summary>
    [HttpPost]
    public async Task<ActionResult<ReportDto>> Generate([FromBody] GenerateReportRequest request, CancellationToken ct)
    {
        var report = await _reports.GenerateAsync(request, User.GetUserId(), ct);
        return StatusCode(StatusCodes.Status201Created, report);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReportDto>>> List(CancellationToken ct)
        => Ok(await _reports.ListAsync(ct));
}
