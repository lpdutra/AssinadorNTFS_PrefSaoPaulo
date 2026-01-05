using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using AssinadorNFTS.Models;

namespace AssinadorNFTS;

/// <summary>
/// Classe responsável por processar e assinar NFTS (Nota Fiscal de Tomador de Serviços)
/// </summary>
public static class RealizarAssinaturasXML
{
    private static string DEBUG_DIR = "D:\\Workspace\\FESP\\Projeto_NTFS\\processamento\\nfts_debug";

    /// <summary>
    /// Processa e assina uma NFTS a partir de um arquivo XML
    /// </summary>
    /// <param name="caminhoXml">Caminho do arquivo XML da NFTS</param>
    /// <param name="caminhoCertificado">Caminho do certificado digital (.pfx)</param>
    /// <param name="senhaCertificado">Senha do certificado</param>
    /// <param name="formatarXmlMensagem">Se true, formata o XML dentro da tag MensagemXML com indentação</param>
    public static void ProcessarNFTS(string caminhoXml, string caminhoCertificado, string senhaCertificado, bool formatarXmlMensagem = false)
    {
        // 1. Carregar certificado digital
        Console.WriteLine("Carregando certificado digital...");
        X509Certificate2 certificado = new X509Certificate2(caminhoCertificado, senhaCertificado);
        Console.WriteLine($"Certificado carregado: {certificado.Subject}");

        // 2. Carregar NFTS do arquivo XML
        Console.WriteLine($"Carregando NFTS do arquivo: {caminhoXml}");
        TpNfts nfts = CarregarNFTSDoXml(caminhoXml);
        CabecalhoPedidoEnvioLote? cabecalho = CarregarCabecalhoDoXml(caminhoXml);
        Console.WriteLine($"NFTS carregada - Inscrição: {nfts.ChaveDocumento?.InscricaoMunicipal}, Valor: {nfts.ValorServicos:C2}");
        if (cabecalho != null)
            Console.WriteLine($"Cabeçalho carregado - Período: {cabecalho.DtInicio:yyyy-MM-dd} a {cabecalho.DtFim:yyyy-MM-dd}");

        // 3. Assinar a NFTS
        Console.WriteLine("Assinando NFTS...");
        
        // Criar diretório de debug se não existir
        Console.WriteLine($"Debug dir: {DEBUG_DIR}");
        
        // Mostrar o XML que será usado para gerar a assinatura
        var xmlParaAssinar = Encoding.UTF8.GetString(AssinadorXml.SimpleXmlFragment(nfts));
        Console.WriteLine("\n=== String XML utilizada para gerar a assinatura ===");
        Console.WriteLine(xmlParaAssinar);
        Console.WriteLine("=== Fim da string XML ===\n");
        
        // Assinar passando o diretório de debug e o contador (1 para a primeira NFTS)
        nfts.Assinatura = AssinadorXml.Assinar(certificado, nfts, DEBUG_DIR, 1);
        Console.WriteLine($"Assinatura gerada: {Convert.ToBase64String(nfts.Assinatura)}");

        // 4. Salvar XML assinado
        Console.WriteLine("Salvando XML assinado...");
        // string caminhoSaida = Path.Combine(Path.GetDirectoryName(caminhoXml)!, "request.assinado.xml");
        string caminhoSaida = caminhoXml.Replace(".xml", ".assinado.xml");
        SalvarXmlSoap(nfts, caminhoSaida, certificado, cabecalho, formatarXmlMensagem);
        Console.WriteLine($"XML assinado salvo em: {caminhoSaida}");

        // 5. Comparar com arquivo original usando WinMerge
        // string arquivoOriginal = @"D:\Workspace\FESP\Projeto_NTFS\python\nfts_assinado_original.xml";
        // AbrirWinMerge(caminhoSaida, arquivoOriginal);
    }

    /// <summary>
    /// Processa e assina uma NFTS a partir de uma string XML
    /// </summary>
    public static string GetValorTagAssinaturaNFTS(string xmlString, string caminhoCertificado, string senhaCertificado)
    {
        // 1. Carregar certificado digital
        Console.WriteLine("Carregando certificado digital...");
        X509Certificate2 certificado = new X509Certificate2(caminhoCertificado, senhaCertificado);
        Console.WriteLine($"Certificado carregado: {certificado.Subject}");

        byte[] assinatura = AssinadorXml.AssinarXmlString(certificado, xmlString);
        string assinaturaBase64 = Convert.ToBase64String(assinatura);
        Console.WriteLine("Assinatura=" + assinaturaBase64);

        return assinaturaBase64;
    }

    /// <summary>
    /// Salva objeto NFTS em arquivo XML no formato SOAP TesteEnvioLoteNFTSRequest com CDATA
    /// </summary>
    private static void SalvarXmlSoap(TpNfts nfts, string caminhoArquivo, X509Certificate2 certificado, CabecalhoPedidoEnvioLote? cabecalho = null, bool formatarXmlMensagem = false)
    {
        // Gerar o PedidoEnvioLoteNFTS
        string pedidoXml = GerarPedidoEnvioLoteNFTS(nfts, cabecalho, certificado, formatarXmlMensagem);
        
        // Trocar aspas simples por duplas na declaração XML
        pedidoXml = pedidoXml.Replace("<?xml version='1.0' encoding='utf-8'?>", "<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        
        // Criar envelope SOAP
        using (var memoryStream = new MemoryStream())
        {
            var settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = false,
                Indent = true,
                IndentChars = "  ",
                Encoding = new UTF8Encoding(false), // UTF-8 sem BOM
                NewLineChars = "\n" // Unix line endings (LF)
            };

            using (var xmlWriter = XmlWriter.Create(memoryStream, settings))
            {
                xmlWriter.WriteStartDocument();
                
                // soap:Envelope
                xmlWriter.WriteStartElement("soap", "Envelope", "http://schemas.xmlsoap.org/soap/envelope/");
                
                // soap:Body
                xmlWriter.WriteStartElement("soap", "Body", "http://schemas.xmlsoap.org/soap/envelope/");
                
                // TesteEnvioLoteNFTSRequest
                xmlWriter.WriteStartElement("ns0", "TesteEnvioLoteNFTSRequest", "http://www.prefeitura.sp.gov.br/nfts");
                
                // VersaoSchema
                xmlWriter.WriteElementString("ns0", "VersaoSchema", "http://www.prefeitura.sp.gov.br/nfts", "2");
                
                // MensagemXML com CDATA
                xmlWriter.WriteStartElement("ns0", "MensagemXML", "http://www.prefeitura.sp.gov.br/nfts");
                xmlWriter.WriteCData(pedidoXml);
                xmlWriter.WriteEndElement(); // MensagemXML
                
                xmlWriter.WriteEndElement(); // TesteEnvioLoteNFTSRequest
                xmlWriter.WriteEndElement(); // Body
                xmlWriter.WriteEndElement(); // Envelope
                xmlWriter.WriteEndDocument();
            }
            
            File.WriteAllBytes(caminhoArquivo, memoryStream.ToArray());
        }
    }

    /// <summary>
    /// Gera o XML do PedidoEnvioLoteNFTS com a NFTS assinada
    /// </summary>
    private static string GerarPedidoEnvioLoteNFTS(TpNfts nfts, CabecalhoPedidoEnvioLote? cabecalho = null, X509Certificate2? certificado = null, bool formatarXml = false)
    {
        using (var memoryStream = new MemoryStream())
        {
            using (var xmlWriter = XmlWriter.Create(memoryStream, new XmlWriterSettings
            {
                OmitXmlDeclaration = false,  // Incluir declaração XML
                Indent = formatarXml,
                IndentChars = formatarXml ? "  " : "",
                Encoding = new UTF8Encoding(false), // false = sem BOM
                NewLineHandling = formatarXml ? NewLineHandling.Replace : NewLineHandling.None
            }))
            {
                xmlWriter.WriteStartDocument();
                xmlWriter.WriteStartElement("PedidoEnvioLoteNFTS", "http://www.prefeitura.sp.gov.br/nfts");
                xmlWriter.WriteAttributeString("xmlns", "xsd", null, "http://www.w3.org/2001/XMLSchema");
                xmlWriter.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
                
                // Cabeçalho
                xmlWriter.WriteStartElement("Cabecalho", "");
                xmlWriter.WriteAttributeString("xmlns", ""); // Força xmlns="" a aparecer primeiro
                xmlWriter.WriteAttributeString("Versao", "1");
                
                xmlWriter.WriteStartElement("Remetente");
                xmlWriter.WriteStartElement("CPFCNPJ");
                // Usar CPF ou CNPJ do cabeçalho se disponível, senão usar do Prestador
                if (cabecalho?.Remetente?.Cpfcnpj != null)
                {
                    if (!string.IsNullOrEmpty(cabecalho.Remetente.Cpfcnpj.Cpf))
                        xmlWriter.WriteElementString("CPF", cabecalho.Remetente.Cpfcnpj.Cpf);
                    else if (!string.IsNullOrEmpty(cabecalho.Remetente.Cpfcnpj.Cnpj))
                        xmlWriter.WriteElementString("CNPJ", cabecalho.Remetente.Cpfcnpj.Cnpj);
                }
                xmlWriter.WriteEndElement(); // CPFCNPJ
                xmlWriter.WriteEndElement(); // Remetente
                
                xmlWriter.WriteElementString("transacao", "true");
                
                // Usar datas do cabeçalho se fornecido, senão usar datas dinâmicas
                if (cabecalho != null)
                {
                    xmlWriter.WriteElementString("dtInicio", cabecalho.DtInicio.ToString("yyyy-MM-dd"));
                    xmlWriter.WriteElementString("dtFim", cabecalho.DtFim.ToString("yyyy-MM-dd"));
                    xmlWriter.WriteElementString("QtdNFTS", cabecalho.QtdNfts.ToString());
                    xmlWriter.WriteElementString("ValorTotalServicos", cabecalho.ValorTotalServicos.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                    
                    // ValorTotalDeducoes é opcional
                    if (cabecalho.ValorTotalDeducoesSpecified)
                        xmlWriter.WriteElementString("ValorTotalDeducoes", cabecalho.ValorTotalDeducoes.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                }
                else
                {
                    xmlWriter.WriteElementString("dtInicio", DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd"));
                    xmlWriter.WriteElementString("dtFim", DateTime.Now.ToString("yyyy-MM-dd"));
                    xmlWriter.WriteElementString("QtdNFTS", "1");
                    xmlWriter.WriteElementString("ValorTotalServicos", nfts.ValorServicos.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                }
                
                xmlWriter.WriteEndElement(); // Cabecalho
                
                // NFTS
                xmlWriter.WriteStartElement("NFTS", "");
                
                // TipoDocumento
                xmlWriter.WriteElementString("TipoDocumento", nfts.TipoDocumento switch
                {
                    TpTipoDocumentoNfts.Item01 => "01",
                    TpTipoDocumentoNfts.Item02 => "02",
                    TpTipoDocumentoNfts.Item03 => "03",
                    _ => "02"
                });
                
                // ChaveDocumento
                if (nfts.ChaveDocumento != null)
                {
                    xmlWriter.WriteStartElement("ChaveDocumento");
                    xmlWriter.WriteElementString("InscricaoMunicipal", nfts.ChaveDocumento.InscricaoMunicipal.ToString());
                    xmlWriter.WriteElementString("SerieNFTS", nfts.ChaveDocumento.SerieNfts ?? "");
                    xmlWriter.WriteElementString("NumeroDocumento", nfts.ChaveDocumento.NumeroDocumento.ToString());
                    xmlWriter.WriteEndElement(); // ChaveDocumento
                }
                
                // Outros campos
                xmlWriter.WriteElementString("DataPrestacao", nfts.DataPrestacao.ToString("yyyy-MM-dd"));
                xmlWriter.WriteElementString("StatusNFTS", nfts.StatusNfts == TpStatusNfts.C ? "C" : "N");
                xmlWriter.WriteElementString("TributacaoNFTS", nfts.TributacaoNfts switch
                {
                    TpTributacaoNfts.I => "I",
                    TpTributacaoNfts.J => "J",
                    _ => "T"
                });
                
                xmlWriter.WriteElementString("ValorServicos", nfts.ValorServicos.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                xmlWriter.WriteElementString("ValorDeducoes", nfts.ValorDeducoes.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                xmlWriter.WriteElementString("CodigoServico", nfts.CodigoServico.ToString("D4"));
                
                if (nfts.CodigoSubItemSpecified)
                    xmlWriter.WriteElementString("CodigoSubItem", nfts.CodigoSubItem.ToString("D3"));
                
                // AliquotaServicos sem formatação específica - usa o valor natural do decimal
                xmlWriter.WriteElementString("AliquotaServicos", nfts.AliquotaServicos.ToString(System.Globalization.CultureInfo.InvariantCulture));
                xmlWriter.WriteElementString("ISSRetidoTomador", nfts.IssRetidoTomador.ToString().ToLower());
                
                if (nfts.IssRetidoIntermediarioSpecified)
                    xmlWriter.WriteElementString("ISSRetidoIntermediario", nfts.IssRetidoIntermediario.ToString().ToLower());
                
                // Prestador
                if (nfts.Prestador != null)
                {
                    xmlWriter.WriteStartElement("Prestador");
                    
                    if (nfts.Prestador.Cpfcnpj != null)
                    {
                        xmlWriter.WriteStartElement("CPFCNPJ");
                        if (!string.IsNullOrEmpty(nfts.Prestador.Cpfcnpj.Cnpj))
                            xmlWriter.WriteElementString("CNPJ", nfts.Prestador.Cpfcnpj.Cnpj);
                        else if (!string.IsNullOrEmpty(nfts.Prestador.Cpfcnpj.Cpf))
                            xmlWriter.WriteElementString("CPF", nfts.Prestador.Cpfcnpj.Cpf);
                        xmlWriter.WriteEndElement(); // CPFCNPJ
                    }
                    
                    if (nfts.Prestador.InscricaoMunicipalSpecified)
                        xmlWriter.WriteElementString("InscricaoMunicipal", nfts.Prestador.InscricaoMunicipal.ToString());
                    
                    xmlWriter.WriteElementString("RazaoSocialPrestador", nfts.Prestador.RazaoSocialPrestador ?? "");
                    
                    // Endereço (se existir)
                    if (nfts.Prestador.Endereco != null)
                    {
                        xmlWriter.WriteStartElement("Endereco");
                        if (!string.IsNullOrEmpty(nfts.Prestador.Endereco.TipoLogradouro))
                        xmlWriter.WriteElementString("TipoLogradouro", nfts.Prestador.Endereco.TipoLogradouro ?? "");
                        if (!string.IsNullOrEmpty(nfts.Prestador.Endereco.Logradouro))
                        xmlWriter.WriteElementString("Logradouro", nfts.Prestador.Endereco.Logradouro ?? "");
                        if (!string.IsNullOrEmpty(nfts.Prestador.Endereco.NumeroEndereco))
                        xmlWriter.WriteElementString("NumeroEndereco", nfts.Prestador.Endereco.NumeroEndereco ?? "");
                        if (!string.IsNullOrEmpty(nfts.Prestador.Endereco.ComplementoEndereco))
                            xmlWriter.WriteElementString("ComplementoEndereco", nfts.Prestador.Endereco.ComplementoEndereco);
                        if (!string.IsNullOrEmpty(nfts.Prestador.Endereco.Bairro))
                        xmlWriter.WriteElementString("Bairro", nfts.Prestador.Endereco.Bairro ?? "");
                        if (!string.IsNullOrEmpty(nfts.Prestador.Endereco.Cidade))
                        xmlWriter.WriteElementString("Cidade", nfts.Prestador.Endereco.Cidade ?? "");
                        if (!string.IsNullOrEmpty(nfts.Prestador.Endereco.Uf))
                        xmlWriter.WriteElementString("UF", nfts.Prestador.Endereco.Uf ?? "");
                        if (nfts.Prestador.Endereco.CepSpecified)
                            xmlWriter.WriteElementString("CEP", nfts.Prestador.Endereco.Cep.ToString());
                        xmlWriter.WriteEndElement(); // Endereco
                    }
                    
                    if (!string.IsNullOrEmpty(nfts.Prestador.Email))
                        xmlWriter.WriteElementString("Email", nfts.Prestador.Email);
                    
                    xmlWriter.WriteEndElement(); // Prestador
                }
                
                xmlWriter.WriteElementString("RegimeTributacao", nfts.RegimeTributacao.ToString());
                
                if (nfts.DataPagamentoSpecified)
                    xmlWriter.WriteElementString("DataPagamento", nfts.DataPagamento.ToString("yyyy-MM-dd"));
                
                // Discriminacao é opcional, mas se incluído deve ter pelo menos 1 caractere
                if (!string.IsNullOrEmpty(nfts.Discriminacao))
                    xmlWriter.WriteElementString("Discriminacao", nfts.Discriminacao);
                
                xmlWriter.WriteElementString("TipoNFTS", nfts.TipoNfts.ToString());
                
                // Tomador
                if (nfts.Tomador != null)
                {
                    xmlWriter.WriteStartElement("Tomador");
                    
                    if (nfts.Tomador.Cpfcnpj != null)
                    {
                        xmlWriter.WriteStartElement("CPFCNPJ");
                        if (!string.IsNullOrEmpty(nfts.Tomador.Cpfcnpj.Cnpj))
                            xmlWriter.WriteElementString("CNPJ", nfts.Tomador.Cpfcnpj.Cnpj);
                        else if (!string.IsNullOrEmpty(nfts.Tomador.Cpfcnpj.Cpf))
                            xmlWriter.WriteElementString("CPF", nfts.Tomador.Cpfcnpj.Cpf);
                        xmlWriter.WriteEndElement(); // CPFCNPJ
                    }
                    
                    xmlWriter.WriteElementString("RazaoSocial", nfts.Tomador.RazaoSocial ?? "");
                    
                    xmlWriter.WriteEndElement(); // Tomador
                }
                
                // Assinatura
                if (nfts.Assinatura != null)
                {
                    xmlWriter.WriteElementString("Assinatura", Convert.ToBase64String(nfts.Assinatura));
                }
                
                xmlWriter.WriteEndElement(); // NFTS
                
                xmlWriter.WriteEndElement(); // PedidoEnvioLoteNFTS
                xmlWriter.WriteEndDocument();
            }
            
            // Converter o MemoryStream para string UTF-8 sem BOM
            byte[] xmlBytes = memoryStream.ToArray();
            string xmlString = new UTF8Encoding(false).GetString(xmlBytes);
            
            // Remover BOM se existir (BOM UTF-8 = 0xEF, 0xBB, 0xBF ou char code 65279)
            if (xmlString.Length > 0 && xmlString[0] == '\uFEFF')
            {
                xmlString = xmlString.Substring(1);
            }
            
            // Substituir a declaração XML completa com aspas simples
            if (xmlString.StartsWith("<?xml"))
            {
                int endDeclaration = xmlString.IndexOf("?>");
                if (endDeclaration > 0)
                {
                    // Remover a declaração antiga e adicionar a nova
                    xmlString = "<?xml version='1.0' encoding='utf-8'?>" + xmlString.Substring(endDeclaration + 2);
                }
            }
            
            // Adicionar assinatura XMLDSig se certificado fornecido
            if (certificado != null)
            {
                xmlString = AdicionarAssinaturaXmlDSig(xmlString, certificado);
            }
            
            return xmlString;
        }
    }

    /// <summary>
    /// Adiciona a assinatura XMLDSig ao XML do PedidoEnvioLoteNFTS
    /// </summary>
    private static string AdicionarAssinaturaXmlDSig(string xmlString, X509Certificate2 certificado)
    {
        try
        {
            // 1. Carregar o XML preservando espaços em branco
            var xmlDoc = new XmlDocument { PreserveWhitespace = true };
            xmlDoc.LoadXml(xmlString);

            // 2. Criar a assinatura XML
            var signedXml = new SignedXml(xmlDoc);
            
            // Configurar método de canonicalização (Exclusive XML Canonicalization)
            signedXml.SignedInfo!.CanonicalizationMethod = SignedXml.XmlDsigExcC14NTransformUrl;
            
            // Configurar método de assinatura (RSA-SHA1)
            signedXml.SignedInfo!.SignatureMethod = SignedXml.XmlDsigRSASHA1Url;
            
            // Usar a chave privada do certificado
            signedXml.SigningKey = certificado.GetRSAPrivateKey();

            // 3. Configurar a referência (todo o documento)
            var reference = new Reference("");
            reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
            reference.DigestMethod = SignedXml.XmlDsigSHA1Url;
            signedXml.AddReference(reference);

            // 4. Configurar informações do certificado
            var keyInfo = new KeyInfo();
            keyInfo.AddClause(new KeyInfoX509Data(certificado));
            signedXml.KeyInfo = keyInfo;

            // 5. Computar a assinatura
            signedXml.ComputeSignature();

            // 6. Obter o elemento da assinatura e formatá-lo
            var signatureElement = signedXml.GetXml();

            // 7. Obter a assinatura como XML compacto (sem formatação)
            string signatureXml = signatureElement.OuterXml;

            // 8. Adicionar a assinatura ao final do XML (antes de </PedidoEnvioLoteNFTS>)
            int closingTagIndex = xmlString.LastIndexOf("</PedidoEnvioLoteNFTS>");
            if (closingTagIndex > 0)
            {
                xmlString = xmlString.Substring(0, closingTagIndex) + signatureXml + xmlString.Substring(closingTagIndex);
            }

            return xmlString;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao gerar assinatura XMLDSig: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
            return xmlString; // Retornar o XML sem assinatura em caso de erro
        }
    }

    /// <summary>
    /// Carrega o cabeçalho do lote de um arquivo XML (PedidoEnvioLoteNFTS)
    /// </summary>
    private static CabecalhoPedidoEnvioLote? CarregarCabecalhoDoXml(string caminhoArquivo)
    {
        try
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(caminhoArquivo);

            var namespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
            namespaceManager.AddNamespace("nfts", "http://www.prefeitura.sp.gov.br/nfts");

            // Buscar o nó Cabecalho
            var cabecalhoNode = xmlDoc.SelectSingleNode("//nfts:Cabecalho", namespaceManager) ?? 
                               xmlDoc.SelectSingleNode("//Cabecalho");

            if (cabecalhoNode == null)
                return null;

            var cabecalho = new CabecalhoPedidoEnvioLote();

            // Ler Remetente
            var remetenteNode = cabecalhoNode.SelectSingleNode("Remetente") ?? 
                               cabecalhoNode.SelectSingleNode("nfts:Remetente", namespaceManager);
            if (remetenteNode != null)
            {
                var cpfcnpjNode = remetenteNode.SelectSingleNode("CPFCNPJ") ?? 
                                 remetenteNode.SelectSingleNode("nfts:CPFCNPJ", namespaceManager);
                if (cpfcnpjNode != null)
                {
                    var cnpj = cpfcnpjNode.SelectSingleNode("CNPJ")?.InnerText ?? 
                              cpfcnpjNode.SelectSingleNode("nfts:CNPJ", namespaceManager)?.InnerText;
                    var cpf = cpfcnpjNode.SelectSingleNode("CPF")?.InnerText ?? 
                             cpfcnpjNode.SelectSingleNode("nfts:CPF", namespaceManager)?.InnerText;
                    
                    cabecalho.Remetente = new TpRemetente
                    {
                        Cpfcnpj = new TpCpfcnpj()
                    };
                    
                    if (!string.IsNullOrEmpty(cnpj))
                        cabecalho.Remetente.Cpfcnpj.Cnpj = cnpj;
                    else if (!string.IsNullOrEmpty(cpf))
                        cabecalho.Remetente.Cpfcnpj.Cpf = cpf;
                }
            }

            // Ler dtInicio
            var dtInicioText = cabecalhoNode.SelectSingleNode("dtInicio")?.InnerText ?? 
                              cabecalhoNode.SelectSingleNode("nfts:dtInicio", namespaceManager)?.InnerText;
            if (DateTime.TryParse(dtInicioText, out DateTime dtInicio))
                cabecalho.DtInicio = dtInicio;

            // Ler dtFim
            var dtFimText = cabecalhoNode.SelectSingleNode("dtFim")?.InnerText ?? 
                           cabecalhoNode.SelectSingleNode("nfts:dtFim", namespaceManager)?.InnerText;
            if (DateTime.TryParse(dtFimText, out DateTime dtFim))
                cabecalho.DtFim = dtFim;

            // Ler QtdNFTS
            var qtdText = cabecalhoNode.SelectSingleNode("QtdNFTS")?.InnerText ?? 
                         cabecalhoNode.SelectSingleNode("nfts:QtdNFTS", namespaceManager)?.InnerText;
            if (int.TryParse(qtdText, out int qtd))
                cabecalho.QtdNfts = qtd;

            // Ler ValorTotalServicos
            var valorText = cabecalhoNode.SelectSingleNode("ValorTotalServicos")?.InnerText ?? 
                           cabecalhoNode.SelectSingleNode("nfts:ValorTotalServicos", namespaceManager)?.InnerText;
            if (decimal.TryParse(valorText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal valor))
                cabecalho.ValorTotalServicos = valor;

            // Ler ValorTotalDeducoes
            var valorDeducoesText = cabecalhoNode.SelectSingleNode("ValorTotalDeducoes")?.InnerText ?? 
                                   cabecalhoNode.SelectSingleNode("nfts:ValorTotalDeducoes", namespaceManager)?.InnerText;
            if (!string.IsNullOrEmpty(valorDeducoesText) && decimal.TryParse(valorDeducoesText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal valorDeducoes))
            {
                cabecalho.ValorTotalDeducoes = valorDeducoes;
                cabecalho.ValorTotalDeducoesSpecified = true;
            }

            return cabecalho;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Aviso: Não foi possível carregar cabeçalho do XML: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Carrega NFTS de um arquivo XML (PedidoEnvioLoteNFTS)
    /// </summary>
    private static TpNfts CarregarNFTSDoXml(string caminhoArquivo)
    {
        try
        {
            // Carregar o XML como documento
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(caminhoArquivo);

            // Criar namespace manager para lidar com o namespace do XML
            var namespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
            namespaceManager.AddNamespace("nfts", "http://www.prefeitura.sp.gov.br/nfts");

            // Buscar o nó NFTS
            var nftsNode = xmlDoc.SelectSingleNode("//nfts:NFTS", namespaceManager);
            if (nftsNode == null)
            {
                // Tentar sem namespace
                nftsNode = xmlDoc.SelectSingleNode("//NFTS");
            }
            
            if (nftsNode == null)
            {
                throw new InvalidOperationException("Tag <NFTS> não encontrada no XML.");
            }

            // Ler manualmente os dados do XML e criar o objeto TpNfts
            var nfts = new TpNfts();
            
            // TipoDocumento
            var tipoDoc = nftsNode.SelectSingleNode("TipoDocumento")?.InnerText ?? 
                          nftsNode.SelectSingleNode("nfts:TipoDocumento", namespaceManager)?.InnerText;
            nfts.TipoDocumento = tipoDoc switch
            {
                "01" => TpTipoDocumentoNfts.Item01,
                "02" => TpTipoDocumentoNfts.Item02,
                "03" => TpTipoDocumentoNfts.Item03,
                _ => TpTipoDocumentoNfts.Item02
            };

            // ChaveDocumento
            var chaveNode = nftsNode.SelectSingleNode("ChaveDocumento") ?? 
                           nftsNode.SelectSingleNode("nfts:ChaveDocumento", namespaceManager);
            if (chaveNode != null)
            {
                nfts.ChaveDocumento = new TpChaveDocumento
                {
                    InscricaoMunicipal = long.Parse(chaveNode.SelectSingleNode("InscricaoMunicipal")?.InnerText ?? 
                                                    chaveNode.SelectSingleNode("nfts:InscricaoMunicipal", namespaceManager)?.InnerText ?? "0"),
                    SerieNfts = chaveNode.SelectSingleNode("SerieNFTS")?.InnerText ?? 
                               chaveNode.SelectSingleNode("nfts:SerieNFTS", namespaceManager)?.InnerText,
                    NumeroDocumento = ulong.Parse(chaveNode.SelectSingleNode("NumeroDocumento")?.InnerText ?? 
                                                 chaveNode.SelectSingleNode("nfts:NumeroDocumento", namespaceManager)?.InnerText ?? "0"),
                    NumeroDocumentoSpecified = true
                };
            }

            // DataPrestacao
            var dataPrestacao = nftsNode.SelectSingleNode("DataPrestacao")?.InnerText ?? 
                               nftsNode.SelectSingleNode("nfts:DataPrestacao", namespaceManager)?.InnerText;
            nfts.DataPrestacao = DateTime.Parse(dataPrestacao ?? DateTime.Now.ToString("yyyy-MM-dd"));

            // StatusNFTS
            var status = nftsNode.SelectSingleNode("StatusNFTS")?.InnerText ?? 
                        nftsNode.SelectSingleNode("nfts:StatusNFTS", namespaceManager)?.InnerText;
            nfts.StatusNfts = status == "C" ? TpStatusNfts.C : TpStatusNfts.N;

            // TributacaoNFTS
            var trib = nftsNode.SelectSingleNode("TributacaoNFTS")?.InnerText ?? 
                      nftsNode.SelectSingleNode("nfts:TributacaoNFTS", namespaceManager)?.InnerText;
            nfts.TributacaoNfts = trib switch
            {
                "I" => TpTributacaoNfts.I,
                "J" => TpTributacaoNfts.J,
                _ => TpTributacaoNfts.T
            };

            // Valores
            nfts.ValorServicos = decimal.Parse(nftsNode.SelectSingleNode("ValorServicos")?.InnerText ?? 
                                              nftsNode.SelectSingleNode("nfts:ValorServicos", namespaceManager)?.InnerText ?? "0",
                                              System.Globalization.CultureInfo.InvariantCulture);
            nfts.ValorDeducoes = decimal.Parse(nftsNode.SelectSingleNode("ValorDeducoes")?.InnerText ?? 
                                              nftsNode.SelectSingleNode("nfts:ValorDeducoes", namespaceManager)?.InnerText ?? "0",
                                              System.Globalization.CultureInfo.InvariantCulture);

            // Códigos
            nfts.CodigoServico = int.Parse(nftsNode.SelectSingleNode("CodigoServico")?.InnerText ?? 
                                          nftsNode.SelectSingleNode("nfts:CodigoServico", namespaceManager)?.InnerText ?? "0");
            
            var codigoSubItem = nftsNode.SelectSingleNode("CodigoSubItem")?.InnerText ?? 
                               nftsNode.SelectSingleNode("nfts:CodigoSubItem", namespaceManager)?.InnerText;
            if (!string.IsNullOrEmpty(codigoSubItem))
            {
                nfts.CodigoSubItem = short.Parse(codigoSubItem);
                nfts.CodigoSubItemSpecified = true;
            }

            // Alíquota
            nfts.AliquotaServicos = decimal.Parse(nftsNode.SelectSingleNode("AliquotaServicos")?.InnerText ?? 
                                                  nftsNode.SelectSingleNode("nfts:AliquotaServicos", namespaceManager)?.InnerText ?? "0",
                                                  System.Globalization.CultureInfo.InvariantCulture);

            // ISS Retido
            nfts.IssRetidoTomador = bool.Parse(nftsNode.SelectSingleNode("ISSRetidoTomador")?.InnerText ?? 
                                              nftsNode.SelectSingleNode("nfts:ISSRetidoTomador", namespaceManager)?.InnerText ?? "false");
            
            var issRetidoInter = nftsNode.SelectSingleNode("ISSRetidoIntermediario")?.InnerText ?? 
                                nftsNode.SelectSingleNode("nfts:ISSRetidoIntermediario", namespaceManager)?.InnerText;
            if (!string.IsNullOrEmpty(issRetidoInter))
            {
                nfts.IssRetidoIntermediario = bool.Parse(issRetidoInter);
                nfts.IssRetidoIntermediarioSpecified = true;
            }

            // Prestador
            var prestadorNode = nftsNode.SelectSingleNode("Prestador") ?? 
                               nftsNode.SelectSingleNode("nfts:Prestador", namespaceManager);
            if (prestadorNode != null)
            {
                nfts.Prestador = new TpPrestador();
                
                var cpfcnpjNode = prestadorNode.SelectSingleNode("CPFCNPJ") ?? 
                                 prestadorNode.SelectSingleNode("nfts:CPFCNPJ", namespaceManager);
                if (cpfcnpjNode != null)
                {
                    nfts.Prestador.Cpfcnpj = new TpCpfcnpj();
                    var cnpj = cpfcnpjNode.SelectSingleNode("CNPJ")?.InnerText ?? 
                              cpfcnpjNode.SelectSingleNode("nfts:CNPJ", namespaceManager)?.InnerText;
                    var cpf = cpfcnpjNode.SelectSingleNode("CPF")?.InnerText ?? 
                             cpfcnpjNode.SelectSingleNode("nfts:CPF", namespaceManager)?.InnerText;
                    
                    if (!string.IsNullOrEmpty(cnpj))
                        nfts.Prestador.Cpfcnpj.Cnpj = cnpj;
                    else if (!string.IsNullOrEmpty(cpf))
                        nfts.Prestador.Cpfcnpj.Cpf = cpf;
                }

                var inscMun = prestadorNode.SelectSingleNode("InscricaoMunicipal")?.InnerText ?? 
                             prestadorNode.SelectSingleNode("nfts:InscricaoMunicipal", namespaceManager)?.InnerText;
                if (!string.IsNullOrEmpty(inscMun))
                {
                    nfts.Prestador.InscricaoMunicipal = long.Parse(inscMun);
                    nfts.Prestador.InscricaoMunicipalSpecified = true;
                }

                nfts.Prestador.RazaoSocialPrestador = prestadorNode.SelectSingleNode("RazaoSocialPrestador")?.InnerText ?? 
                                                      prestadorNode.SelectSingleNode("nfts:RazaoSocialPrestador", namespaceManager)?.InnerText;
                
                // Carregar endereço do prestador
                var enderecoNode = prestadorNode.SelectSingleNode("Endereco") ?? 
                                  prestadorNode.SelectSingleNode("nfts:Endereco", namespaceManager);
                if (enderecoNode != null)
                {
                    var cep = enderecoNode.SelectSingleNode("CEP")?.InnerText ?? 
                             enderecoNode.SelectSingleNode("nfts:CEP", namespaceManager)?.InnerText;
                    
                    nfts.Prestador.Endereco = new TpEndereco
                    {
                        TipoLogradouro = enderecoNode.SelectSingleNode("TipoLogradouro")?.InnerText ?? 
                                        enderecoNode.SelectSingleNode("nfts:TipoLogradouro", namespaceManager)?.InnerText,
                        Logradouro = enderecoNode.SelectSingleNode("Logradouro")?.InnerText ?? 
                                    enderecoNode.SelectSingleNode("nfts:Logradouro", namespaceManager)?.InnerText,
                        NumeroEndereco = enderecoNode.SelectSingleNode("NumeroEndereco")?.InnerText ?? 
                                        enderecoNode.SelectSingleNode("nfts:NumeroEndereco", namespaceManager)?.InnerText,
                        ComplementoEndereco = enderecoNode.SelectSingleNode("ComplementoEndereco")?.InnerText ?? 
                                             enderecoNode.SelectSingleNode("nfts:ComplementoEndereco", namespaceManager)?.InnerText,
                        Bairro = enderecoNode.SelectSingleNode("Bairro")?.InnerText ?? 
                                enderecoNode.SelectSingleNode("nfts:Bairro", namespaceManager)?.InnerText,
                        Cidade = enderecoNode.SelectSingleNode("Cidade")?.InnerText ?? 
                                enderecoNode.SelectSingleNode("nfts:Cidade", namespaceManager)?.InnerText,
                        Uf = enderecoNode.SelectSingleNode("UF")?.InnerText ?? 
                            enderecoNode.SelectSingleNode("nfts:UF", namespaceManager)?.InnerText
                    };
                    
                    if (!string.IsNullOrEmpty(cep))
                    {
                        nfts.Prestador.Endereco.Cep = int.Parse(cep);
                        nfts.Prestador.Endereco.CepSpecified = true;
                    }
                }
                
                nfts.Prestador.Email = prestadorNode.SelectSingleNode("Email")?.InnerText ?? 
                                      prestadorNode.SelectSingleNode("nfts:Email", namespaceManager)?.InnerText;
            }

            // RegimeTributacao
            nfts.RegimeTributacao = short.Parse(nftsNode.SelectSingleNode("RegimeTributacao")?.InnerText ?? 
                                               nftsNode.SelectSingleNode("nfts:RegimeTributacao", namespaceManager)?.InnerText ?? "0");

            // DataPagamento
            var dataPgto = nftsNode.SelectSingleNode("DataPagamento")?.InnerText ?? 
                          nftsNode.SelectSingleNode("nfts:DataPagamento", namespaceManager)?.InnerText;
            if (!string.IsNullOrEmpty(dataPgto))
            {
                nfts.DataPagamento = DateTime.Parse(dataPgto);
                nfts.DataPagamentoSpecified = true;
            }

            // Discriminacao
            nfts.Discriminacao = nftsNode.SelectSingleNode("Discriminacao")?.InnerText ?? 
                                nftsNode.SelectSingleNode("nfts:Discriminacao", namespaceManager)?.InnerText;

            // TipoNFTS
            nfts.TipoNfts = short.Parse(nftsNode.SelectSingleNode("TipoNFTS")?.InnerText ?? 
                                       nftsNode.SelectSingleNode("nfts:TipoNFTS", namespaceManager)?.InnerText ?? "1");

            // Tomador
            var tomadorNode = nftsNode.SelectSingleNode("Tomador") ?? 
                             nftsNode.SelectSingleNode("nfts:Tomador", namespaceManager);
            if (tomadorNode != null)
            {
                nfts.Tomador = new TpTomador();
                
                var cpfcnpjTomNode = tomadorNode.SelectSingleNode("CPFCNPJ") ?? 
                                    tomadorNode.SelectSingleNode("nfts:CPFCNPJ", namespaceManager);
                if (cpfcnpjTomNode != null)
                {
                    nfts.Tomador.Cpfcnpj = new TpCpfcnpj();
                    var cnpj = cpfcnpjTomNode.SelectSingleNode("CNPJ")?.InnerText ?? 
                              cpfcnpjTomNode.SelectSingleNode("nfts:CNPJ", namespaceManager)?.InnerText;
                    var cpf = cpfcnpjTomNode.SelectSingleNode("CPF")?.InnerText ?? 
                             cpfcnpjTomNode.SelectSingleNode("nfts:CPF", namespaceManager)?.InnerText;
                    
                    if (!string.IsNullOrEmpty(cnpj))
                        nfts.Tomador.Cpfcnpj.Cnpj = cnpj;
                    else if (!string.IsNullOrEmpty(cpf))
                        nfts.Tomador.Cpfcnpj.Cpf = cpf;
                }

                nfts.Tomador.RazaoSocial = tomadorNode.SelectSingleNode("RazaoSocial")?.InnerText ?? 
                                          tomadorNode.SelectSingleNode("nfts:RazaoSocial", namespaceManager)?.InnerText;
            }

            Console.WriteLine("XML carregado com sucesso!");
            return nfts;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao carregar XML: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Erro interno: {ex.InnerException.Message}");
            }
            throw;
        }
    }

    /// <summary>
    /// Abre o WinMerge para comparar dois arquivos XML
    /// </summary>
    private static void AbrirWinMerge(string arquivo1, string arquivo2)
    {
        try
        {
            if (!File.Exists(arquivo1))
            {
                Console.WriteLine($"Arquivo não encontrado: {arquivo1}");
                return;
            }

            if (!File.Exists(arquivo2))
            {
                Console.WriteLine($"Arquivo não encontrado: {arquivo2}");
                return;
            }

            // Localização padrão do WinMerge
            string winMergePath = @"C:\Program Files\WinMerge\WinMergeU.exe";
            
            if (!File.Exists(winMergePath))
            {
                // Tentar localização alternativa
                winMergePath = @"C:\Program Files (x86)\WinMerge\WinMergeU.exe";
            }

            if (!File.Exists(winMergePath))
            {
                Console.WriteLine("WinMerge não encontrado. Por favor, instale o WinMerge ou ajuste o caminho no código.");
                return;
            }

            Console.WriteLine("\nAbrindo WinMerge para comparação...");
            var processStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = winMergePath,
                Arguments = $"\"{arquivo1}\" \"{arquivo2}\"",
                UseShellExecute = true
            };
            
            System.Diagnostics.Process.Start(processStartInfo);
            Console.WriteLine("WinMerge aberto com sucesso!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao abrir WinMerge: {ex.Message}");
        }
    }
}
