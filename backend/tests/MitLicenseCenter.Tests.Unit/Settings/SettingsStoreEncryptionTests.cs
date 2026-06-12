using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Infrastructure.Settings;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Settings;

// DPAPI на CI/Linux недоступно, поэтому в тестах используем
// EphemeralDataProtectionProvider — он шифрует in-memory, но через тот же
// IDataProtectionProvider контракт, что и боевой path.
public sealed class SettingsStoreEncryptionTests
{
    private static (SettingsStore Store, AppDbContext Db, ISettingsSnapshot Snapshot) MakeStore()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"settings-{Guid.NewGuid():N}")
            .Options;
        var db = new AppDbContext(options);
        var snapshot = Substitute.For<ISettingsSnapshot>();
        var clock = TimeProvider.System;
        var store = new SettingsStore(db, new EphemeralDataProtectionProvider(), snapshot, clock);
        return (store, db, snapshot);
    }

    [Fact]
    public async Task Secret_value_round_trips_through_DPAPI()
    {
        var (store, db, _) = MakeStore();

        await store.SetAsync("OneC.Cluster.AdminPassword", "hunter2", isSecret: true, updatedBy: "admin");

        var row = await db.Settings.FirstAsync(x => x.Key == "OneC.Cluster.AdminPassword");
        row.IsSecret.Should().BeTrue();
        row.ValueText.Should().BeNull();
        row.Value.Should().NotBeNull();
        // Зашифрованный пейлоад НЕ содержит plaintext UTF-8 байт.
        var plaintextBytes = Encoding.UTF8.GetBytes("hunter2");
        Bytes.Contains(row.Value!, plaintextBytes).Should().BeFalse("DPAPI должен скрыть plaintext");

        var read = await store.GetAsync("OneC.Cluster.AdminPassword");
        read.Should().Be("hunter2");
    }

    [Fact]
    public async Task Plain_value_uses_ValueText_only()
    {
        var (store, db, _) = MakeStore();

        await store.SetAsync("OneC.Cluster.AdminUser", "admin", isSecret: false, updatedBy: "admin");

        var row = await db.Settings.FirstAsync(x => x.Key == "OneC.Cluster.AdminUser");
        row.IsSecret.Should().BeFalse();
        row.ValueText.Should().Be("admin");
        row.Value.Should().BeNull();
    }

    [Fact]
    public async Task Setting_null_clears_both_payload_columns()
    {
        var (store, db, _) = MakeStore();

        await store.SetAsync("OneC.Cluster.AdminPassword", "hunter2", isSecret: true, updatedBy: "admin");
        await store.SetAsync("OneC.Cluster.AdminPassword", null, isSecret: true, updatedBy: "admin");

        var row = await db.Settings.FirstAsync(x => x.Key == "OneC.Cluster.AdminPassword");
        row.Value.Should().BeNull();
        row.ValueText.Should().BeNull();
    }

    [Fact]
    public async Task GetIntAsync_parses_invariant_culture()
    {
        var (store, _, _) = MakeStore();

        await store.SetAsync("Polling.HotIntervalSeconds", "7", isSecret: false, updatedBy: "admin");

        var parsed = await store.GetIntAsync("Polling.HotIntervalSeconds");
        parsed.Should().Be(7);
    }

    [Fact]
    public async Task ListAsync_masks_secret_ValueText_even_if_present()
    {
        var (store, db, _) = MakeStore();

        // Симулируем «грязный» row: IsSecret=true и при этом ValueText заполнен —
        // store должен замаскировать на чтении, чтобы UI никогда не увидел plaintext.
        db.Settings.Add(new MitLicenseCenter.Domain.Settings.SettingEntry
        {
            Key = "Bogus.Secret",
            IsSecret = true,
            ValueText = "accidentally-plaintext",
            Value = null,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = "test",
        });
        await db.SaveChangesAsync();

        var list = await store.ListAsync();
        var entry = list.Single(x => x.Key == "Bogus.Secret");
        entry.ValueText.Should().BeNull();
        // IsSet тем не менее true — store видит, что значение присутствует.
        entry.IsSet.Should().BeTrue();
    }

    [Fact]
    public async Task GetAllAsync_returns_decrypted_secret_plain_and_null()
    {
        var (store, _, _) = MakeStore();

        await store.SetAsync("OneC.Cluster.AdminPassword", "hunter2", isSecret: true, updatedBy: "admin");
        await store.SetAsync("OneC.Cluster.AdminUser", "admin", isSecret: false, updatedBy: "admin");

        var all = await store.GetAllAsync();

        // Секрет приходит расшифрованным (как GetAsync), plain — как есть.
        all["OneC.Cluster.AdminPassword"].Should().Be("hunter2");
        all["OneC.Cluster.AdminUser"].Should().Be("admin");
        // Незаданный ключ просто отсутствует в словаре.
        all.ContainsKey("OneC.RAS.Endpoint").Should().BeFalse();
    }

    [Fact]
    public async Task GetAllAsync_matches_per_key_GetAsync()
    {
        var (store, _, _) = MakeStore();

        await store.SetAsync("OneC.Cluster.AdminPassword", "p@ss", isSecret: true, updatedBy: "admin");
        await store.SetAsync("Polling.HotIntervalSeconds", "7", isSecret: false, updatedBy: "admin");

        var all = await store.GetAllAsync();

        all["OneC.Cluster.AdminPassword"].Should().Be(await store.GetAsync("OneC.Cluster.AdminPassword"));
        all["Polling.HotIntervalSeconds"].Should().Be(await store.GetAsync("Polling.HotIntervalSeconds"));
    }

    [Fact]
    public async Task GetAsync_wraps_CryptographicException_with_operator_message()
    {
        // Симулируем потерянный/чужой key ring: значение зашифровано ОДНИМ провайдером,
        // а читается ДРУГИМ (независимый ephemeral key ring) — Unprotect не сможет
        // расшифровать чужой пейлоад и бросит CryptographicException. Store должен
        // обернуть его в понятную оператору ошибку (InvalidOperationException), сохранив
        // исходное исключение в InnerException.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"settings-crypto-{Guid.NewGuid():N}")
            .Options;
        var db = new AppDbContext(options);
        var snapshot = Substitute.For<ISettingsSnapshot>();
        var clock = TimeProvider.System;

        // Провайдер A пишет секрет.
        var writeStore = new SettingsStore(db, new EphemeralDataProtectionProvider(), snapshot, clock);
        await writeStore.SetAsync("OneC.Cluster.AdminPassword", "hunter2", isSecret: true, updatedBy: "admin");

        // Провайдер B (другой key ring) читает тот же row из той же БД.
        var readStore = new SettingsStore(db, new EphemeralDataProtectionProvider(), snapshot, clock);

        var act = async () => await readStore.GetAsync("OneC.Cluster.AdminPassword");

        var ex = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;
        ex.Message.Should().Contain("OneC.Cluster.AdminPassword");
        ex.Message.Should().Contain("key ring");
        ex.InnerException.Should().BeOfType<CryptographicException>();
    }

    [Fact]
    public async Task GetAllAsync_wraps_CryptographicException_with_operator_message()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"settings-crypto-all-{Guid.NewGuid():N}")
            .Options;
        var db = new AppDbContext(options);
        var snapshot = Substitute.For<ISettingsSnapshot>();
        var clock = TimeProvider.System;

        var writeStore = new SettingsStore(db, new EphemeralDataProtectionProvider(), snapshot, clock);
        await writeStore.SetAsync("OneC.Cluster.AdminPassword", "hunter2", isSecret: true, updatedBy: "admin");

        var readStore = new SettingsStore(db, new EphemeralDataProtectionProvider(), snapshot, clock);

        var act = async () => await readStore.GetAllAsync();

        var ex = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;
        ex.Message.Should().Contain("OneC.Cluster.AdminPassword");
        ex.InnerException.Should().BeOfType<CryptographicException>();
    }

    [Fact]
    public async Task SetAsync_invalidates_snapshot()
    {
        var (store, _, snapshot) = MakeStore();

        await store.SetAsync("OneC.Cluster.AdminUser", "admin", isSecret: false, updatedBy: "admin");

        snapshot.Received(1).Invalidate();
    }

    private static class Bytes
    {
        public static bool Contains(byte[] haystack, byte[] needle)
        {
            if (needle.Length == 0) return true;
            for (var i = 0; i <= haystack.Length - needle.Length; i++)
            {
                var match = true;
                for (var j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return true;
            }
            return false;
        }
    }
}
