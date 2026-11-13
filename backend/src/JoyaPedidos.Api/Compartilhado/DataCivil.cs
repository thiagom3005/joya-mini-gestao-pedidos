using System.Globalization;

namespace JoyaPedidos.Api.Compartilhado;

/// <summary>
/// Datas civis da query string e do eco de período (spec §5.1): formato exato <c>yyyy-MM-dd</c>, sem espaços nem
/// formatos culturais (ex.: <c>01/02/2026</c> é rejeitado).
/// </summary>
public static class DataCivil
{
    public const string Formato = "yyyy-MM-dd";

    public static bool TentarLer(string? texto, out DateOnly data) =>
        DateOnly.TryParseExact(texto, Formato, CultureInfo.InvariantCulture, DateTimeStyles.None, out data);

    public static string Formatar(DateOnly data) => data.ToString(Formato, CultureInfo.InvariantCulture);
}
