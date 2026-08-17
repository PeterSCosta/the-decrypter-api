namespace TheDecrypter.Domain.Gateways;

/// <summary>
/// Reconhecimento de música a partir de um TRECHO de áudio.
///
/// Recebe áudio cru — não um fingerprint — porque quem recorta é o navegador, e
/// recortar é o que resolve os dois casos reais: várias faixas em sequência no
/// mesmo arquivo, e uma música em CADA canal ao mesmo tempo. Qualquer
/// fingerprint mistura para mono por padrão, e a soma de duas músicas não casa
/// com nenhuma delas.
///
/// Devolve null quando a chave não está configurada ou nada foi reconhecido —
/// mesmo contrato do <see cref="IWhat3WordsGateway"/>.
/// </summary>
public interface IMusicGateway
{
    /// <summary>
    /// Falso quando não há chave configurada.
    ///
    /// Existe porque "não reconheci este trecho" e "ninguém configurou a chave"
    /// são a MESMA resposta para quem olha a tela, e levam a ações opostas: no
    /// primeiro caso vale recortar outro pedaço, no segundo isso é perda de
    /// tempo até alguém mexer no servidor.
    /// </summary>
    bool Configurado { get; }

    Task<MusicaInfo?> IdentificarAsync(Stream audio, string nomeDoArquivo, CancellationToken ct = default);
}

/// <param name="Titulo">Título da faixa.</param>
/// <param name="Artista">Artista.</param>
/// <param name="Album">Álbum, quando o serviço traz.</param>
/// <param name="Lancamento">Data de lançamento, como o serviço a escreve.</param>
/// <param name="Timecode">
/// Onde, DENTRO da faixa original, o trecho enviado começa. É o que permite
/// juntar dois segmentos vizinhos que voltaram com o mesmo título: se o
/// timecode cresce, é a mesma música continuando, e não duas execuções.
/// </param>
/// <param name="Url">Link para ouvir, quando houver.</param>
public record MusicaInfo(
    string Titulo,
    string Artista,
    string? Album,
    string? Lancamento,
    string? Timecode,
    string? Url);
