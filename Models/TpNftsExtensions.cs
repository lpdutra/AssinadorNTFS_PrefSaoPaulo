using System;
using System.Xml.Serialization;
using System.IO;
using System.Text;
using System.Xml;

namespace AssinadorNFTS.Models
{
    /// <summary>
    /// Extensão da classe TpNfts para métodos auxiliares
    /// </summary>
    public partial class TpNfts
    {
        // Esta classe parcial pode ser usada para adicionar métodos customizados
        // A serialização de data será tratada diretamente no AssinadorXml
    }

    /// <summary>
    /// Métodos de extensão para enums da NFTS
    /// </summary>
    public static class TpNftsEnumExtensions
    {
        /// <summary>
        /// Converte TpTipoDocumentoNfts para string de 1 dígito (usado na assinatura)
        /// </summary>
        public static string ToSingleDigitString(this TpTipoDocumentoNfts tipo)
        {
            return tipo switch
            {
                TpTipoDocumentoNfts.Item01 => "1",
                TpTipoDocumentoNfts.Item02 => "2",
                TpTipoDocumentoNfts.Item03 => "3",
                _ => "2"
            };
        }
    }
}
