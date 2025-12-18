using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;

namespace AssinadorNFTS
{
    /// <summary>
    /// Classe responsável por recalcular a assinatura XMLDSig de um arquivo XML existente
    /// </summary>
    public class RecalcularAssinaturaXmlDSigByPathArquivo
    {
        private static string _caminhoXmlOriginal;
        private static string _caminhoCertificado;
        private static string _senhaCertificado;

        /// <summary>
        /// Executa o processo de recálculo da assinatura XMLDSig
        /// </summary>
        /// <returns>Caminho do arquivo assinado gerado</returns>
        public static string DoProcess(string caminhoXmlOriginal,
            string caminhoCertificado,
            string senhaCertificado)
        {
            _caminhoXmlOriginal = caminhoXmlOriginal;
            _caminhoCertificado = caminhoCertificado;
            _senhaCertificado = senhaCertificado;

            try
            {
                Console.WriteLine("\n=== RECALCULANDO ASSINATURA XMLDSIG ===");

                // 1. Carregar certificado
                var certificado = CarregarCertificado();

                // 2. Ler o XML original
                string xmlContent = LerXmlOriginal();

                // 3. Remover assinatura XMLDSig existente (se houver)
                XmlDocument docSemAssinatura = RemoverAssinaturaExistente(xmlContent);

                // 4. Adicionar nova assinatura XMLDSig
                XmlDocument docAssinado = AdicionarNovaAssinatura(docSemAssinatura, certificado);

                // 5. Salvar o arquivo assinado
                string caminhoAssinado = SalvarArquivoAssinado(docAssinado);

                Console.WriteLine($"✓ Arquivo assinado salvo: {caminhoAssinado}");
                Console.WriteLine("\n=== ASSINATURA XMLDSIG RECALCULADA COM SUCESSO ===\n");

                return caminhoAssinado;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ ERRO ao recalcular assinatura: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private static X509Certificate2 CarregarCertificado()
        {
            var certificado = new X509Certificate2(_caminhoCertificado, _senhaCertificado);
            Console.WriteLine($"✓ Certificado carregado: {certificado.Subject}");
            return certificado;
        }

        private static string LerXmlOriginal()
        {
            string xmlContent = File.ReadAllText(_caminhoXmlOriginal, Encoding.UTF8);
            Console.WriteLine($"✓ XML lido: {_caminhoXmlOriginal}");
            return xmlContent;
        }

        private static XmlDocument RemoverAssinaturaExistente(string xmlContent)
        {
            XmlDocument docOriginal = new XmlDocument();
            docOriginal.PreserveWhitespace = true;
            docOriginal.LoadXml(xmlContent);

            // Remover tag <Signature> se existir
            XmlNamespaceManager nsManager = new XmlNamespaceManager(docOriginal.NameTable);
            nsManager.AddNamespace("ds", "http://www.w3.org/2000/09/xmldsig#");

            XmlNode signatureNode = docOriginal.SelectSingleNode("//ds:Signature", nsManager);
            if (signatureNode != null)
            {
                signatureNode.ParentNode.RemoveChild(signatureNode);
                Console.WriteLine("✓ Assinatura XMLDSig anterior removida");
            }
            else
            {
                Console.WriteLine("ℹ Nenhuma assinatura XMLDSig encontrada no XML original");
            }

            return docOriginal;
        }

        private static XmlDocument AdicionarNovaAssinatura(XmlDocument docSemAssinatura, X509Certificate2 certificado)
        {
            XmlDocument docParaAssinar = new XmlDocument();
            docParaAssinar.PreserveWhitespace = true;
            docParaAssinar.LoadXml(docSemAssinatura.OuterXml);

            SignedXml signedXml = new SignedXml(docParaAssinar);
            signedXml.SigningKey = certificado.GetRSAPrivateKey();

            // Configurar a referência
            Reference reference = new Reference("");
            reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
            reference.DigestMethod = "http://www.w3.org/2000/09/xmldsig#sha1";
            signedXml.AddReference(reference);

            // Configurar o método de assinatura
            signedXml.SignedInfo.CanonicalizationMethod = "http://www.w3.org/2001/10/xml-exc-c14n#";
            signedXml.SignedInfo.SignatureMethod = "http://www.w3.org/2000/09/xmldsig#rsa-sha1";

            // Adicionar informações do certificado
            KeyInfo keyInfo = new KeyInfo();
            keyInfo.AddClause(new KeyInfoX509Data(certificado));
            signedXml.KeyInfo = keyInfo;

            // Calcular a assinatura
            signedXml.ComputeSignature();
            Console.WriteLine("✓ Nova assinatura XMLDSig calculada");

            // Obter o elemento de assinatura
            XmlElement xmlSignature = signedXml.GetXml();

            // Importar e adicionar ao documento
            XmlNode importedSignature = docParaAssinar.ImportNode(xmlSignature, true);
            docParaAssinar.DocumentElement.AppendChild(importedSignature);

            return docParaAssinar;
        }

        private static string SalvarArquivoAssinado(XmlDocument docAssinado)
        {
            string caminhoAssinado = _caminhoXmlOriginal.Replace(".xml", "-assinado.xml");

            using (var writer = new StreamWriter(caminhoAssinado, false, new UTF8Encoding(false)))
            {
                writer.Write(docAssinado.OuterXml);
            }

            return caminhoAssinado;
        }
    }
}
