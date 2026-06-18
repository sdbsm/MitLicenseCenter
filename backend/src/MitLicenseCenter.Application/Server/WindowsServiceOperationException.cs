namespace MitLicenseCenter.Application.Server;

/// <summary>
/// Доменное исключение слоя управления службой Windows: операция не достигла целевого
/// состояния (жёсткий сбой <c>sc</c>, служба не найдена, истёк таймаут верификации —
/// ADR-55). Эндпоинт MLC-213+ мапит его в 409 с санитизированным русским текстом.
/// </summary>
public sealed class WindowsServiceOperationException : Exception
{
    public WindowsServiceOperationException(string message)
        : base(message)
    {
    }

    public WindowsServiceOperationException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
