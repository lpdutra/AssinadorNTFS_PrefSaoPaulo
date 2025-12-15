using System;
using System.Xml.Serialization;
using System.ComponentModel.DataAnnotations;

namespace AssinadorNFTS.Models
{
    /// <summary>
    /// Schema utilizado para PEDIDO de envio de lote de NFTS.
    /// Este Schema XML é utilizado pelos tomadores/intermediários de serviços para emissão de NFTS.
    /// </summary>
    [XmlRoot("PedidoEnvioLoteNFTS", Namespace = "http://www.prefeitura.sp.gov.br/nfts")]
    public class PedidoEnvioLoteNfts
    {
        /// <summary>
        /// Cabeçalho do pedido NFTS.
        /// </summary>
        [XmlElement("Cabecalho")]
        [Required]
        public CabecalhoPedidoEnvioLote Cabecalho { get; set; } = null!;

        /// <summary>
        /// Informe as NFTS a serem emitidas (mínimo 1, máximo 50).
        /// </summary>
        [XmlElement("NFTS")]
        [Required]
        [MinLength(1)]
        [MaxLength(50)]
        public TpNfts[] Nfts { get; set; } = Array.Empty<TpNfts>();

        /// <summary>
        /// Assinatura digital do contribuinte que gerou as NFTS contidos na mensagem XML.
        /// </summary>
        [XmlElement("Signature", Namespace = "http://www.w3.org/2000/09/xmldsig#")]
        public object? Signature { get; set; }
    }

    /// <summary>
    /// Cabeçalho do pedido de envio de lote NFTS.
    /// </summary>
    public class CabecalhoPedidoEnvioLote
    {
        /// <summary>
        /// Informe os dados do Remetente autorizado a transmitir a mensagem XML.
        /// </summary>
        [XmlElement("Remetente")]
        [Required]
        public TpRemetente Remetente { get; set; } = null!;

        /// <summary>
        /// Informe se as NFTS a serem emitidas farão parte de uma mesma transação.
        /// True - As NFTS só serão emitidas se não ocorrer nenhum evento de erro durante o processamento de todo o lote;
        /// False - As NFTS válidos serão emitidas, mesmo que ocorram eventos de erro durante processamento de outras NFTS deste lote.
        /// </summary>
        [XmlElement("transacao")]
        public bool Transacao { get; set; } = true;

        [XmlIgnore]
        public bool TransacaoSpecified { get; set; }

        /// <summary>
        /// Informe a data de início do período transmitido (AAAA-MM-DD).
        /// </summary>
        [XmlElement("dtInicio", DataType = "date")]
        [Required]
        public DateTime DtInicio { get; set; }

        /// <summary>
        /// Informe a data final do período transmitido (AAAA-MM-DD).
        /// </summary>
        [XmlElement("dtFim", DataType = "date")]
        [Required]
        public DateTime DtFim { get; set; }

        /// <summary>
        /// Informe o total de NFTS contidos na mensagem XML.
        /// </summary>
        [XmlElement("QtdNFTS")]
        [Required]
        [RegularExpression(@"[0-9]{1,15}")]
        public long QtdNfts { get; set; }

        /// <summary>
        /// Informe o valor total dos serviços das NFTS contidos na mensagem XML.
        /// </summary>
        [XmlElement("ValorTotalServicos")]
        [Required]
        [RegularExpression(@"0|0\.[0-9]{2}|[1-9]{1}[0-9]{0,12}(\.[0-9]{0,2})?")]
        public decimal ValorTotalServicos { get; set; }

        /// <summary>
        /// Informe o valor total das deduções das NFTS contidos na mensagem XML (opcional).
        /// </summary>
        [XmlElement("ValorTotalDeducoes")]
        [RegularExpression(@"0|0\.[0-9]{2}|[1-9]{1}[0-9]{0,12}(\.[0-9]{0,2})?")]
        public decimal ValorTotalDeducoes { get; set; }

        [XmlIgnore]
        public bool ValorTotalDeducoesSpecified { get; set; }

        /// <summary>
        /// Informe a Versão do Schema XML utilizado.
        /// </summary>
        [XmlAttribute("Versao")]
        [Required]
        [RegularExpression(@"[0-9]{1,3}")]
        public long Versao { get; set; } = 1;

        /// <summary>
        /// ID da tag (opcional).
        /// </summary>
        [XmlAttribute("id")]
        public string? Id { get; set; }
    }
}
