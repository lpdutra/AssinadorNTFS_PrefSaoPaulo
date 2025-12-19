using System.Xml;

namespace AssinadorNFTS;

/// <summary>
/// Classe para realizar múltiplas tentativas de envio de NFTS com diferentes códigos de serviço
/// </summary>
public static class MultipleTryRequests
{
    // Lista de códigos de serviço possíveis para testar
    private static readonly List<int> CodigosServicoPossiveis = new()
    {
        1001, 1002, 
        9784
    };

    /// <summary>
    /// Realiza tentativas de geração, assinatura e envio de XMLs com diferentes códigos de serviço
    /// </summary>
    /// <param name="caminhoXmlOrigem">Caminho do XML de origem (nfts_minimum_data.xml)</param>
    /// <param name="caminhoCertificado">Caminho do certificado digital (.pfx)</param>
    /// <param name="senhaCertificado">Senha do certificado</param>
    public static async Task RealizarTentativas(string caminhoXmlOrigem, string caminhoCertificado, string senhaCertificado)
    {
        Console.WriteLine("=== Iniciando geração de XMLs com diferentes códigos de serviço ===\n");

        // Criar diretórios se não existirem
        string dirBase = Path.Combine(Path.GetDirectoryName(caminhoXmlOrigem)!, "..", "multiple_tries");
        string diretorioOrigem = Path.Combine(dirBase, "xml_origem");
        string diretorioRequests = Path.Combine(dirBase, "requests");
        string diretorioResponses = Path.Combine(dirBase, "responses");
        
        Directory.CreateDirectory(diretorioOrigem);
        Directory.CreateDirectory(diretorioRequests);
        Directory.CreateDirectory(diretorioResponses);

        Console.WriteLine($"Diretório de XMLs origem: {diretorioOrigem}");
        Console.WriteLine($"Diretório de requests: {diretorioRequests}");
        Console.WriteLine($"Diretório de responses: {diretorioResponses}");
        Console.WriteLine($"Total de códigos a testar: {CodigosServicoPossiveis.Count}\n");

        int contadorGerados = 0;
        int contadorAssinados = 0;
        int contadorEnviados = 0;
        
        foreach (int codigoServico in CodigosServicoPossiveis)
        {
            try
            {
                // Gerar XML de origem
                string caminhoXmlGerado = GerarXmlComCodigoServico(caminhoXmlOrigem, diretorioOrigem, codigoServico);
                contadorGerados++;
                Console.WriteLine($"[{contadorGerados}/{CodigosServicoPossiveis.Count}] ✓ XML origem gerado: {Path.GetFileName(caminhoXmlGerado)}");

                // Processar e assinar o XML
                RealizarAssinaturasXML.ProcessarNFTS(caminhoXmlGerado, caminhoCertificado, senhaCertificado, false);
                contadorAssinados++;
                
                // Mover o arquivo assinado para a pasta de requests
                string arquivoAssinado = caminhoXmlGerado.Replace(".xml", ".assinado.xml");
                string destinoRequest = Path.Combine(diretorioRequests, Path.GetFileName(arquivoAssinado));
                if (File.Exists(arquivoAssinado))
                {
                    File.Move(arquivoAssinado, destinoRequest, true);
                    Console.WriteLine($"[{contadorAssinados}/{CodigosServicoPossiveis.Count}] ✓ Request assinado: {Path.GetFileName(destinoRequest)}");
                }

                // Realizar chamada SOAP
                try
                {
                    await SoapClient.CallTesteEnvioLoteNFTS(destinoRequest, caminhoCertificado, senhaCertificado);
                    contadorEnviados++;
                    
                    // Mover a resposta gerada para o diretório de responses
                    string arquivoResponse = Path.Combine(diretorioRequests, "response.xml");
                    string nomeResponse = $"response_{codigoServico}.xml";
                    string destinoResponse = Path.Combine(diretorioResponses, nomeResponse);
                    
                    if (File.Exists(arquivoResponse))
                    {
                        File.Move(arquivoResponse, destinoResponse, true);
                        Console.WriteLine($"[{contadorEnviados}/{CodigosServicoPossiveis.Count}] ✓ Response salvo: {nomeResponse}\n");
                    }
                    else
                    {
                        Console.WriteLine($"[{contadorEnviados}/{CodigosServicoPossiveis.Count}] ⚠ Response não encontrado\n");
                    }
                }
                catch (Exception exSoap)
                {
                    Console.WriteLine($"[{contadorAssinados}/{CodigosServicoPossiveis.Count}] ✗ Erro na chamada SOAP para código {codigoServico}: {exSoap.Message}\n");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{contadorGerados + 1}/{CodigosServicoPossiveis.Count}] ✗ Erro ao processar código {codigoServico}: {ex.Message}\n");
            }
        }

        Console.WriteLine($"\n=== Processo concluído ===");
        Console.WriteLine($"XMLs origem gerados: {contadorGerados}");
        Console.WriteLine($"Requests assinados: {contadorAssinados}");
        Console.WriteLine($"Requests enviados: {contadorEnviados}");
    }

    /// <summary>
    /// Gera um novo XML alterando o código de serviço
    /// </summary>
    /// <returns>Caminho do arquivo XML gerado</returns>
    private static string GerarXmlComCodigoServico(string caminhoXmlOrigem, string diretorioDestino, int codigoServico)
    {
        // Carregar o XML original
        var xmlDoc = new XmlDocument();
        xmlDoc.Load(caminhoXmlOrigem);

        // Criar namespace manager
        var namespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
        namespaceManager.AddNamespace("nfts", "http://www.prefeitura.sp.gov.br/nfts");

        // Buscar o nó CodigoServico
        var codigoServicoNode = xmlDoc.SelectSingleNode("//CodigoServico") ?? 
                               xmlDoc.SelectSingleNode("//nfts:CodigoServico", namespaceManager);

        if (codigoServicoNode == null)
        {
            throw new InvalidOperationException("Tag <CodigoServico> não encontrada no XML.");
        }

        // Alterar o valor do código de serviço
        codigoServicoNode.InnerText = codigoServico.ToString();

        // Definir o caminho do novo arquivo
        string nomeArquivoOriginal = Path.GetFileNameWithoutExtension(caminhoXmlOrigem);
        string nomeNovoArquivo = $"{nomeArquivoOriginal}_{codigoServico}.xml";
        string caminhoNovoArquivo = Path.Combine(diretorioDestino, nomeNovoArquivo);

        // Salvar o novo XML
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            Encoding = System.Text.Encoding.UTF8,
            OmitXmlDeclaration = false
        };

        using (var writer = XmlWriter.Create(caminhoNovoArquivo, settings))
        {
            xmlDoc.Save(writer);
        }

        return caminhoNovoArquivo;
    }

    /// <summary>
    /// Retorna a lista de códigos de serviço possíveis
    /// </summary>
    public static List<int> ObterCodigosServicoPossiveis()
    {
        return new List<int>(CodigosServicoPossiveis);
    }

    /// <summary>
    /// Adiciona um código de serviço à lista
    /// </summary>
    public static void AdicionarCodigoServico(int codigoServico)
    {
        if (!CodigosServicoPossiveis.Contains(codigoServico))
        {
            CodigosServicoPossiveis.Add(codigoServico);
            Console.WriteLine($"Código de serviço {codigoServico} adicionado à lista.");
        }
        else
        {
            Console.WriteLine($"Código de serviço {codigoServico} já existe na lista.");
        }
    }
}
