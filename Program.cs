using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using AssinadorNFTS.Models;

namespace AssinadorNFTS;

class Program
{

    private static string xmlExemplo = "<tpNFTS><TipoDocumento>01</TipoDocumento><ChaveDocumento><InscricaoMunicipal>10259627</InscricaoMunicipal><SerieNFTS>12345</SerieNFTS><NumeroDocumento>12345</NumeroDocumento></ChaveDocumento><DataPrestacao>2025-01-15</DataPrestacao><StatusNFTS>N</StatusNFTS><TributacaoNFTS>T</TributacaoNFTS><ValorServicos>1500.31</ValorServicos><ValorDeducoes>1300.31</ValorDeducoes><CodigoServico>1001</CodigoServico><AliquotaServicos>0.05</AliquotaServicos><ISSRetidoTomador>false</ISSRetidoTomador><Prestador><CPFCNPJ><CNPJ>45443039000111</CNPJ></CPFCNPJ><RazaoSocialPrestador>EMPRESAFICTICIALTDA</RazaoSocialPrestador><Endereco><TipoLogradouro>RUA</TipoLogradouro><Logradouro>TESTEPRESTADOR</Logradouro><NumeroEndereco>1001</NumeroEndereco><ComplementoEndereco>SALA</ComplementoEndereco><Bairro>CENTRO</Bairro><Cidade>3550308</Cidade><UF>SP</UF><CEP>95082200</CEP></Endereco></Prestador><RegimeTributacao>5</RegimeTributacao><TipoNFTS>1</TipoNFTS></tpNFTS>";

    private static string DEBUG_DIR = "D:\\Workspace\\FESP\\Projeto_NTFS\\processamento\\nfts_debug";
    
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Assinador de NFTS - Prefeitura de São Paulo ===");
        Console.WriteLine();

        string caminhoCertificado = "D:\\Workspace\\FESP\\Projeto_NTFS\\processamento\\Fesp cert A1.pfx";
        string senhaCertificado = "Unimed2025";

        try
        {
            // === EXEMPLO 1: Gerar múltiplos XMLs com diferentes códigos de serviço, assinar e enviar ===
            // string caminhoXml = "D:\\Workspace\\FESP\\Projeto_NTFS\\processamento\\nfts_minimum_data-prest-cpf.xml";
            //await MultipleTryRequests.GerarArquivosERealizarTentativas(caminhoXml, caminhoCertificado, senhaCertificado);

            string caminhoPastaRequests = "D:\\Workspace\\FESP\\Projeto_NTFS\\multiple_tries";
            await MultipleTryRequests.FazerRequisicoesDosRequestsExistentes(caminhoPastaRequests, caminhoCertificado, senhaCertificado);
            
            // string caminhoXml = "D:\\Workspace\\FESP\\Projeto_NTFS\\processamento\\nfts_minimum_data-prest-cpf.xml";
            string caminhoXml = "D:\\Workspace\\FESP\\Projeto_NTFS\\processamento\\LOTE_421.xml";
            // RealizarAssinaturasXML.ProcessarNFTS(caminhoXml, caminhoCertificado, senhaCertificado, true);
            
            // Enviar para o servidor
            // string caminhoRequestAssinado = "D:\\Workspace\\FESP\\Projeto_NTFS\\processamento\\nfts_minimum_data-prest-cpf.assinado.xml";
            string caminhoRequestAssinado = "D:\\Workspace\\FESP\\Projeto_NTFS\\processamento\\LOTE_421.assinado.xml";
            string caminhoResponse = "D:\\Workspace\\FESP\\Projeto_NTFS\\processamento\\response.xml";
            // await SoapClient.CallTesteEnvioLoteNFTS(caminhoRequestAssinado, caminhoCertificado, senhaCertificado, caminhoResponse);

            // === EXEMPLO 2: Recalcular apenas a assinatura XMLDSig de um XML existente ===
            // string caminhoXMLReassinar = "D:\\Workspace\\FESP\\Projeto_NTFS\\processamento\\nfts-recalcular-signature.xml";
            // RecalcularAssinaturaXmlDSigByPathArquivo.DoProcess(caminhoXMLReassinar, caminhoCertificado, senhaCertificado);

            // RealizarAssinaturasXML.GetValorTagAssinaturaNFTS(xmlExemplo, caminhoCertificado, senhaCertificado);

            // === EXEMPLO 3: Validar assinatura de um XML existente ===
            // string caminhoXmlParaValidar = "D:\\Workspace\\FESP\\Projeto_NTFS\\processamento\\pref_salvador\\TestePedidoEnvioLoteNFTS_Envio-salvador.xml";
            // bool assinaturaValida = ValidacaoArquivoXML.ValidarAssinaturaNFTS(caminhoXmlParaValidar);
            // if (assinaturaValida)
            //     Console.WriteLine("✅ O arquivo possui assinatura válida!");
            // else
            //     Console.WriteLine("❌ O arquivo NÃO possui assinatura válida!");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
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

}