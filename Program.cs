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

        // 2. Carregar ou criar objeto NFTS
        Console.WriteLine("Criando objeto NFTS...");
        TpNfts nfts = CriarNFTSExemplo();

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
    /// Carrega objeto NFTS de arquivo XML
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
