using System.Globalization;
using System.Text.RegularExpressions;
using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Infrastructure.Identity;

namespace MitLicenseCenter.Web.Endpoints;

public static partial class SettingsEndpoints
{
    // host:port — без пробелов, порт распарсивается отдельно для проверки диапазона.
    [GeneratedRegex(@"^(?<host>[^\s:]+):(?<port>\d+)$", RegexOptions.CultureInvariant)]
    private static partial Regex HostPortRegex();

    public static void MapSettingsEndpoints(this IEndpointRouteBuilder endpoints, ApiVersionSet versionSet)
    {
        var group = endpoints
            .MapGroup("/api/v{version:apiVersion}/settings")
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0))
            .WithTags("Settings");

        // Admin-only на всё: list показывает description секретов и метаданные,
        // их не должны видеть Viewer'ы (информация о наличии cluster creds — sensitive).
        group.MapGet("/", ListAsync).RequireAuthorization(Roles.Admin);
        group.MapPut("/{key}", UpdateAsync).RequireAuthorization(Roles.Admin);
    }

    internal static async Task<Ok<IReadOnlyList<SettingDescriptorResponse>>> ListAsync(
        ISettingsStore store,
        CancellationToken ct)
    {
        var items = await store.ListAsync(ct).ConfigureAwait(false);
        var dto = items
            .Select(s => new SettingDescriptorResponse(
                s.Key,
                s.IsSecret,
                s.IsSet,
                Value: s.IsSecret ? null : s.ValueText,
                s.Description,
                s.UpdatedAt,
                s.UpdatedBy))
            .ToList();
        return TypedResults.Ok((IReadOnlyList<SettingDescriptorResponse>)dto);
    }

    internal static async Task<Results<Ok, NotFound<ProblemDetails>, ValidationProblem>> UpdateAsync(
        string key,
        [FromBody] UpdateSettingRequest request,
        ISettingsStore store,
        IAuditLogger audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (!SettingDefinitions.All.TryGetValue(key, out var def))
        {
            var problem = new ProblemDetails
            {
                Type = "https://mitlicense.center/problems/setting-unknown",
                Title = "Неизвестный параметр",
                Status = StatusCodes.Status404NotFound,
                Detail = $"Параметр «{key}» не входит в whitelist.",
            };
            problem.Extensions["code"] = ProblemCodes.SettingUnknownKey;
            return TypedResults.NotFound(problem);
        }

        // null/whitespace → очистка значения (для секрета = «убрать пароль»).
        var normalized = string.IsNullOrWhiteSpace(request.Value) ? null : request.Value.Trim();

        var errors = ValidateValue(def, normalized);
        if (errors.Count > 0)
        {
            // Machine-readable code сидит в extensions ValidationProblem'а — frontend
            // отличает «значение не прошло валидацию» от прочих 400 по нему.
            return TypedResults.ValidationProblem(
                errors,
                extensions: new Dictionary<string, object?> { ["code"] = ProblemCodes.SettingInvalidValue });
        }

        var initiator = httpContext.User.Identity?.Name ?? "unknown";
        await store.SetAsync(def.Key, normalized, def.IsSecret, initiator, ct).ConfigureAwait(false);

        // ВАЖНО: для секретов description НЕ содержит plain-значения (даже masked).
        var description = def.IsSecret
            ? $"Параметр {def.Key} (секрет) обновлён."
            : $"Параметр {def.Key} изменён.";
        await audit.LogAsync(
            AuditActionType.SettingChanged,
            initiator: initiator,
            description: description,
            ct: ct).ConfigureAwait(false);

        return TypedResults.Ok();
    }

    private static Dictionary<string, string[]> ValidateValue(SettingDefinition def, string? normalized)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        // null допустимо для любого ключа — это сброс значения.
        if (normalized is null)
        {
            return errors;
        }

        const string field = nameof(UpdateSettingRequest.Value);

        switch (def.Kind)
        {
            case SettingValueKind.Number:
                if (!int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    errors[field] = ["Ожидается целое число."];
                }
                else if (def.Min is { } min && parsed < min)
                {
                    errors[field] = [$"Значение должно быть не меньше {min}."];
                }
                else if (def.Max is { } max && parsed > max)
                {
                    errors[field] = [$"Значение должно быть не больше {max}."];
                }
                break;

            case SettingValueKind.Url:
                if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    errors[field] = ["Ожидается абсолютный URL http(s)://…"];
                }
                break;

            case SettingValueKind.HostPort:
                var match = HostPortRegex().Match(normalized);
                if (!match.Success)
                {
                    errors[field] = ["Ожидается формат host:port."];
                }
                else if (!int.TryParse(match.Groups["port"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port)
                    || port < 1024
                    || port > 65535)
                {
                    errors[field] = ["Порт должен быть в диапазоне 1024–65535."];
                }
                break;

            case SettingValueKind.Path:
                // Без агрессивных проверок: путь может быть UNC, локальным,
                // относительным к ProgramData и т.д. Достаточно non-empty (normalized != null уже).
                break;

            case SettingValueKind.Text:
            default:
                break;
        }

        return errors;
    }
}
