namespace TheDecrypter.Domain.Entities;

/// <summary>
/// Um código da CID-10 (DATASUS, V2008).
///
/// Categoria (<c>A00</c>) e subcategoria (<c>A00.0</c>) moram na mesma tabela: as
/// duas são código válido em prontuário e atestado, e separá-las obrigaria toda
/// consulta a perguntar duas vezes.
/// </summary>
public class Cid
{
    /// <summary>Sem ponto: <c>A000</c>. A grafia com ponto é de exibição.</summary>
    public string Codigo { get; set; } = "";

    public string Descricao { get; set; } = "";

    /// <summary>1 a 22.</summary>
    public short Capitulo { get; set; }
    public string CapituloDesc { get; set; } = "";

    /// <summary>O bloco dentro do capítulo (ex.: "Doenças hipertensivas").</summary>
    public string? GrupoDesc { get; set; }

    /// <summary>`+` etiologia (adaga), `*` manifestação (asterisco), ou vazio.</summary>
    public string? Classif { get; set; }

    /// <summary>"F" ou "M" quando o código só se aplica a um sexo.</summary>
    public string? Sexo { get; set; }

    /// <summary>Verdadeiro quando o código NÃO pode ser causa básica de óbito.</summary>
    public bool NaoObito { get; set; }
}
