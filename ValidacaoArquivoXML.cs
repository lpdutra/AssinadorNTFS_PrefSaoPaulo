using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using AssinadorNFTS.Models;

namespace AssinadorNFTS;

/// <summary>
/// Classe responsável por validar assinaturas de arquivos XML NFTS
/// </summary>
public static class ValidacaoArquivoXML
{
    /// <summary>
    /// Valida a tag Assinatura de uma NFTS em um arquivo XML
    /// </summary>
    /// <param name="caminhoXml">Caminho completo do arquivo XML a ser validado</param>
    /// <returns>True se a assinatura é válida, False caso contrário</returns>
    public static bool ValidarAssinaturaNFTS(string caminhoXml)
    {
        try
        {
            Console.WriteLine($"\n=== VALIDANDO ASSINATURA NFTS ===");
            Console.WriteLine($"Arquivo: {caminhoXml}");
            
            if (!File.Exists(caminhoXml))
            {
                Console.WriteLine("❌ Arquivo não encontrado!");
                return false;
            }

            // 1. Carregar o XML
            var xmlDoc = new XmlDocument();
            xmlDoc.PreserveWhitespace = true;
            xmlDoc.Load(caminhoXml);

            // 2. Buscar a tag NFTS
            var namespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
            namespaceManager.AddNamespace("nfts", "http://www.prefeitura.sp.gov.br/nfts");
            namespaceManager.AddNamespace("salvador", "https://nfse.salvador.ba.gov.br/nfts");
            namespaceManager.AddNamespace("ds", "http://www.w3.org/2000/09/xmldsig#");

            var nftsNode = xmlDoc.SelectSingleNode("//nfts:NFTS", namespaceManager) ??
                          xmlDoc.SelectSingleNode("//salvador:NFTS", namespaceManager) ??
                          xmlDoc.SelectSingleNode("//NFTS");

            if (nftsNode == null)
            {
                Console.WriteLine("❌ Tag <NFTS> não encontrada no XML!");
                return false;
            }

            // 3. Extrair a assinatura atual
            var assinaturaNode = nftsNode.SelectSingleNode("Assinatura") ?? 
                                nftsNode.SelectSingleNode("nfts:Assinatura", namespaceManager) ??
                                nftsNode.SelectSingleNode("salvador:Assinatura", namespaceManager);

            if (assinaturaNode == null)
            {
                Console.WriteLine("❌ Tag <Assinatura> não encontrada!");
                return false;
            }

            string assinaturaBase64 = assinaturaNode.InnerText.Trim();
            byte[] assinaturaExistente = Convert.FromBase64String(assinaturaBase64);
            Console.WriteLine($"✓ Assinatura extraída: {assinaturaBase64.Substring(0, Math.Min(50, assinaturaBase64.Length))}...");

            // 4. Extrair o certificado X509
            var signatureNode = xmlDoc.SelectSingleNode("//ds:Signature", namespaceManager) ??
                               xmlDoc.SelectSingleNode("//Signature");

            if (signatureNode == null)
            {
                Console.WriteLine("⚠ Tag <Signature> (XMLDSig) não encontrada - continuando validação apenas da NFTS");
            }

            X509Certificate2? certificado = null;
            
            if (signatureNode != null)
            {
                var x509CertNode = signatureNode.SelectSingleNode(".//ds:X509Certificate", namespaceManager) ??
                                  signatureNode.SelectSingleNode(".//X509Certificate");

                if (x509CertNode != null)
                {
                    string certBase64 = x509CertNode.InnerText.Trim().Replace("\n", "").Replace("\r", "").Replace(" ", "");
                    byte[] certBytes = Convert.FromBase64String(certBase64);
                    certificado = new X509Certificate2(certBytes);
                    
                    Console.WriteLine($"✓ Certificado extraído:");
                    Console.WriteLine($"  Subject: {certificado.Subject}");
                    Console.WriteLine($"  Válido até: {certificado.NotAfter:dd/MM/yyyy HH:mm:ss}");
                    
                    if (DateTime.Now > certificado.NotAfter)
                    {
                        Console.WriteLine($"  ⚠ CERTIFICADO EXPIRADO!");
                    }
                }
            }

            // 5. Reconstruir o objeto NFTS (sem a tag Assinatura)
            var nftsSemAssinatura = nftsNode.Clone();
            var assinaturaParaRemover = nftsSemAssinatura.SelectSingleNode("Assinatura") ?? 
                                        nftsSemAssinatura.SelectSingleNode("nfts:Assinatura", namespaceManager) ??
                                        nftsSemAssinatura.SelectSingleNode("salvador:Assinatura", namespaceManager);
            
            if (assinaturaParaRemover != null)
            {
                assinaturaParaRemover.ParentNode?.RemoveChild(assinaturaParaRemover);
            }

            // 6. Construir o XML canonical usando a MESMA lógica do Python (build_tpNFTS_bytes)
            // O elemento raiz deve ser <tpNFTS> (não <NFTS>) e os elementos em ordem canônica
            try
            {
                // Construir XML canonical replicando a lógica Python
                byte[] xmlBytes = ConstruirXmlCanonicalTpNFTS(nftsSemAssinatura);
                string xmlNftsSemAssinatura = Encoding.UTF8.GetString(xmlBytes);
                
                Console.WriteLine($"✓ XML canonical gerado ({xmlBytes.Length} bytes)");
                Console.WriteLine($"  Preview: {xmlNftsSemAssinatura.Substring(0, Math.Min(100, xmlNftsSemAssinatura.Length))}...");
                
                // DEBUG: Salvar XML canonical para análise
                string debugPath = Path.Combine(Path.GetDirectoryName(caminhoXml)!, "debug_canonical_nfts.xml");
                File.WriteAllText(debugPath, xmlNftsSemAssinatura, Encoding.UTF8);
                Console.WriteLine($"  Debug: XML canonical salvo em {debugPath}");
                
                // DEBUG: Mostrar primeiros 200 caracteres para comparação
                Console.WriteLine($"  Canonical (200 chars): {xmlNftsSemAssinatura.Substring(0, Math.Min(200, xmlNftsSemAssinatura.Length))}");

                // 7. Validar a assinatura usando a chave pública
                if (certificado != null)
                {
                    bool assinaturaValida = ValidarAssinaturaComCertificado(xmlBytes, assinaturaExistente, certificado);
                    
                    if (assinaturaValida)
                    {
                        Console.WriteLine("✅ ASSINATURA VÁLIDA!");
                        Console.WriteLine("   A assinatura corresponde ao XML e ao certificado.");
                    }
                    else
                    {
                        Console.WriteLine("❌ ASSINATURA INVÁLIDA!");
                        Console.WriteLine("   A assinatura NÃO corresponde ao XML ou ao certificado.");
                    }
                    
                    Console.WriteLine("===================================\n");
                    return assinaturaValida;
                }
                else
                {
                    Console.WriteLine("⚠ Não foi possível validar a assinatura:");
                    Console.WriteLine("  Certificado não encontrado no XML.");
                    Console.WriteLine("  Para validar, é necessário ter o certificado público (X509Certificate).");
                    Console.WriteLine("===================================\n");
                    return false;
                }
            }
            catch (Exception exDeserialize)
            {
                Console.WriteLine($"❌ Erro ao processar NFTS: {exDeserialize.Message}");
                if (exDeserialize.InnerException != null)
                {
                    Console.WriteLine($"   Inner: {exDeserialize.InnerException.Message}");
                }
                Console.WriteLine($"   Stack: {exDeserialize.StackTrace}");
                Console.WriteLine("===================================\n");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ERRO ao validar assinatura: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            Console.WriteLine("===================================\n");
            return false;
        }
    }

    /// <summary>
    /// Constrói o XML canonical para assinatura seguindo a mesma lógica do Python (build_tpNFTS_bytes)
    /// Elemento raiz: <tpNFTS> (não <NFTS>)
    /// Ordem canônica conforme canonical_order_map do Python
    /// </summary>
    private static byte[] ConstruirXmlCanonicalTpNFTS(XmlNode nftsNode)
    {
        var xmlDoc = new XmlDocument();
        var root = xmlDoc.CreateElement("tpNFTS");
        xmlDoc.AppendChild(root);

        // Ordem canônica exata do Python
        AdicionarElementoSeExistir(nftsNode, root, "TipoDocumento", NormalizarNumeroString);
        
        // ChaveDocumento (bloco)
        var chaveDoc = nftsNode.SelectSingleNode("ChaveDocumento") ?? 
                       nftsNode.SelectSingleNode("*[local-name()='ChaveDocumento']");
        if (chaveDoc != null)
        {
            var chaveDocElem = xmlDoc.CreateElement("ChaveDocumento");
            AdicionarElementoSeExistir(chaveDoc, chaveDocElem, "InscricaoMunicipal", NormalizarString);
            AdicionarElementoSeExistir(chaveDoc, chaveDocElem, "SerieNFTS", NormalizarSerie);
            AdicionarElementoSeExistir(chaveDoc, chaveDocElem, "NumeroDocumento", NormalizarNumeroString);
            if (chaveDocElem.HasChildNodes) root.AppendChild(chaveDocElem);
        }

        AdicionarElementoSeExistir(nftsNode, root, "DataPrestacao", NormalizarString);
        AdicionarElementoSeExistir(nftsNode, root, "StatusNFTS", NormalizarString);
        AdicionarElementoSeExistir(nftsNode, root, "TributacaoNFTS", NormalizarString);
        AdicionarElementoSeExistir(nftsNode, root, "ValorServicos", NormalizarFloat);
        AdicionarElementoSeExistir(nftsNode, root, "ValorDeducoes", NormalizarFloat);
        AdicionarElementoSeExistir(nftsNode, root, "CodigoServico", NormalizarNumeroString);
        // NOTA: CodigoSubItem é opcional - só incluir se existir explicitamente
        // Manual mostra CodigoSubItem mas alguns XMLs não têm (ou têm CodigoCnae que é diferente)
        AdicionarElementoSeExistir(nftsNode, root, "CodigoSubItem", NormalizarNumeroString);
        AdicionarElementoSeExistir(nftsNode, root, "AliquotaServicos", NormalizarFloat);
        AdicionarElementoSeExistir(nftsNode, root, "ISSRetidoTomador", NormalizarBoolean);
        AdicionarElementoSeExistir(nftsNode, root, "ISSRetidoIntermediario", NormalizarBoolean);

        // Prestador (bloco)
        var prestador = nftsNode.SelectSingleNode("Prestador") ?? 
                        nftsNode.SelectSingleNode("*[local-name()='Prestador']");
        if (prestador != null)
        {
            var prestadorElem = xmlDoc.CreateElement("Prestador");
            
            var cpfcnpj = prestador.SelectSingleNode("CPFCNPJ") ?? 
                         prestador.SelectSingleNode("*[local-name()='CPFCNPJ']");
            if (cpfcnpj != null)
            {
                var cpfcnpjElem = xmlDoc.CreateElement("CPFCNPJ");
                AdicionarElementoSeExistir(cpfcnpj, cpfcnpjElem, "CNPJ", NormalizarNumeroString);
                AdicionarElementoSeExistir(cpfcnpj, cpfcnpjElem, "CPF", NormalizarNumeroString);
                if (cpfcnpjElem.HasChildNodes) prestadorElem.AppendChild(cpfcnpjElem);
            }

            AdicionarElementoSeExistir(prestador, prestadorElem, "InscricaoMunicipal", NormalizarString);
            AdicionarElementoSeExistir(prestador, prestadorElem, "RazaoSocialPrestador", NormalizarString);

            var endereco = prestador.SelectSingleNode("Endereco") ?? 
                          prestador.SelectSingleNode("*[local-name()='Endereco']");
            if (endereco != null)
            {
                var enderecoElem = xmlDoc.CreateElement("Endereco");
                AdicionarElementoSeExistir(endereco, enderecoElem, "TipoLogradouro", NormalizarString);
                AdicionarElementoSeExistir(endereco, enderecoElem, "Logradouro", NormalizarString);
                AdicionarElementoSeExistir(endereco, enderecoElem, "NumeroEndereco", NormalizarString);
                AdicionarElementoSeExistir(endereco, enderecoElem, "ComplementoEndereco", NormalizarString);
                AdicionarElementoSeExistir(endereco, enderecoElem, "Bairro", NormalizarString);
                AdicionarElementoSeExistir(endereco, enderecoElem, "Cidade", NormalizarNumeroString);
                AdicionarElementoSeExistir(endereco, enderecoElem, "UF", NormalizarString);
                AdicionarElementoSeExistir(endereco, enderecoElem, "CEP", NormalizarString);
                if (enderecoElem.HasChildNodes) prestadorElem.AppendChild(enderecoElem);
            }

            AdicionarElementoSeExistir(prestador, prestadorElem, "Email", NormalizarString);
            if (prestadorElem.HasChildNodes) root.AppendChild(prestadorElem);
        }

        AdicionarElementoSeExistir(nftsNode, root, "RegimeTributacao", NormalizarNumeroString);
        AdicionarElementoSeExistir(nftsNode, root, "DataPagamento", NormalizarString);
        AdicionarElementoSeExistir(nftsNode, root, "Discriminacao", NormalizarString);
        AdicionarElementoSeExistir(nftsNode, root, "TipoNFTS", NormalizarNumeroString);

        // Tomador (bloco opcional)
        var tomador = nftsNode.SelectSingleNode("Tomador") ?? 
                      nftsNode.SelectSingleNode("*[local-name()='Tomador']");
        if (tomador != null)
        {
            var tomadorElem = xmlDoc.CreateElement("Tomador");
            
            var cpfcnpjTom = tomador.SelectSingleNode("CPFCNPJ") ?? 
                            tomador.SelectSingleNode("*[local-name()='CPFCNPJ']");
            if (cpfcnpjTom != null)
            {
                var cpfcnpjElem = xmlDoc.CreateElement("CPFCNPJ");
                AdicionarElementoSeExistir(cpfcnpjTom, cpfcnpjElem, "CPF", NormalizarNumeroString);
                AdicionarElementoSeExistir(cpfcnpjTom, cpfcnpjElem, "CNPJ", NormalizarNumeroString);
                if (cpfcnpjElem.HasChildNodes) tomadorElem.AppendChild(cpfcnpjElem);
            }

            AdicionarElementoSeExistir(tomador, tomadorElem, "RazaoSocial", NormalizarString);
            if (tomadorElem.HasChildNodes) root.AppendChild(tomadorElem);
        }

        // Serializar sem formatação
        using var memStream = new MemoryStream();
        using (var writer = new XmlTextWriter(memStream, Encoding.UTF8))
        {
            writer.Formatting = Formatting.None;
            xmlDoc.WriteTo(writer);
            writer.Flush();
        }

        byte[] result = memStream.ToArray();
        
        // Remover BOM se existir
        if (result.Length >= 3 && result[0] == 0xEF && result[1] == 0xBB && result[2] == 0xBF)
        {
            result = result.Skip(3).ToArray();
        }

        return result;
    }

    private static void AdicionarElementoSeExistir(XmlNode parent, XmlElement target, string elementName, Func<string?, string> normalizar)
    {
        var node = parent.SelectSingleNode(elementName) ?? 
                   parent.SelectSingleNode($"*[local-name()='{elementName}']");
        if (node != null)
        {
            string valor = normalizar(node.InnerText);
            if (!string.IsNullOrEmpty(valor))
            {
                var elem = target.OwnerDocument!.CreateElement(elementName);
                elem.InnerText = valor;
                target.AppendChild(elem);
            }
        }
    }

    private static string NormalizarString(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        // Remove espaços em branco extras e non-breaking spaces
        return text.Replace("\u00A0", " ").Trim();
    }

    private static string NormalizarNumeroString(string? text)
    {
        string clean = NormalizarString(text);
        if (string.IsNullOrEmpty(clean)) return "";
        
        // REGRA DO MANUAL: Remove zeros à esquerda de números (CPF, CNPJ, etc)
        // "O problema mais comum envolve CPFs e CNPJs que começam com 0"
        if (clean.All(char.IsDigit))
        {
            if (long.TryParse(clean, out long numero))
            {
                return numero.ToString(); // Remove zeros à esquerda
            }
        }
        return clean;
    }

    private static string NormalizarSerie(string? text)
    {
        // REGRA DO MANUAL: "Não fazer padding de valores (com "0" ou " ") a esquerda ou a direita"
        // SerieNFTS deve ser usada SEM padding, apenas o valor como está
        return NormalizarString(text);
    }

    private static string NormalizarFloat(string? text)
    {
        string clean = NormalizarString(text);
        if (string.IsNullOrEmpty(clean)) return "";
        clean = clean.Replace(',', '.');
        
        // Normalizar para formato decimal com 2 casas
        if (decimal.TryParse(clean, System.Globalization.NumberStyles.Any, 
            System.Globalization.CultureInfo.InvariantCulture, out decimal valor))
        {
            // Usar o formato que remove zeros desnecessários mas mantém pelo menos 2 casas
            return valor.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }
        return clean;
    }

    private static string NormalizarBoolean(string? text)
    {
        string clean = NormalizarString(text).ToLowerInvariant();
        if (clean == "true" || clean == "1" || clean == "s" || clean == "sim" || clean == "t" || clean == "y" || clean == "yes")
        {
            return "true";
        }
        return "false";
    }

    /// <summary>
    /// Valida uma assinatura usando a chave pública do certificado
    /// </summary>
    private static bool ValidarAssinaturaComCertificado(byte[] dados, byte[] assinatura, X509Certificate2 certificado)
    {
        try
        {
            using (RSA? rsa = certificado.GetRSAPublicKey())
            {
                if (rsa == null)
                {
                    Console.WriteLine("❌ Certificado não possui chave pública RSA.");
                    return false;
                }

                // Calcular hash SHA-1 dos dados
                using (var sha1 = SHA1.Create())
                {
                    byte[] hash = sha1.ComputeHash(dados);
                    
                    // DEBUG: Exibir informações de hash
                    Console.WriteLine($"\n[DEBUG]");
                    Console.WriteLine($"  Tamanho dados: {dados.Length} bytes");
                    Console.WriteLine($"  Hash SHA1: {Convert.ToBase64String(hash)}");
                    Console.WriteLine($"  Assinatura: {Convert.ToBase64String(assinatura).Substring(0, 50)}...");
                    
                    // Verificar a assinatura usando a chave pública
                    bool valida = rsa.VerifyHash(hash, assinatura, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
                    
                    return valida;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao verificar assinatura: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Extrai e exibe informações detalhadas do certificado no XML
    /// </summary>
    /// <param name="caminhoXml">Caminho completo do arquivo XML</param>
    public static void ExibirInformacoesCertificado(string caminhoXml)
    {
        try
        {
            if (!File.Exists(caminhoXml))
            {
                Console.WriteLine("❌ Arquivo não encontrado!");
                return;
            }

            var xmlDoc = new XmlDocument();
            xmlDoc.Load(caminhoXml);

            var namespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
            namespaceManager.AddNamespace("ds", "http://www.w3.org/2000/09/xmldsig#");

            var x509CertNode = xmlDoc.SelectSingleNode("//ds:X509Certificate", namespaceManager) ??
                              xmlDoc.SelectSingleNode("//X509Certificate");

            if (x509CertNode == null)
            {
                Console.WriteLine("❌ Certificado não encontrado no XML!");
                return;
            }

            string certBase64 = x509CertNode.InnerText.Trim().Replace("\n", "").Replace("\r", "").Replace(" ", "");
            byte[] certBytes = Convert.FromBase64String(certBase64);
            var certificado = new X509Certificate2(certBytes);

            Console.WriteLine("\n=== INFORMAÇÕES DO CERTIFICADO ===");
            Console.WriteLine($"Subject: {certificado.Subject}");
            Console.WriteLine($"Issuer: {certificado.Issuer}");
            Console.WriteLine($"Serial Number: {certificado.SerialNumber}");
            Console.WriteLine($"Thumbprint: {certificado.Thumbprint}");
            Console.WriteLine($"Válido de: {certificado.NotBefore:dd/MM/yyyy HH:mm:ss}");
            Console.WriteLine($"Válido até: {certificado.NotAfter:dd/MM/yyyy HH:mm:ss}");
            Console.WriteLine($"Expirado: {(DateTime.Now > certificado.NotAfter ? "SIM" : "NÃO")}");
            Console.WriteLine($"Tem chave privada: {(certificado.HasPrivateKey ? "SIM" : "NÃO")}");
            Console.WriteLine("===================================\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao extrair certificado: {ex.Message}");
        }
    }
}
