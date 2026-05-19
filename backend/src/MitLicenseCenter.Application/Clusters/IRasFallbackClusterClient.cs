namespace MitLicenseCenter.Application.Clusters;

// Маркерный интерфейс: DI отличает REST-адаптер от RAS-адаптера по этому типу,
// не по имени. ResilientClusterClient инжектит оба — primary = IClusterClient (REST),
// fallback = IRasFallbackClusterClient (RAS). До PR 3.8 фоллбэк — StubRasClusterClient.
public interface IRasFallbackClusterClient : IClusterClient;
