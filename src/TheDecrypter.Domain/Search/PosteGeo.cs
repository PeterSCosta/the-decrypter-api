namespace TheDecrypter.Domain.Search;

/// <summary>
/// A geometria da busca por proximidade de postes, num lugar só.
///
/// <para>
/// A coluna gerada <c>coord_bnu</c> guarda <c>point(lat, lng * 0.8918)</c>, e a
/// consulta precisa montar o ponto de comparação com **exatamente** a mesma
/// escala. Se as duas divergirem, o planejador simplesmente deixa de usar o
/// índice GiST — <b>sem erro</b>: os resultados continuam corretos e o tempo
/// despenca em silêncio, que é o pior tipo de falha. Por isso a constante mora
/// no domínio e há um teste conferindo-a contra o <c>schema.sql</c>.
/// </para>
/// <para>
/// A escala é única e compartilhada de propósito. Usar <c>cos(lat)</c> por linha
/// parece mais preciso e está errado: cada ponto escalado pelo próprio cosseno
/// introduz ~4 km de deslocamento fantasma entre dois pontos quaisquer.
/// </para>
/// </summary>
public static class PosteGeo
{
    /// <summary>cos(26,9°) — latitude central de Blumenau.</summary>
    public const double EscalaLng = 0.8918;

    /// <summary>Graus (na métrica escalada) → metros.</summary>
    public const double MetrosPorGrau = 111320.0;

    public static double EscalarLng(double lng) => lng * EscalaLng;
}
