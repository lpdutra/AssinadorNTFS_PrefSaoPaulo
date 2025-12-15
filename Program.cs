using System.Security.Cryptography.X509Certificates;
using System.Xml.Serialization;
using AssinadorNFTS.Models;

namespace AssinadorNFTS;

class Program
{
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
            
            Console.WriteLine("Classes geradas com sucesso!");
            Console.WriteLine("Use o método ProcessarNFTS para assinar um XML de NFTS.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
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
        Console.WriteLine($"NFTS carregada - Inscrição: {nfts.ChaveDocumento?.InscricaoMunicipal}, Valor: {nfts.ValorServicos:C2}");

        // 3. Assinar a NFTS
        Console.WriteLine("Assinando NFTS...");
        nfts.Assinatura = AssinadorXml.Assinar(certificado, nfts);
        Console.WriteLine($"Assinatura gerada: {Convert.ToBase64String(nfts.Assinatura).Substring(0, 50)}...");

        // 4. Salvar XML assinado
        Console.WriteLine("Salvando XML assinado...");
        string caminhoSaida = Path.ChangeExtension(caminhoXml, ".assinado.xml");
        SalvarXml(nfts, caminhoSaida);
        Console.WriteLine($"XML assinado salvo em: {caminhoSaida}");
    }

    /// <summary>
    /// Cria um exemplo de NFTS para teste
    /// </summary>
    private static TpNfts CriarNFTSExemplo()
    {
        var nfts = new TpNfts
        {
            TipoDocumento = TpTipoDocumentoNfts.Item02,
            ChaveDocumento = new TpChaveDocumento
            {
                InscricaoMunicipal = 12345678,
                NumeroDocumento = 1,
                SerieNfts = "A"
            },
            DataPrestacao = DateTime.Now,
            StatusNfts = TpStatusNfts.N,
            TributacaoNfts = TpTributacaoNfts.T,
            ValorServicos = 1000.00m,
            ValorDeducoes = 0.00m,
            CodigoServico = 1234,
            AliquotaServicos = 0.05m,
            IssRetidoTomador = false,
            RegimeTributacao = 0,
            TipoNfts = 1,
            Prestador = new TpPrestador
            {
                Cpfcnpj = new TpCpfcnpj { Cnpj = "12345678000190" },
                RazaoSocialPrestador = "Empresa Exemplo LTDA"
            },
            Discriminacao = "Serviços de consultoria"
        };

        return nfts;
    }

    /// <summary>
    /// Salva objeto NFTS em arquivo XML
    /// </summary>
    private static void SalvarXml(TpNfts nfts, string caminhoArquivo)
    {
        XmlSerializer serializer = new XmlSerializer(typeof(TpNfts));
        XmlSerializerNamespaces namespaces = new XmlSerializerNamespaces();
        namespaces.Add(string.Empty, string.Empty);

        using (StreamWriter writer = new StreamWriter(caminhoArquivo))
        {
            serializer.Serialize(writer, nfts, namespaces);
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
                                              nftsNode.SelectSingleNode("nfts:ValorServicos", namespaceManager)?.InnerText ?? "0");
            nfts.ValorDeducoes = decimal.Parse(nftsNode.SelectSingleNode("ValorDeducoes")?.InnerText ?? 
                                              nftsNode.SelectSingleNode("nfts:ValorDeducoes", namespaceManager)?.InnerText ?? "0");

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
                                                  nftsNode.SelectSingleNode("nfts:AliquotaServicos", namespaceManager)?.InnerText ?? "0");

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
