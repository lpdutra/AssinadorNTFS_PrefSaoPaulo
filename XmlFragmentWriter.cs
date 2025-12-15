using System.Text;
using System.Xml;

namespace AssinadorNFTS;

/// <summary>
/// XmlTextWriter customizado que omite a declaração XML
/// </summary>
internal class XmlFragmentWriter : XmlTextWriter
{
    public XmlFragmentWriter(Stream stream, Encoding encoding) 
        : base(stream, encoding)
    {
    }

    public override void WriteStartDocument()
    {
        // Não faz nada (omite a declaração XML)
    }
}
