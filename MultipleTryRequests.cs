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
        // 02660, 02668, 02684, 02692, 02800, 02693, 02881, 02919, 02920, 02935,
        // 02685, 08658, 08659, 01741, 06940, 06956, 07324,
        // 02498, 02499, 06581, 06613, 03210, 07109, 07111, 07123, 07124, 02350,
        // 02366, 02404, 02412, 02431, 02447, 02340, 02489, 07870, 07889, 07579,
        // 07323
        2350, 2412, 2499, 2920, 6956, 7111, 7124, 7889, 8659
    };

    // Dicionário com descrições dos códigos de erro (será preenchido automaticamente)
    private static Dictionary<string, string> DescricoesErros = new()
    {
        // { "410", "O Código do Serviço Prestado (<código do serviço prestado>) não encontrado." },
        // { "411", "Código do Serviço Prestado <código enviado> não permite dedução na base de cálculo." },
        // { "412", "Código do Serviço Prestado <código enviado> não é permitido para prestador pessoa física" },
        // { "413", "Código do Serviço Prestado <código enviado> não é permitido para prestador pessoa jurídica" },
        // { "310", "Código do Serviço Prestado (<código do serviço prestado>) inválido ou não permitido." },
        // { "222", "O código de serviço informado não corresponde à prestação de serviço." },
        { "464", "Os dados de endereço informados serão substituídos pelos relacionados ao CEP informado." }
    };

    /// <summary>
    /// Processa os arquivos de request já existentes na pasta de requests
    /// </summary>
    /// <param name="caminhoCertificado">Caminho do certificado digital (.pfx)</param>
    /// <param name="senhaCertificado">Senha do certificado</param>
    public static async Task FazerRequisicoesDosRequestsExistentes(string caminhoPastaRequests, string caminhoCertificado, string senhaCertificado)
    {
        Console.WriteLine("=== Iniciando processamento de requests existentes ===\n");

        // Definir diretórios
        string dirBase = caminhoPastaRequests;
        string diretorioRequests = Path.Combine(dirBase, "requests");
        string diretorioResponses = Path.Combine(dirBase, "responses");

        // Verificar se o diretório de requests existe
        if (!Directory.Exists(diretorioRequests))
        {
            Console.WriteLine($"❌ Diretório de requests não encontrado: {diretorioRequests}");
            return;
        }

        if (Directory.Exists(diretorioResponses))
        {
            Directory.Delete(diretorioResponses, true);
            Console.WriteLine("✓ Diretório responses limpo");
        }

        // Criar diretório de responses se não existir
        if (!Directory.Exists(diretorioResponses))
        {
            Directory.CreateDirectory(diretorioResponses);
            Console.WriteLine("✓ Diretório de responses criado");
        }

        // Buscar todos os arquivos XML na pasta de requests
        var arquivosRequest = Directory.GetFiles(diretorioRequests, "*.xml")
            .Where(f => !f.EndsWith("response.xml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f)
            .ToList();

        if (arquivosRequest.Count == 0)
        {
            Console.WriteLine($"⚠️ Nenhum arquivo XML encontrado em: {diretorioRequests}");
            return;
        }

        Console.WriteLine($"Diretório de requests: {diretorioRequests}");
        Console.WriteLine($"Diretório de responses: {diretorioResponses}");
        Console.WriteLine($"Total de requests encontrados: {arquivosRequest.Count}\n");

        int contadorEnviados = 0;
        int contadorSemErros = 0;
        
        // Mapa para armazenar código de serviço e seus códigos de erro
        var mapaErros = new Dictionary<int, List<string>>();

        foreach (string caminhoRequest in arquivosRequest)
        {
            try
            {
                // Extrair código de serviço do nome do arquivo
                string nomeArquivo = Path.GetFileName(caminhoRequest);
                int codigoServico = ExtrairCodigoServicoDaNomeArquivo(nomeArquivo);
                
                if (codigoServico == 0)
                {
                    Console.WriteLine($"⚠️ Não foi possível extrair código de serviço do arquivo: {Path.GetFileName(caminhoRequest)}\n");
                    continue;
                }

                Console.WriteLine($"[{contadorEnviados + 1}/{arquivosRequest.Count}] Processando: {Path.GetFileName(caminhoRequest)} (Código: {codigoServico})");

                // Realizar chamada SOAP
                try
                {
                    string nomeResponse = nomeArquivo.Replace(".xml", "response.xml");
                    string destinoResponse = Path.Combine(diretorioResponses, nomeResponse);
                    
                    await SoapClient.CallTesteEnvioLoteNFTS(caminhoRequest, caminhoCertificado, senhaCertificado, destinoResponse);
                    contadorEnviados++;
                    
                    if (File.Exists(destinoResponse))
                    {
                        Console.WriteLine($"[{contadorEnviados}/{arquivosRequest.Count}] ✓ Response salvo: {nomeResponse}");
                        
                        // Extrair códigos de erro da resposta
                        var codigosErro = ExtrairCodigosErro(codigoServico, destinoResponse);
                        mapaErros[codigoServico] = codigosErro;
                        
                        if (codigosErro.Count > 0)
                        {
                            Console.WriteLine($"   Erros encontrados: {string.Join(", ", codigosErro)}\n");
                        }
                        else
                        {
                            contadorSemErros++;
                            Console.WriteLine($"   ✓ Sem erros\n");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[{contadorEnviados}/{arquivosRequest.Count}] ⚠ Response não encontrado\n");
                        mapaErros[codigoServico] = new List<string> { "Response não encontrado" };
                    }
                }
                catch (Exception exSoap)
                {
                    Console.WriteLine($"[{contadorEnviados + 1}/{arquivosRequest.Count}] ✗ Erro na chamada SOAP para código {codigoServico}: {exSoap.Message}\n");
                    mapaErros[codigoServico] = new List<string> { $"Erro SOAP: {exSoap.Message}" };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Erro ao processar arquivo {Path.GetFileName(caminhoRequest)}: {ex.Message}\n");
            }
        }

        Console.WriteLine($"\n=== Processo concluído ===");
        Console.WriteLine($"Requests processados: {arquivosRequest.Count}");
        Console.WriteLine($"Requests enviados: {contadorEnviados}");
        Console.WriteLine($"Requests sem erros: {contadorSemErros}");
        
        // Exibir e salvar mapa de erros
        ExibirESalvarMapaErros(mapaErros, dirBase);
    }

    /// <summary>
    /// Realiza tentativas de geração, assinatura e envio de XMLs com diferentes códigos de serviço
    /// </summary>
    /// <param name="caminhoXmlOrigem">Caminho do XML de origem (nfts_minimum_data.xml)</param>
    /// <param name="caminhoCertificado">Caminho do certificado digital (.pfx)</param>
    /// <param name="senhaCertificado">Senha do certificado</param>
    public static async Task GerarArquivosERealizarTentativas(string caminhoXmlOrigem, string caminhoCertificado, string senhaCertificado)
    {
        Console.WriteLine("=== Iniciando geração de XMLs com diferentes códigos de serviço ===\n");

        // Definir diretórios
        string dirBase = Path.Combine(Path.GetDirectoryName(caminhoXmlOrigem)!, "..", "multiple_tries");
        string diretorioOrigem = Path.Combine(dirBase, "xml_origem");
        string diretorioRequests = Path.Combine(dirBase, "requests");
        string diretorioResponses = Path.Combine(dirBase, "responses");
        
        // Limpar diretórios se já existirem
        if (Directory.Exists(diretorioOrigem))
        {
            Directory.Delete(diretorioOrigem, true);
            Console.WriteLine("✓ Diretório xml_origem limpo");
        }
        if (Directory.Exists(diretorioRequests))
        {
            Directory.Delete(diretorioRequests, true);
            Console.WriteLine("✓ Diretório requests limpo");
        }
        if (Directory.Exists(diretorioResponses))
        {
            Directory.Delete(diretorioResponses, true);
            Console.WriteLine("✓ Diretório responses limpo");
        }
        
        // Criar diretórios
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
        int contadorSemErros = 0;
        
        // Mapa para armazenar código de serviço e seus códigos de erro
        var mapaErros = new Dictionary<int, List<string>>();
        
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
                    string nomeResponse = $"response_{codigoServico}.xml";
                    string destinoResponse = Path.Combine(diretorioResponses, nomeResponse);
                    
                    await SoapClient.CallTesteEnvioLoteNFTS(destinoRequest, caminhoCertificado, senhaCertificado, destinoResponse);
                    contadorEnviados++;
                    
                    if (File.Exists(destinoResponse))
                    {
                        Console.WriteLine($"[{contadorEnviados}/{CodigosServicoPossiveis.Count}] ✓ Response salvo: {nomeResponse}");
                        
                        // Extrair códigos de erro da resposta
                        var codigosErro = ExtrairCodigosErro(codigoServico, destinoResponse);
                        mapaErros[codigoServico] = codigosErro;
                        
                        if (codigosErro.Count > 0)
                        {
                            Console.WriteLine($"   Erros encontrados: {string.Join(", ", codigosErro)}\n");
                        }
                        else
                        {
                            contadorSemErros++;
                            Console.WriteLine($"   ✓ Sem erros\n");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[{contadorEnviados}/{CodigosServicoPossiveis.Count}] ⚠ Response não encontrado\n");
                        mapaErros[codigoServico] = new List<string> { "Response não encontrado" };
                    }
                }
                catch (Exception exSoap)
                {
                    Console.WriteLine($"[{contadorAssinados}/{CodigosServicoPossiveis.Count}] ✗ Erro na chamada SOAP para código {codigoServico}: {exSoap.Message}\n");
                    mapaErros[codigoServico] = new List<string> { $"Erro SOAP: {exSoap.Message}" };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{contadorGerados + 1}/{CodigosServicoPossiveis.Count}] ✗ Erro ao processar código {codigoServico}: {ex.Message}\n");
                mapaErros[codigoServico] = new List<string> { $"Erro no processamento: {ex.Message}" };
            }
        }

        Console.WriteLine($"\n=== Processo concluído ===");
        Console.WriteLine($"XMLs origem gerados: {contadorGerados}");
        Console.WriteLine($"Requests assinados: {contadorAssinados}");
        Console.WriteLine($"Requests enviados: {contadorEnviados}");
        Console.WriteLine($"Requests sem erros: {contadorSemErros}");
        
        // Exibir e salvar mapa de erros
        ExibirESalvarMapaErros(mapaErros, dirBase);
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
    /// Extrai códigos de erro do XML de resposta
    /// </summary>
    private static List<string> ExtrairCodigosErro(int codigoServico, string caminhoXmlResposta)
    {
        var codigosErro = new List<string>();
        
        try
        {
            // Ler o arquivo com a codificação correta (UTF-16)
            string xmlContent = File.ReadAllText(caminhoXmlResposta);
            
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xmlContent);
            
            // Criar namespace manager para lidar com namespaces
            var namespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
            namespaceManager.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");
            namespaceManager.AddNamespace("nfts", "http://www.prefeitura.sp.gov.br/nfts");
            
            // Buscar apenas por tags de erro (ignorando alertas)
            var nodosErro = xmlDoc.SelectNodes("//Erro | //nfts:Erro", namespaceManager);
            
            if (nodosErro != null)
            {
                foreach (XmlNode nodoErro in nodosErro)
                {
                    // Extrair código do erro
                    var nodoCodigo = nodoErro.SelectSingleNode("Codigo | CodigoErro | nfts:Codigo | nfts:CodigoErro", namespaceManager);
                    
                    if (nodoCodigo != null && !string.IsNullOrWhiteSpace(nodoCodigo.InnerText))
                    {
                        string codigoErro = nodoCodigo.InnerText.Trim();
                        codigosErro.Add(codigoErro);
                        
                        // Se o código não está no dicionário, adicionar com a descrição do XML
                        if (!DescricoesErros.ContainsKey(codigoErro))
                        {
                            // Procurar pela descrição/mensagem do erro
                            var nodoDescricao = nodoErro.SelectSingleNode("Descricao | Mensagem | Message | nfts:Descricao | nfts:Mensagem", namespaceManager);
                            
                            if (nodoDescricao != null && !string.IsNullOrWhiteSpace(nodoDescricao.InnerText))
                            {
                                string descricao = nodoDescricao.InnerText.Trim();
                                DescricoesErros[codigoErro] = descricao.Replace(codigoServico.ToString(), "<codigo servico>");
                                // Console.WriteLine($"   [INFO] Novo código de erro adicionado ao dicionário: {codigoErro} - {descricao}");
                            }
                        }
                    }
                }
            }
            
            // Se não encontrou códigos específicos, verificar se há mensagem de erro
            if (codigosErro.Count == 0)
            {
                var nodosMensagem = xmlDoc.SelectNodes("//Mensagem | //Message | //mensagem | //erro | //Erro | //nfts:Mensagem", namespaceManager);
                if (nodosMensagem != null && nodosMensagem.Count > 0)
                {
                    foreach (XmlNode nodo in nodosMensagem)
                    {
                        if (!string.IsNullOrWhiteSpace(nodo.InnerText))
                        {
                            // Adiciona apenas as primeiras palavras da mensagem
                            var mensagem = nodo.InnerText.Trim();
                            if (mensagem.Length > 50)
                                mensagem = mensagem.Substring(0, 50) + "...";
                            codigosErro.Add(mensagem);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            codigosErro.Add($"Erro ao ler XML: {ex.Message}");
        }
        
        return codigosErro;
    }
    
    /// <summary>
    /// Exibe e salva o mapa de códigos de serviço e seus erros
    /// </summary>
    private static void ExibirESalvarMapaErros(Dictionary<int, List<string>> mapaErros, string dirBase)
    {
        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine(new string('=', 80));
        sb.AppendLine("=== RESUMO: Códigos de Serviço x Erros ===");
        sb.AppendLine(new string('=', 80));
        
        if (mapaErros.Count == 0)
        {
            sb.AppendLine("Nenhum resultado para exibir.");
            Console.WriteLine(sb.ToString());
            return;
        }
        
        // Separar serviços com e sem erros
        var servicosComErro = new List<int>();
        var servicosSemErro = new List<int>();
        var todosCodigosErro = new HashSet<string>();
        
        foreach (var item in mapaErros.OrderBy(x => x.Key))
        {
            if (item.Value.Count == 0)
            {
                servicosSemErro.Add(item.Key);
            }
            else
            {
                servicosComErro.Add(item.Key);
                // Coletar todos os códigos de erro
                foreach (var erro in item.Value)
                {
                    todosCodigosErro.Add(erro);
                }
            }
        }
        
        // Exibir serviços COM erros
        if (servicosComErro.Count > 0)
        {
            sb.AppendLine("\n❌ CÓDIGOS COM ERROS:");
            foreach (var codigo in servicosComErro)
            {
                sb.AppendLine($"\nCódigo de Serviço: {codigo}");
                sb.AppendLine($"   Erros encontrados ({mapaErros[codigo].Count}):");
                foreach (var erro in mapaErros[codigo])
                {
                    sb.AppendLine($"   • {erro}");
                }
            }
        }
        
        // Exibir serviços SEM erros
        if (servicosSemErro.Count > 0)
        {
            sb.AppendLine("\n✅ CÓDIGOS SEM ERROS (SUCESSO):");
            sb.AppendLine($"   {string.Join(", ", servicosSemErro)}");
            sb.AppendLine($"\n   Total: {servicosSemErro.Count} código(s) processado(s) com sucesso!");
        }
        else
        {
            sb.AppendLine("\n⚠️ Nenhum código foi processado sem erros.");
        }
        
        // Adicionar dicionário de códigos de erro
        if (todosCodigosErro.Count > 0)
        {
            sb.AppendLine("\n" + new string('=', 80));
            sb.AppendLine("=== DICIONÁRIO DE CÓDIGOS DE ERRO ===");
            sb.AppendLine(new string('=', 80));
            
            // Filtrar apenas códigos que estão no dicionário
            var codigosComDescricao = todosCodigosErro
                .Where(codigo => DescricoesErros.ContainsKey(codigo))
                .OrderBy(x => x);
            
            foreach (var codigoErro in codigosComDescricao)
            {
                sb.AppendLine($"\n{codigoErro}");
                sb.AppendLine($"   {DescricoesErros[codigoErro]}");
            }
        }
        
        sb.AppendLine("\n" + new string('=', 80));
        
        // Exibir no console
        // Console.WriteLine(sb.ToString());
        
        // Salvar em arquivo
        try
        {
            string caminhoArquivo = Path.Combine(dirBase, "resumo_testes.txt");
            File.WriteAllText(caminhoArquivo, sb.ToString(), System.Text.Encoding.UTF8);
            Console.WriteLine($"📄 Resumo salvo em: {caminhoArquivo}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Erro ao salvar resumo em arquivo: {ex.Message}");
        }
    }

    /// <summary>
    /// Extrai o código de serviço do nome do arquivo
    /// </summary>
    /// <param name="nomeArquivo">Nome do arquivo sem extensão</param>
    /// <returns>Código de serviço ou 0 se não encontrado</returns>
    private static int ExtrairCodigoServicoDaNomeArquivo(string nomeArquivo)
    {
        nomeArquivo = nomeArquivo.ToLower().Replace(".xml", "");

        // Tentar extrair números do final do nome do arquivo
        // Exemplos esperados: "nfts_minimum_data_2350.assinado", "lote_7889", etc.
        
        // Remover sufixo .assinado se existir
        nomeArquivo = nomeArquivo.Replace(".assinado", "");
        
        // Dividir por underscore e pegar a última parte
        var partes = nomeArquivo.Split('_');
        string ultimaParte = partes[^1];
        
        // Tentar converter para int
        if (int.TryParse(ultimaParte, out int codigo))
        {
            return codigo;
        }
        
        return 0;
    }
}
