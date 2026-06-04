using Microsoft.Extensions.Configuration;

namespace MitLicenseCenter.Infrastructure.Diagnostics;

// MLC-038 (PERF-02): опт-ин профиль EF-команд — чистые решения гейта + файловый приёмник,
// вынесенные сюда, чтобы их таблицу истинности можно было проверить юнит-тестом БЕЗ загрузки
// хоста (тот же приём, что TransportSecurity, MLC-012). DependencyInjection связывает эти
// решения с живым DbContextOptionsBuilder.LogTo.
//
// Дефолт — выключен: пока флаг Diagnostics:EfQueryProfiling не задан, LogTo не навешивается,
// файл-приёмник лениво не создаётся, секция Logging в appsettings не трогается → прод-логи и
// прод-поведение 1:1 (Database.Command остаётся Warning). EnableSensitiveDataLogging — за
// ОТДЕЛЬНЫМ явным флагом и только при включённом профиле: без явного opt-in значения параметров
// в открытом виде не пишутся ни при каких условиях.
internal static class EfQueryProfiling
{
    public const string EnabledKey = "Diagnostics:EfQueryProfiling";
    public const string SensitiveKey = "Diagnostics:EfSensitiveDataLogging";
    public const string LogPathKey = "Diagnostics:EfQueryProfilingLogPath";

    private static readonly object FileLock = new();

    public static bool IsEnabled(IConfiguration config)
        => config.GetValue<bool>(EnabledKey);

    // Sensitive — ТОЛЬКО при включённом профиле И собственном явном флаге; иначе всегда false.
    public static bool IsSensitiveEnabled(IConfiguration config)
        => IsEnabled(config) && config.GetValue<bool>(SensitiveKey);

    // Путь файла-приёмника; по умолчанию %LOCALAPPDATA%\MitLicenseCenter\perf\ef-profile.log.
    public static string ResolveLogPath(IConfiguration config)
    {
        var configured = config[LogPathKey];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "MitLicenseCenter", "perf", "ef-profile.log");
    }

    // Делегат для DbContextOptionsBuilder.LogTo: дописывает строку в файл-приёмник + Console.
    // Каталог/файл создаются ЛЕНИВО при первой записи — вызывается только когда профиль включён,
    // поэтому при выключенном флаге файла на диске нет (подтверждение «прод 1:1»). Append под
    // локом — scoped DbContext'ы могут логировать конкурентно (dev-диагностика, перф не важен).
    public static Action<string> BuildSink(IConfiguration config)
    {
        var path = ResolveLogPath(config);
        var directory = Path.GetDirectoryName(path);

        return line =>
        {
            lock (FileLock)
            {
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(path, line + Environment.NewLine);
            }

            Console.WriteLine(line);
        };
    }
}
