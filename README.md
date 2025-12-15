# Assinador de NFTS - Prefeitura de São Paulo

Projeto C# para assinatura digital de XML de Nota Fiscal de Tomador de Serviços (NFTS) conforme especificação da Prefeitura de São Paulo.

## Estrutura do Projeto

- **AssinadorXml.cs**: Classe principal para assinatura XML usando certificado digital X509
- **XmlFragmentWriter.cs**: Writer XML customizado que omite a declaração XML
- **Program.cs**: Aplicação console principal

## Funcionalidades Implementadas

### Assinatura Digital
- ✅ Assinatura usando RSA com SHA-1
- ✅ Formato PKCS#1
- ✅ Serialização XML sem namespaces
- ✅ Remoção de BOM (Byte Order Mark)
- ✅ Omissão da declaração XML

## Próximos Passos

1. Gerar classes C# a partir dos schemas XSD fornecidos
2. Implementar leitura do arquivo `nfts.xml`
3. Implementar carregamento de certificado digital
4. Completar o processo de assinatura
5. Salvar XML assinado

## Como Usar

```csharp
// Exemplo de uso (após implementação completa):
var certificado = new X509Certificate2("certificado.pfx", "senha");
var nfts = new TpNFTS();
// ... preencher dados da NFTS ...
nfts.Assinatura = AssinadorXml.Assinar(certificado, nfts);
```

## Requisitos

- .NET 8.0 ou superior
- Certificado digital X509 (A1 ou A3)
- Schemas XSD da Prefeitura de São Paulo

## Observações

- A assinatura utiliza SHA-1 conforme especificação da Prefeitura
- O certificado usado na assinatura adicional deve ser o mesmo da assinatura da mensagem XML
