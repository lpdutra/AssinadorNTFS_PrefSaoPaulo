using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

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
        byte[] arrayToSign = SimpleXmlFragment(detalheItem);
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

        return Encoding.UTF8.GetBytes(xml);
    }
}
