using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Serialization;
using AssinadorNFTS.Models;

namespace AssinadorNFTS;

class Program
{

    private static string xmlExemplo = "<tpNFTS><TipoDocumento>2</TipoDocumento><ChaveDocumento><InscricaoMunicipal>10259627</InscricaoMunicipal><SerieNFTS>12345</SerieNFTS><NumeroDocumento>12345</NumeroDocumento></ChaveDocumento><DataPrestacao>2025-01-15</DataPrestacao><StatusNFTS>N</StatusNFTS><TributacaoNFTS>T</TributacaoNFTS><ValorServicos>1500.30</ValorServicos><ValorDeducoes>1300.30</ValorDeducoes><CodigoServico>1001</CodigoServico><CodigoSubItem>1</CodigoSubItem><AliquotaServicos>0.03</AliquotaServicos><ISSRetidoTomador>false</ISSRetidoTomador><ISSRetidoIntermediario>false</ISSRetidoIntermediario><Prestador><CPFCNPJ><CNPJ>43643139000166</CNPJ></CPFCNPJ><InscricaoMunicipal>10259627</InscricaoMunicipal><RazaoSocialPrestador>EMPRESAFICTICIALTDA</RazaoSocialPrestador><Endereco><TipoLogradouro>RUA</TipoLogradouro><Logradouro>TESTEPRESTADOR</Logradouro><NumeroEndereco>100</NumeroEndereco><ComplementoEndereco>SALA10</ComplementoEndereco><Bairro>CENTRO</Bairro><Cidade>3550308</Cidade><UF>SP</UF><CEP>95082200</CEP></Endereco><Email>contato@yahoo.com</Email></Prestador><RegimeTributacao>5</RegimeTributacao><DataPagamento>2025-01-20</DataPagamento><Discriminacao>TESTE</Discriminacao><TipoNFTS>2</TipoNFTS><Tomador><CPFCNPJ><CNPJ>43643139000166</CNPJ></CPFCNPJ><RazaoSocial>CLIENTEFICTICIO</RazaoSocial></Tomador></tpNFTS>";

    private static string DEBUG_DIR = "D:\\Workspace\\FESP\\Projeto_NTFS\\processamento\\nfts_debug";
    
    static void Main(string[] args)
    {
        Console.WriteLine("=== Assinador de NFTS - Prefeitura de São Paulo ===");
        Console.WriteLine();

        try
        {
            // Exemplo de uso (descomente e adapte conforme necessário)
            string caminhoXml = "D:\\Workspace\\FESP\\Projeto_NTFS\\processamento\\nfts.xml";
            string caminhoCertificado = "D:\\Workspace\\FESP\\Projeto_NTFS\\processamento\\Fesp cert A1.pfx";
            string senhaCertificado = "Unimed2025";
            
            ProcessarNFTS(caminhoXml, caminhoCertificado, senhaCertificado);
            ProcessarNFTSDeString(xmlExemplo, caminhoCertificado, senhaCertificado);

            // ProcessarNFTSDeString(xmlExemplo.Replace(
            //     "<DataPrestacao>2025-01-15</DataPrestacao>", "<DataPrestacao>20250115</DataPrestacao>"
            //     ), caminhoCertificado, senhaCertificado);
            
            Console.WriteLine("Classes geradas com sucesso!");
            Console.WriteLine("Use o método ProcessarNFTS para assinar um XML de NFTS.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    public static void ProcessarNFTSDeString(string xmlString, string caminhoCertificado, string senhaCertificado)
    {
        // 1. Carregar certificado digital
        Console.WriteLine("Carregando certificado digital...");
        X509Certificate2 certificado = new X509Certificate2(caminhoCertificado, senhaCertificado);
        Console.WriteLine($"Certificado carregado: {certificado.Subject}");

        byte[] assinatura = AssinadorXml.AssinarXmlString(certificado, xmlString);
        string assinaturaBase64 = Convert.ToBase64String(assinatura);
        Console.WriteLine("Assinatura=" + assinaturaBase64);


    }

    /// <summary>
    /// Processa e assina uma NFTS
    /// </summary>
    /// <param name="caminhoXml">Caminho do arquivo XML da NFTS</param>
    /// <param name="caminhoCertificado">Caminho do certificado digital (.pfx)</param>
    /// <param name="senhaCertificado">Senha do certificado</param>
    public static void ProcessarNFTS(string caminhoXml, string caminhoCertificado, string senhaCertificado)
    {
        // 1. Carregar certificado digital
        Console.WriteLine("Carregando certificado digital...");
        X509Certificate2 certificado = new X509Certificate2(caminhoCertificado, senhaCertificado);
        Console.WriteLine($"Certificado carregado: {certificado.Subject}");

        // 2. Carregar NFTS do arquivo XML
        Console.WriteLine($"Carregando NFTS do arquivo: {caminhoXml}");
        TpNfts nfts = CarregarNFTSDoXml(caminhoXml);
        CabecalhoLote? cabecalho = CarregarCabecalhoDoXml(caminhoXml);
        //TpNfts nfts = CriarNFTSExemploMinimo(); // Usar exemplo para teste
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
        string caminhoSaida = Path.Combine(Path.GetDirectoryName(caminhoXml)!, "request.assinado.xml");
        SalvarXmlSoap(nfts, caminhoSaida, certificado, cabecalho);
        Console.WriteLine($"XML assinado salvo em: {caminhoSaida}");
    }

    /// <summary>
    /// Cria um exemplo de NFTS para teste
    /// </summary>
    private static TpNfts CriarNFTSExemplo()
    {
        var nfts = new TpNfts
        {
            TipoDocumento = TpTipoDocumentoNfts.Item01,
            ChaveDocumento = new TpChaveDocumento
            {
                InscricaoMunicipal = 12345678,
                NumeroDocumento = 1,
                NumeroDocumentoSpecified = true,  // Necessário para incluir o campo na serialização
                SerieNfts = "A"
            },
            DataPrestacao = DateTime.Now,
            StatusNfts = TpStatusNfts.N,
            TributacaoNfts = TpTributacaoNfts.T,
            ValorServicos = 0.01m,  // 1 centavo para minimizar riscos
            ValorDeducoes = 0.01m,  // 1 centavo
            CodigoServico = 1234,
            AliquotaServicos = 0.01m,  // 1%
            IssRetidoTomador = false,
            RegimeTributacao = 0,  // 0=Normal, 4=Simples Nacional, 5=MEI
            TipoNfts = 1,
            Prestador = new TpPrestador
            {
                Cpfcnpj = new TpCpfcnpj { Cpf = "12345678909" },  // CPF válido para teste
                RazaoSocialPrestador = "PRESTADORFICTICIO"  // Sem espaços
            },
            Discriminacao = "TESTE"  // Simples, sem espaços
        };

        return nfts;
    }

    /// <summary>
    /// Cria um exemplo de NFTS mínimo para teste (apenas campos obrigatórios)
    /// </summary>
    private static TpNfts CriarNFTSExemploMinimo()
    {
        var nfts = new TpNfts
        {
            // Obrigatório: TipoDocumento
            TipoDocumento = TpTipoDocumentoNfts.Item01,
            
            // Obrigatório: ChaveDocumento
            ChaveDocumento = new TpChaveDocumento
            {
                InscricaoMunicipal = 12345678,
                NumeroDocumento = 1,
                NumeroDocumentoSpecified = true,
                SerieNfts = "A"
            },
            
            // Obrigatório: DataPrestacao
            DataPrestacao = DateTime.Now,
            
            // Obrigatório: StatusNFTS
            StatusNfts = TpStatusNfts.N,
            
            // Obrigatório: TributacaoNFTS
            TributacaoNfts = TpTributacaoNfts.T,
            
            // Obrigatório: ValorServicos
            ValorServicos = 0.01m,
            
            // Obrigatório: ValorDeducoes
            ValorDeducoes = 0.00m,
            
            // Obrigatório: CodigoServico
            CodigoServico = 1234,
            
            // Obrigatório: AliquotaServicos
            AliquotaServicos = 0.01m,
            
            // Obrigatório: ISSRetidoTomador
            IssRetidoTomador = false,
            
            // Obrigatório: Prestador
            Prestador = new TpPrestador
            {
                Cpfcnpj = new TpCpfcnpj { Cpf = "12345678909" },
                RazaoSocialPrestador = "PRESTADOR"
            },
            
            // Obrigatório: RegimeTributacao
            RegimeTributacao = 0,
            
            // Obrigatório: TipoNFTS
            TipoNfts = 1
            
            // Campos opcionais removidos:
            // - Discriminacao (se não incluída, tag não será gerada)
            // - CodigoSubItem
            // - ISSRetidoIntermediario
            // - DataPagamento
            // - Tomador
        };

        return nfts;
    }

    /// <summary>
    /// Salva objeto NFTS em arquivo XML no formato PedidoEnvioLoteNFTS
    /// </summary>
    private static void SalvarXml(TpNfts nfts, string caminhoArquivo)
    {
        // Gerar apenas o conteúdo do PedidoEnvioLoteNFTS
        string conteudo = GerarPedidoEnvioLoteNFTS(nfts);
        File.WriteAllText(caminhoArquivo, conteudo, System.Text.Encoding.UTF8);
    }

    /// <summary>
    /// Salva objeto NFTS em arquivo XML no formato SOAP TesteEnvioLoteNFTSRequest com CDATA
    /// </summary>
    private static void SalvarXmlSoap(TpNfts nfts, string caminhoArquivo, X509Certificate2 certificado, CabecalhoLote? cabecalho = null)
    {
        // Gerar o PedidoEnvioLoteNFTS
        string pedidoXml = GerarPedidoEnvioLoteNFTS(nfts, cabecalho);
        
        // Adicionar quebra de linha após a declaração XML (primeira ocorrência apenas)
        int firstDeclarationEnd = pedidoXml.IndexOf("?>");
        if (firstDeclarationEnd > 0)
        {
            pedidoXml = pedidoXml.Substring(0, firstDeclarationEnd + 2) + "\n" + pedidoXml.Substring(firstDeclarationEnd + 2);
        }
        
        // Criar envelope SOAP
        using (var memoryStream = new MemoryStream())
        {
            var settings = new System.Xml.XmlWriterSettings
            {
                OmitXmlDeclaration = false,
                Indent = true,
                IndentChars = "  ",
                Encoding = System.Text.Encoding.UTF8
            };

            using (var xmlWriter = System.Xml.XmlWriter.Create(memoryStream, settings))
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

    // Classe para armazenar dados do cabeçalho
    private class CabecalhoLote
    {
        public DateTime DtInicio { get; set; }
        public DateTime DtFim { get; set; }
        public int QtdNFTS { get; set; }
        public decimal ValorTotalServicos { get; set; }
    }

    /// <summary>
    /// Gera o XML do PedidoEnvioLoteNFTS com a NFTS assinada
    /// </summary>
    private static string GerarPedidoEnvioLoteNFTS(TpNfts nfts, CabecalhoLote? cabecalho = null)
    {
        using (var memoryStream = new MemoryStream())
        {
            using (var xmlWriter = System.Xml.XmlWriter.Create(memoryStream, new System.Xml.XmlWriterSettings
            {
                OmitXmlDeclaration = false,  // Incluir declaração XML
                Indent = false,
                Encoding = new System.Text.UTF8Encoding(false) // false = sem BOM
            }))
        {
            xmlWriter.WriteStartDocument();
            xmlWriter.WriteStartElement("PedidoEnvioLoteNFTS", "http://www.prefeitura.sp.gov.br/nfts");
            xmlWriter.WriteAttributeString("xmlns", "xsd", null, "http://www.w3.org/2001/XMLSchema");
            xmlWriter.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
            
            // Cabeçalho
            xmlWriter.WriteStartElement("Cabecalho", "");
            xmlWriter.WriteAttributeString("Versao", "1");
            
            xmlWriter.WriteStartElement("Remetente");
            xmlWriter.WriteStartElement("CPFCNPJ");
            // Usar CPF ou CNPJ dependendo do que está preenchido
            if (!string.IsNullOrEmpty(nfts.Prestador?.Cpfcnpj?.Cpf))
                xmlWriter.WriteElementString("CPF", nfts.Prestador.Cpfcnpj.Cpf);
            else
                xmlWriter.WriteElementString("CNPJ", nfts.Prestador?.Cpfcnpj?.Cnpj ?? "");
            xmlWriter.WriteEndElement(); // CPFCNPJ
            xmlWriter.WriteEndElement(); // Remetente
            
            xmlWriter.WriteElementString("transacao", "true");
            
            // Usar datas do cabeçalho se fornecido, senão usar datas dinâmicas
            if (cabecalho != null)
            {
                xmlWriter.WriteElementString("dtInicio", cabecalho.DtInicio.ToString("yyyy-MM-dd"));
                xmlWriter.WriteElementString("dtFim", cabecalho.DtFim.ToString("yyyy-MM-dd"));
                xmlWriter.WriteElementString("QtdNFTS", cabecalho.QtdNFTS.ToString());
                xmlWriter.WriteElementString("ValorTotalServicos", cabecalho.ValorTotalServicos.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
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
            
            xmlWriter.WriteElementString("AliquotaServicos", nfts.AliquotaServicos.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
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
                    xmlWriter.WriteElementString("TipoLogradouro", nfts.Prestador.Endereco.TipoLogradouro ?? "");
                    xmlWriter.WriteElementString("Logradouro", nfts.Prestador.Endereco.Logradouro ?? "");
                    xmlWriter.WriteElementString("NumeroEndereco", nfts.Prestador.Endereco.NumeroEndereco ?? "");
                    if (!string.IsNullOrEmpty(nfts.Prestador.Endereco.ComplementoEndereco))
                        xmlWriter.WriteElementString("ComplementoEndereco", nfts.Prestador.Endereco.ComplementoEndereco);
                    xmlWriter.WriteElementString("Bairro", nfts.Prestador.Endereco.Bairro ?? "");
                    xmlWriter.WriteElementString("Cidade", nfts.Prestador.Endereco.Cidade ?? "");
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
            
            // Adicionar assinatura digital XML (ds:Signature)
            xmlWriter.WriteRaw(GerarAssinaturaXml());
            
            xmlWriter.WriteEndElement(); // PedidoEnvioLoteNFTS
            xmlWriter.WriteEndDocument();
            }
            
            // Converter o MemoryStream para string UTF-8 sem BOM
            byte[] xmlBytes = memoryStream.ToArray();
            string xmlString = new System.Text.UTF8Encoding(false).GetString(xmlBytes);
            

            
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
            
            return xmlString;
        }
    }

    /// <summary>
    /// Gera a estrutura de assinatura XML digital (placeholder)
    /// </summary>
    private static string GerarAssinaturaXml()
    {
        return @"<ds:Signature xmlns:ds=""http://www.w3.org/2000/09/xmldsig#"">
<ds:SignedInfo>
<ds:CanonicalizationMethod Algorithm=""http://www.w3.org/2001/10/xml-exc-c14n#""/>
<ds:SignatureMethod Algorithm=""http://www.w3.org/2000/09/xmldsig#rsa-sha1""/>
<ds:Reference URI="""">
<ds:Transforms>
<ds:Transform Algorithm=""http://www.w3.org/2000/09/xmldsig#enveloped-signature""/>
</ds:Transforms>
<ds:DigestMethod Algorithm=""http://www.w3.org/2000/09/xmldsig#sha1""/>
<ds:DigestValue></ds:DigestValue>
</ds:Reference>
</ds:SignedInfo>
<ds:SignatureValue></ds:SignatureValue>
<ds:KeyInfo>
<ds:X509Data>
<ds:X509Certificate></ds:X509Certificate>
</ds:X509Data>
</ds:KeyInfo>
</ds:Signature>";
    }

    /// <summary>
    /// Carrega o cabeçalho do lote de um arquivo XML (PedidoEnvioLoteNFTS)
    /// </summary>
    private static CabecalhoLote? CarregarCabecalhoDoXml(string caminhoArquivo)
    {
        try
        {
            var xmlDoc = new System.Xml.XmlDocument();
            xmlDoc.Load(caminhoArquivo);

            var namespaceManager = new System.Xml.XmlNamespaceManager(xmlDoc.NameTable);
            namespaceManager.AddNamespace("nfts", "http://www.prefeitura.sp.gov.br/nfts");

            // Buscar o nó Cabecalho
            var cabecalhoNode = xmlDoc.SelectSingleNode("//nfts:Cabecalho", namespaceManager) ?? 
                               xmlDoc.SelectSingleNode("//Cabecalho");

            if (cabecalhoNode == null)
                return null;

            var cabecalho = new CabecalhoLote();

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
                cabecalho.QtdNFTS = qtd;

            // Ler ValorTotalServicos
            var valorText = cabecalhoNode.SelectSingleNode("ValorTotalServicos")?.InnerText ?? 
                           cabecalhoNode.SelectSingleNode("nfts:ValorTotalServicos", namespaceManager)?.InnerText;
            if (decimal.TryParse(valorText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal valor))
                cabecalho.ValorTotalServicos = valor;

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
            var xmlDoc = new System.Xml.XmlDocument();
            xmlDoc.Load(caminhoArquivo);

            // Criar namespace manager para lidar com o namespace do XML
            var namespaceManager = new System.Xml.XmlNamespaceManager(xmlDoc.NameTable);
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
    /// Carrega objeto NFTS de arquivo XML (formato simples, apenas tag NFTS)
    /// </summary>
    private static TpNfts CarregarXml(string caminhoArquivo)
    {
        XmlSerializer serializer = new XmlSerializer(typeof(TpNfts));
        using (StreamReader reader = new StreamReader(caminhoArquivo))
        {
            return (TpNfts)serializer.Deserialize(reader)!;
        }
    }
}
