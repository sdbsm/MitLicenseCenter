using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Persistence;

namespace MitLicenseCenter.Infrastructure.Settings;

internal sealed class SettingsStore : ISettingsStore
{
    // Purpose-string фиксирует scheme: если когда-нибудь сменим формат (например
    // добавим AAD-tag поверх плейн-байт), просто бамп до "mlc.settings.v2" + миграция.
    private const string ProtectorPurpose = "mlc.settings.v1";

    private readonly AppDbContext _db;
    private readonly IDataProtector _protector;
    private readonly ISettingsSnapshot _snapshot;
    private readonly TimeProvider _clock;

    public SettingsStore(
        AppDbContext db,
        IDataProtectionProvider dpProvider,
        ISettingsSnapshot snapshot,
        TimeProvider clock)
    {
        _db = db;
        _protector = dpProvider.CreateProtector(ProtectorPurpose);
        _snapshot = snapshot;
        _clock = clock;
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        var row = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key, ct).ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        if (row.IsSecret)
        {
            return row.Value is null ? null : Encoding.UTF8.GetString(_protector.Unprotect(row.Value));
        }

        return row.ValueText;
    }

    public async Task<int?> GetIntAsync(string key, CancellationToken ct = default)
    {
        var raw = await GetAsync(key, ct).ConfigureAwait(false);
        if (raw is null)
        {
            return null;
        }
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    public async Task SetAsync(string key, string? value, bool isSecret, string updatedBy, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        var row = await _db.Settings.FirstOrDefaultAsync(x => x.Key == key, ct).ConfigureAwait(false);
        var now = _clock.GetUtcNow().UtcDateTime;

        if (row is null)
        {
            row = new SettingEntry { Key = key };
            _db.Settings.Add(row);
        }

        row.IsSecret = isSecret;
        row.UpdatedAt = now;
        row.UpdatedBy = updatedBy;

        if (value is null)
        {
            // Clear: оба столбца обнуляются. IsSet → false на read-side.
            row.Value = null;
            row.ValueText = null;
        }
        else if (isSecret)
        {
            row.Value = _protector.Protect(Encoding.UTF8.GetBytes(value));
            row.ValueText = null;
        }
        else
        {
            row.ValueText = value;
            row.Value = null;
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        _snapshot.Invalidate();
    }

    public async Task<IReadOnlyList<SettingDescriptor>> ListAsync(CancellationToken ct = default)
    {
        var rows = await _db.Settings.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);
        // Маскировка секретов происходит здесь: ValueText зашифрованного row всегда null,
        // плюс на любой `IsSecret=true` сбрасываем ValueText в null даже если у row он
        // как-то затесался (защита от расхождения write-side инварианта).
        return rows
            .Select(r => new SettingDescriptor(
                Key: r.Key,
                IsSecret: r.IsSecret,
                IsSet: r.ValueText is not null || r.Value is not null,
                ValueText: r.IsSecret ? null : r.ValueText,
                Description: r.Description,
                UpdatedAt: r.UpdatedAt,
                UpdatedBy: r.UpdatedBy))
            .ToList();
    }
}
