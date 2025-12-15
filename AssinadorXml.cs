using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using AssinadorNFTS.Models;

namespace AssinadorNFTS;

/// <summary>
/// Classe responsável por assinar XML de NFTS conforme especificação da Prefeitura de São Paulo
/// </summary>
public class AssinadorXml
{
    /// <summary>
    /// Assina um objeto utilizando certificado digital X509
    /// </summary>
    /// <param name="x509certificate">Certificado digital para assinatura</param>
    /// <param name="detalheItem">Objeto a ser assinado</param>
    /// <returns>Array de bytes com a assinatura</returns>
    public static byte[] Assinar(X509Certificate2 x509certificate, object detalheItem)
    {
        return Assinar(x509certificate, detalheItem, null, 0);
    }

    /// <summary>
    /// Assina um objeto utilizando certificado digital X509 e salva o arquivo canonical para debug
    /// </summary>
    /// <param name="x509certificate">Certificado digital para assinatura</param>
    /// <param name="detalheItem">Objeto a ser assinado</param>
    /// <param name="debugDir">Diretório onde salvar arquivos de debug (null para não salvar)</param>
    /// <param name="nftsCounter">Contador da NFTS (para nomear o arquivo)</param>
    /// <returns>Array de bytes com a assinatura</returns>
    public static byte[] Assinar(X509Certificate2 x509certificate, object detalheItem, string? debugDir, int nftsCounter)
    {
        byte[] arrayToSign = SimpleXmlFragment(detalheItem);
        
        // Salvar arquivo canonical para debug
        if (!string.IsNullOrEmpty(debugDir))
        {
            try
            {
                Directory.CreateDirectory(debugDir);
                string canonicalFile = Path.Combine(debugDir, $"canonical_NFTS_{nftsCounter}.bin");
                string canonicalTxtFile = Path.Combine(debugDir, $"canonical_NFTS_{nftsCounter}.txt");
                
                File.WriteAllBytes(canonicalFile, arrayToSign);
                File.WriteAllText(canonicalTxtFile, Encoding.UTF8.GetString(arrayToSign), Encoding.UTF8);
                
                Console.WriteLine($" canonical salvo em: {canonicalFile} (len={arrayToSign.Length})");
                Console.WriteLine($" canonical (texto) salvo em: {canonicalTxtFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Aviso: Não foi possível salvar arquivo canonical: {ex.Message}");
            }
        }
        
        byte[] signature = CreateSignaturePKCS1(x509certificate, arrayToSign);
        
        // Salvar arquivos de assinatura para debug
        if (!string.IsNullOrEmpty(debugDir))
        {
            try
            {
                string signatureBinFile = Path.Combine(debugDir, $"signature_NFTS_{nftsCounter}.bin");
                string signatureB64File = Path.Combine(debugDir, $"signature_NFTS_{nftsCounter}.b64");
                
                File.WriteAllBytes(signatureBinFile, signature);
                File.WriteAllText(signatureB64File, Convert.ToBase64String(signature), Encoding.ASCII);
                
                Console.WriteLine($" assinatura salva em: {signatureBinFile} / {signatureB64File}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Aviso: Não foi possível salvar arquivos de assinatura: {ex.Message}");
            }
        }
        
        return signature;
    }

    /// <summary>
    /// Assina uma string XML utilizando certificado digital X509
    /// </summary>
    /// <param name="x509certificate">Certificado digital para assinatura</param>
    /// <param name="xmlString">String XML a ser assinada</param>
    /// <returns>Array de bytes com a assinatura</returns>
    public static byte[] AssinarXmlString(X509Certificate2 x509certificate, string xmlString)
    {
        byte[] arrayToSign = Encoding.UTF8.GetBytes(xmlString);
        return CreateSignaturePKCS1(x509certificate, arrayToSign);
    }

    /// <summary>
    /// Cria assinatura PKCS1 usando RSA e SHA-1
    /// </summary>
    /// <param name="x509">Certificado digital</param>
    /// <param name="value">Dados a serem assinados</param>
    /// <returns>Assinatura digital</returns>
    private static byte[] CreateSignaturePKCS1(X509Certificate2 x509, byte[] value)
    {
        // Usar GetRSAPrivateKey() em vez de PrivateKey (obsoleto)
        using (RSA? rsa = x509.GetRSAPrivateKey())
        {
            if (rsa == null)
                throw new InvalidOperationException("Certificado não possui chave privada RSA.");

            // Usar SHA1.Create() em vez de SHA1CryptoServiceProvider (obsoleto)
            using (var sha1 = SHA1.Create())
            {
                byte[] hash = sha1.ComputeHash(value);
                
                // Assinar usando SignHash diretamente
                return rsa.SignHash(hash, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
            }
        }
    }

    /// <summary>
    /// Serializa objeto para XML sem declaração e sem namespaces
    /// </summary>
    /// <param name="objectGraph">Objeto a ser serializado</param>
    /// <returns>Array de bytes com XML serializado</returns>
    public static byte[] SimpleXmlFragment(object objectGraph)
    {
        if (objectGraph == null)
        {
            throw new InvalidOperationException("Nenhum Grafo de Objetos foi especificado.");
        }

        MemoryStream memoryStream = new MemoryStream();
        XmlTextWriter textWriter = new XmlFragmentWriter(memoryStream, Encoding.UTF8);
        textWriter.Namespaces = false;
        textWriter.Formatting = Formatting.None;

        XmlSerializer xmlSerializer = new XmlSerializer(objectGraph.GetType());
        XmlSerializerNamespaces xmlSerializerNamespaces = new XmlSerializerNamespaces();
        xmlSerializerNamespaces.Add(string.Empty, string.Empty);

        xmlSerializer.Serialize(textWriter, objectGraph, xmlSerializerNamespaces);

        textWriter.Close();
        memoryStream.Close();

        var xml = Encoding.UTF8.GetString(memoryStream.ToArray());

        // Remove o BOM (Byte Order Mark), se houver
        string byteOrderMarkUtf8 = Encoding.UTF8.GetString(Encoding.UTF8.GetPreamble());
        if (xml.StartsWith(byteOrderMarkUtf8))
        {
            xml = xml.Remove(0, byteOrderMarkUtf8.Length);
        }

        // Ajustar TipoDocumento para 1 dígito (conforme especificação para assinatura)
        // Substitui <TipoDocumento>01</TipoDocumento> por <TipoDocumento>1</TipoDocumento>
        xml = Regex.Replace(xml, @"<TipoDocumento>0([1-3])</TipoDocumento>", "<TipoDocumento>$1</TipoDocumento>");

        return Encoding.UTF8.GetBytes(xml);
    }
}
