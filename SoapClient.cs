using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace AssinadorNFTS;

/// <summary>
/// Cliente SOAP para envio de NFTS à Prefeitura de São Paulo
/// </summary>
public class SoapClient
{
    private readonly string _endpoint;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Construtor do cliente SOAP
    /// </summary>
    /// <param name="certificado">Certificado digital X509 para autenticação SSL</param>
    /// <param name="endpoint">URL do endpoint SOAP (padrão: produção)</param>
    public SoapClient(X509Certificate2 certificado, string? endpoint = null)
    {
        _endpoint = endpoint ?? "https://nfe.prefeitura.sp.gov.br/ws/LoteNFTS.asmx";
        
        // Criar HttpClientHandler com certificado client SSL
        var handler = new HttpClientHandler();
        handler.ClientCertificates.Add(certificado);
        handler.AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate;
        
        // Opcional: aceitar certificados SSL auto-assinados (apenas para testes em homologação)
        // handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
        
        _httpClient = new HttpClient(handler);
        // SOAPAction DEVE ter aspas duplas ao redor do valor!
        _httpClient.DefaultRequestHeaders.Add("SOAPAction", "\"http://www.prefeitura.sp.gov.br/nfts/ws/TesteEnvioLoteNFTS\"");
        _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip,deflate");
        _httpClient.DefaultRequestHeaders.ConnectionClose = false; // Keep-Alive
        _httpClient.Timeout = TimeSpan.FromSeconds(120); // Timeout de 2 minutos
    }

    /// <summary>
    /// Envia lote de NFTS para a Prefeitura
    /// </summary>
    /// <param name="xmlFilePath">Caminho completo do arquivo XML SOAP a ser enviado</param>
    /// <returns>Resposta XML do servidor</returns>
    public async Task<string> EnviarLoteNFTS(string xmlFilePath)
    {
        try
        {
            Console.WriteLine($"\n=== Enviando NFTS para {_endpoint} ===\n");

            // Ler o arquivo XML
            if (!File.Exists(xmlFilePath))
            {
                throw new FileNotFoundException($"Arquivo não encontrado: {xmlFilePath}");
            }

            string soapEnvelope = File.ReadAllText(xmlFilePath, Encoding.UTF8);
            Console.WriteLine($"Arquivo carregado: {xmlFilePath}");
            Console.WriteLine($"Tamanho: {soapEnvelope.Length} caracteres\n");

            // Validar se é um XML válido
            try
            {
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(soapEnvelope);
                Console.WriteLine("✓ XML validado com sucesso");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"XML inválido: {ex.Message}");
            }

            // Criar requisição HTTP com Content-Type EXATO do SoapUI
            var content = new StringContent(soapEnvelope, Encoding.UTF8);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/xml");
            content.Headers.ContentType.CharSet = "UTF-8"; // Maiúsculo como no SoapUI

            // Logar detalhes da requisição HTTP
            Console.WriteLine("\n=== DETALHES DA REQUISIÇÃO HTTP ===");
            Console.WriteLine($"Method: POST");
            Console.WriteLine($"URL: {_endpoint}");
            Console.WriteLine($"Content-Type: {content.Headers.ContentType}");
            Console.WriteLine($"Content-Length: {content.Headers.ContentLength}");
            Console.WriteLine("\nRequest Headers:");
            foreach (var header in _httpClient.DefaultRequestHeaders)
            {
                Console.WriteLine($"  {header.Key}: {string.Join(", ", header.Value)}");
            }
            Console.WriteLine("===================================\n");

            Console.WriteLine("Enviando requisição...");
            var response = await _httpClient.PostAsync(_endpoint, content);

            Console.WriteLine($"Status: {(int)response.StatusCode} {response.StatusCode}");

            // Ler resposta
            string responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"\n❌ Erro HTTP: {response.StatusCode}");
                Console.WriteLine($"Resposta:\n{responseContent}");
                return responseContent;
            }

            Console.WriteLine("\n✓ Requisição enviada com sucesso!\n");

            // Extrair e formatar o retorno
            return FormatarResposta(responseContent);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Erro ao enviar NFTS: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Formata a resposta SOAP para exibição no console
    /// </summary>
    private string FormatarResposta(string responseXml)
    {
        try
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(responseXml);

            // Extrair o RetornoXML do CDATA
            var namespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
            namespaceManager.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");
            namespaceManager.AddNamespace("ns", "http://www.prefeitura.sp.gov.br/nfts");

            var retornoNode = xmlDoc.SelectSingleNode("//ns:RetornoXML", namespaceManager);
            
            if (retornoNode != null)
            {
                string retornoXml = retornoNode.InnerText;
                
                // Formatar o XML de retorno
                var retornoDoc = new XmlDocument();
                retornoDoc.LoadXml(retornoXml);

                using (var stringWriter = new StringWriter())
                using (var xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings
                {
                    Indent = true,
                    IndentChars = "  ",
                    OmitXmlDeclaration = false
                }))
                {
                    retornoDoc.Save(xmlWriter);
                    string formattedXml = stringWriter.ToString();

                    // Analisar se teve sucesso ou erro
                    AnalisarRetorno(retornoDoc);

                    return formattedXml;
                }
            }

            return responseXml;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Aviso: Não foi possível formatar a resposta: {ex.Message}");
            return responseXml;
        }
    }

    /// <summary>
    /// Analisa o XML de retorno e exibe informações resumidas
    /// </summary>
    private void AnalisarRetorno(XmlDocument retornoDoc)
    {
        try
        {
            var namespaceManager = new XmlNamespaceManager(retornoDoc.NameTable);
            namespaceManager.AddNamespace("ns", "http://www.prefeitura.sp.gov.br/nfts");

            // Verificar sucesso
            var sucessoNode = retornoDoc.SelectSingleNode("//ns:Sucesso", namespaceManager);
            bool sucesso = sucessoNode?.InnerText.ToLower() == "true";

            Console.WriteLine("=== RESULTADO DO ENVIO ===\n");
            Console.WriteLine($"Sucesso: {(sucesso ? "✓ SIM" : "✗ NÃO")}");

            // Informações do lote
            var numeroLoteNode = retornoDoc.SelectSingleNode("//ns:NumeroLote", namespaceManager);
            var qtdeNftsNode = retornoDoc.SelectSingleNode("//ns:QtdeNFTSProcessadas", namespaceManager);
            var valorTotalNode = retornoDoc.SelectSingleNode("//ns:ValorTotalServicos", namespaceManager);
            var tempoNode = retornoDoc.SelectSingleNode("//ns:TempoProcessamento", namespaceManager);

            if (numeroLoteNode != null)
                Console.WriteLine($"Número do Lote: {numeroLoteNode.InnerText}");
            if (qtdeNftsNode != null)
                Console.WriteLine($"NFTS Processadas: {qtdeNftsNode.InnerText}");
            if (valorTotalNode != null)
                Console.WriteLine($"Valor Total: R$ {valorTotalNode.InnerText}");
            if (tempoNode != null)
                Console.WriteLine($"Tempo de Processamento: {tempoNode.InnerText}s");

            // Listar erros (se houver)
            var erros = retornoDoc.SelectNodes("//ns:Erro", namespaceManager);
            if (erros != null && erros.Count > 0)
            {
                Console.WriteLine($"\n❌ Erros encontrados ({erros.Count}):\n");
                
                foreach (XmlNode erro in erros)
                {
                    var codigoNode = erro.SelectSingleNode("ns:Codigo", namespaceManager);
                    var descricaoNode = erro.SelectSingleNode("ns:Descricao", namespaceManager);
                    
                    string codigo = codigoNode?.InnerText ?? "?";
                    string descricao = descricaoNode?.InnerText ?? "Descrição não disponível";
                    
                    Console.WriteLine($"  [{codigo}] {descricao}");

                    // Identificação do documento (se houver)
                    var posicaoNode = erro.SelectSingleNode(".//ns:Posicao", namespaceManager);
                    var inscricaoNode = erro.SelectSingleNode(".//ns:InscricaoMunicipal", namespaceManager);
                    var serieNode = erro.SelectSingleNode(".//ns:SerieNFTS", namespaceManager);
                    var numeroNode = erro.SelectSingleNode(".//ns:NumeroDocumento", namespaceManager);

                    if (posicaoNode != null || inscricaoNode != null)
                    {
                        Console.Write("    Documento: ");
                        if (posicaoNode != null)
                            Console.Write($"Posição {posicaoNode.InnerText}");
                        if (inscricaoNode != null)
                            Console.Write($" - IM: {inscricaoNode.InnerText}");
                        if (serieNode != null)
                            Console.Write($" - Série: {serieNode.InnerText}");
                        if (numeroNode != null)
                            Console.Write($" - Número: {numeroNode.InnerText}");
                        Console.WriteLine();
                    }
                    Console.WriteLine();
                }
            }
            else if (sucesso)
            {
                Console.WriteLine("\n✓ Lote processado com sucesso, sem erros!");
            }

            Console.WriteLine("\n=== XML COMPLETO ===\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Aviso: Não foi possível analisar o retorno: {ex.Message}");
        }
    }

    /// <summary>
    /// Envia o XML assinado para o servidor da Prefeitura
    /// </summary>
    /// <param name="caminhoXmlAssinado">Caminho do arquivo request.assinado.xml</param>
    private static async Task CallTesteEnvioLoteNFTS(string caminhoXmlAssinado, string caminhoCertificado, string senhaCertificado)
    {
        try
        {
            // Carregar certificado para autenticação SSL
            var certificado = new X509Certificate2(caminhoCertificado, senhaCertificado);
            
            var soapClient = new SoapClient(certificado);
            string resposta = await soapClient.EnviarLoteNFTS(caminhoXmlAssinado);
            
            Console.WriteLine(resposta);
            
            // Salvar resposta em arquivo
            string caminhoResposta = Path.Combine(
                Path.GetDirectoryName(caminhoXmlAssinado)!,
                "response.xml"
            );
            File.WriteAllText(caminhoResposta, resposta, Encoding.UTF8);
            Console.WriteLine($"\nResposta salva em: {caminhoResposta}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Erro ao enviar para o servidor: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Detalhes: {ex.InnerException.Message}");
            }
        }
    }
}
