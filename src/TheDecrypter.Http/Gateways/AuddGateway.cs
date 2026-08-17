using System.Text.Json;
using Microsoft.Extensions.Configuration;
using TheDecrypter.Domain.Gateways;

namespace TheDecrypter.Http.Gateways;

/// <summary>
/// AudD — reconhecimento de música por upload de áudio cru.
///
/// Escolhido em vez do AcoustID por uma razão medida, não por preferência: o
/// AcoustID identifica pela FAIXA INTEIRA e exige um fingerprint do Chromaprint
/// bit-exato, que teria de ser reimplementado no navegador com risco de falhar
/// em silêncio. O AudD aceita um trecho do MEIO, que é exatamente o que a aba
/// recorta.
///
/// A cota gratuita é de ~300 requisições que NÃO renovam. É pouco, e por isso
/// quem decide o que enviar é a pessoa: a aba recorta na mão e manda um trecho
/// por vez, em vez de varrer o arquivo sozinha.
/// </summary>
public class AuddGateway(HttpClient http, IConfiguration config) : IMusicGateway
{
    public bool Configurado => !string.IsNullOrWhiteSpace(config["Gateways:Audd:Token"]);

    public async Task<MusicaInfo?> IdentificarAsync(
        Stream audio, string nomeDoArquivo, CancellationToken ct = default)
    {
        var token = config["Gateways:Audd:Token"];
        if (string.IsNullOrWhiteSpace(token)) return null;


        using var form = new MultipartFormDataContent
        {
            { new StringContent(token), "api_token" },
            { new StringContent("apple_music,spotify"), "return" },
        };
        using var conteudo = new StreamContent(audio);
        form.Add(conteudo, "file", nomeDoArquivo);

        using var resp = await http.PostAsync("", form, ct);
        if (!resp.IsSuccessStatusCode) return null;

        await using var s = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(s, cancellationToken: ct);
        var raiz = doc.RootElement;

        // A API responde 200 mesmo quando não reconhece; o que decide é o campo
        // `status` e o `result` vir nulo.
        if (!raiz.TryGetProperty("status", out var status) || status.GetString() != "success") return null;
        if (!raiz.TryGetProperty("result", out var r) || r.ValueKind != JsonValueKind.Object) return null;

        string? Texto(string nome) =>
            r.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        var titulo = Texto("title");
        var artista = Texto("artist");
        if (string.IsNullOrWhiteSpace(titulo) || string.IsNullOrWhiteSpace(artista)) return null;

        return new MusicaInfo(
            titulo,
            artista,
            Texto("album"),
            Texto("release_date"),
            Texto("timecode"),
            Texto("song_link"));
    }
}
