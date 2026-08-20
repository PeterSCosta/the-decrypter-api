namespace TheDecrypter.Domain.Search;

/// <summary>Que consultas uma entrada da bancada merece.</summary>
[Flags]
public enum Consultas
{
    Nenhuma = 0,
    CepExato = 1,
    CepPrefixoSc = 2,
    Municipio = 4,
    Plaqueta = 8,
    RuaOuBairro = 16,
    CepCuringa = 32,
    Aeroporto = 64,
    CidCodigo = 128,
    CidNome = 256,
    LoteBlumenau = 512,
    Filme = 1024,
}

/// <summary>
/// Os portões de forma, **no servidor**.
///
/// Antes eles moravam no `use-decoder.ts`, decidindo qual dataset baixar. Com a
/// consulta pela API, deixá-los no cliente significaria manter a mesma regra
/// escrita em dois idiomas — e elas divergiriam. Aqui é um lugar só, puro e
/// testável; o cliente mantém apenas um portão grosseiro de custo ("isto não
/// parece nada, nem pergunte").
/// </summary>
public static class LookupShape
{
    /// <summary>Acima disto é texto cifrado colado, não identificador.</summary>
    public const int MaxEntrada = 64;

    /// <summary>
    /// Os nomes das consultas que <see cref="De"/> abriu — vazio quando nenhuma.
    ///
    /// ── POR QUE O SERVIDOR PRECISA DIZER ISTO ──────────────────────────────
    /// A resposta de "não abri consulta nenhuma" é **byte a byte idêntica** à de
    /// "abri, perguntei e não achei": as duas saem `200` com todas as chaves em
    /// `null` (o `Program.cs` não configura `DefaultIgnoreCondition`, então nulo
    /// vai no JSON). Para o cliente as duas são o mesmo vazio.
    ///
    /// São coisas MUITO diferentes. "Perguntei em CEP e em plaqueta e nenhuma
    /// bateu" é informação; "não sei procurar isto" é outra; e apresentar a
    /// segunda como a primeira afirma uma busca que nunca houve. Numa lista de
    /// sessenta itens esse engano não erra uma linha — erra sessenta.
    ///
    /// A alternativa seria o cliente adivinhar a forma. É exatamente o que o
    /// cabeçalho desta classe proíbe: a mesma regra escrita em dois idiomas
    /// diverge, e o contraexemplo já existe — `MR-103` é chapa de estação
    /// geodésica, cai em <c>Nenhuma</c> aqui, e um espelho frouxo no cliente
    /// anunciaria "perguntei como plaqueta" sobre uma base nunca tocada.
    ///
    /// Os nomes saem crus (o identificador da bandeira). Traduzir é trabalho de
    /// tela, e uma bandeira nova aparecer na UI com o nome cru é degradação
    /// honesta — melhor que sumir de um mapa desatualizado.
    /// </summary>
    public static IReadOnlyList<string> Nomes(Consultas quais) =>
        quais == Consultas.Nenhuma
            ? []
            : [.. Enum.GetValues<Consultas>()
                .Where(f => f != Consultas.Nenhuma && quais.HasFlag(f))
                .Select(f => f.ToString())];

    public static Consultas De(string? entrada)
    {
        var texto = (entrada ?? string.Empty).Trim();
        if (texto.Length == 0 || texto.Length > MaxEntrada) return Consultas.Nenhuma;

        var c = Consultas.Nenhuma;

        // `tt` + 7 ou 8 dígitos: ID da IMDb. Vem cedo e sai cedo porque a forma
        // é fechada — nada mais no repositório tem esse desenho, e ele não abre
        // porta nenhuma das de baixo (duas letras não chegam ao mínimo de três
        // do ramo de nome, e o texto não é só dígito).
        // Duas portas para a mesma coisa: o código da IMDb (`tt1074638`) e o do
        // Wikidata (`Q4941`). Ver `WikidataId` para por que a segunda existe.
        // `tt1074638`, `Q2`, `P345`, `L1` — quatro formas, uma porta. Todas são
        // acerto EXATO: um código aponta para um registro e só um, o que é o
        // oposto do problema de ambiguidade que fechou a busca por nome.
        if (ImdbId.Parece(texto) || CodigoWikidata.Especie(texto) is not null) return Consultas.Filme;
        var digitos = new string([.. texto.Where(char.IsDigit)]);

        /*
         * ── A GRAFIA CANÔNICA DO CEP TEM MÁSCARA ───────────────────────────
         * `88010-500` e `88.010-500` são como o CEP se escreve — na conta de
         * luz, no site dos Correios, na prova. E eram o único jeito de escrever
         * CEP que esta função NÃO reconhecia: com o hífen, `soDigitos` era
         * falso, o bloco inteiro das faixas por dígito era pulado, e a resposta
         * saía "não abri consulta nenhuma". Na bancada isso passava (o cliente
         * limpa o texto antes); numa lista de CEPs colados, cada linha voltava
         * com "não sei procurar isto" — a lista mais provável que existe.
         *
         * O corte é EXATAMENTE oito dígitos, e não "só dígitos e separadores".
         * Sem ele, `-26.9194` (uma coordenada, seis dígitos) abriria consulta
         * de CEP e de município a cada linha de uma lista de coordenadas —
         * requisição gasta num balde que a equipe inteira divide.
         */
        var mascaraDeCep = digitos.Length == 8
            && texto.All(ch => char.IsDigit(ch) || ch is '.' or '-')
            && digitos.Length != texto.Length;

        var soDigitos = digitos.Length == texto.Length || mascaraDeCep;

        // Curinga explícito (88xxx500). Vem antes das outras formas, e sai com
        // `return`, porque a forma é fechada: um padrão de CEP não é número
        // nem nome, e se caísse adiante os três `x` da máscara contariam como
        // letras e "88xxx500" viraria também busca de rua.
        //
        // O portão é a FORMA INTEIRA, não "contém um x" — ver
        // <see cref="ParecePadraoDeCep"/>.
        if (ParecePadraoDeCep(texto) && CepPattern.Traduzir(texto) is not null)
            return Consultas.CepCuringa;

        // CID-10 antes das faixas por dígito: um código é letra + 2 ou 3
        // dígitos, com ou sem ponto, e não cai em nenhum dos ramos abaixo (uma
        // letra só não chega ao mínimo de três do ramo de nome).
        if (Search.CidCodigo.Normalizar(texto) is not null) c |= Consultas.CidCodigo;

        // Inscrição imobiliária de Blumenau: 15 dígitos, 12 dígitos, os grupos
        // pontuados do carnê de IPTU, ou o IQ colado (5 a 10 dígitos) que sai
        // de quem copia da tela do geoportal.
        //
        // A última forma PISA nos comprimentos que a bancada já usa — CEP tem
        // 8, IBGE tem 7, plaqueta tem até 6. Medido antes de abrir a porta:
        // dos 82.603 números do cadastro, ZERO são CEP existente; 81 são
        // geocódigo do IBGE e 1.166 dos curtos são plaqueta de poste. Por isso
        // aqui só se ABRE a consulta — quem decide se o card vale a pena é a
        // pré-resolução (não achou, não emite) e o peso por comprimento no
        // cliente, que mantém o lote de 7 dígitos abaixo do município.
        if (InscricaoBlumenau.Normalizar(texto) is not null) c |= Consultas.LoteBlumenau;

        if (soDigitos)
        {
            // 8 dígitos: CEP. Também é o comprimento de um ISPB do PIX, mas esse
            // vive numa lista que o app já carrega inteira.
            if (digitos.Length == 8) c |= Consultas.CepExato;
            // 6 dígitos: CEP sem o prefixo de SC (88/89) ou IBGE sem o dígito.
            if (digitos.Length == 6) c |= Consultas.CepPrefixoSc | Consultas.Municipio;
            if (digitos.Length == 7) c |= Consultas.Municipio;
            // Plaquetas vão de 1 a 6 dígitos (78% têm 5).
            if (digitos.Length is >= 1 and <= 6) c |= Consultas.Plaqueta;
        }
        else if (texto.Count(char.IsLetter) >= 3)
        {
            c |= Consultas.RuaOuBairro;
            // 3 letras = IATA (GRU), 4 = ICAO (SBGR). Só letras, sem espaço.
            if (texto.Length is 3 or 4 && texto.All(char.IsLetter)) c |= Consultas.Aeroporto;
            // Doença pelo NOME: a metade que a CID responde e o código não —
            // "qual o código da dengue". Só texto sem dígito, porque o número no
            // meio já é sinal de que aquilo é identificador de outra coisa.
            if (texto.Length >= 4 && !texto.Any(char.IsDigit)) c |= Consultas.CidNome;
        }

        return c;
    }

    /// <summary>
    /// A entrada é um padrão de CEP com curinga: **só** dígito, curinga e os
    /// separadores que o padrão ignora, com pelo menos um curinga e pelo menos
    /// um dígito.
    ///
    /// POR QUE A FORMA INTEIRA, E NÃO "contém um x": o portão antes pedia
    /// apenas que o texto tivesse um dos curingas, e `CepPattern.Traduzir`
    /// descartava as letras em vez de recusar o texto. Somados, os dois faziam
    /// `Rua XV` — a principal de Blumenau — sair daqui como <c>CepCuringa</c> e
    /// **não receber busca de rua nem de poste**; o mesmo acontecia com
    /// `XV de Novembro` e `Rua Max Tavares`.
    ///
    /// POR QUE NÃO HÁ PISO DE DÍGITOS: `8xxxxxxx` — um dígito e sete curingas —
    /// é uma pergunta legítima ("todos os CEPs que começam com 8"), e das mais
    /// baratas que existem aqui: a máscara de oito posições é ANCORADA, e o
    /// prefixo até o primeiro curinga vira o `LIKE '8%'` que o índice atende.
    /// Um piso de dígitos barrava exatamente esse caso. O único dígito exigido
    /// é o que separa filtro de tabela inteira: `xxxxxxxx` não é consulta, é o
    /// acervo todo.
    /// </summary>
    /// <remarks>
    /// PÚBLICO porque o portão do CSV precisa exatamente desta regra, e não de
    /// uma parecida. O `/cep/export` recusa o mesmo `xxxxxxxx` que o lookup
    /// recusa — se ele aceitasse só o que o `CepPattern.Traduzir` aceita, uma
    /// requisição só levaria a base de CEP inteira embora, pela porta que este
    /// método existe para fechar.
    /// </remarks>
    public static bool ParecePadraoDeCep(string? entrada)
    {
        var texto = (entrada ?? string.Empty).Trim();
        var digitos = texto.Count(char.IsDigit);
        if (digitos == 0) return false;
        var curingas = 0;
        foreach (var ch in texto)
        {
            if (char.IsDigit(ch) || CepPattern.ESeparador(ch)) continue;
            if (!CepPattern.Curingas.Contains(ch)) return false;
            curingas++;
        }
        return curingas > 0;
    }
}
