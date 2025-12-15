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

## Script Python para Assinatura de NFTS

O projeto inclui um script Python (`python/sign_xml_soap_nfts_original.py`) que realiza a assinatura digital de arquivos XML de NFTS e gera o envelope SOAP para envio.

### Funcionalidades do Script Python

- ✅ Assinatura individual de cada bloco `<NFTS>` usando SHA-1 + PKCS#1 v1.5
- ✅ Normalização e canonicalização conforme tipo `tpNFTS`
- ✅ Geração de envelope SOAP `TesteEnvioLoteNFTSRequest` com XML em CDATA
- ✅ Assinatura opcional do documento inteiro usando xmlsec
- ✅ Modo de verificação de assinaturas existentes
- ✅ Suporte a certificados digitais .pfx (A1)

### Modos de Uso

#### 1. Assinar XML e Gerar SOAP

```bash
python sign_xml_soap_nfts_original.py input_nfts.xml cert.pfx senha output_soap.xml
```

**Exemplo:**
```bash
python sign_xml_soap_nfts_original.py nfts_original.xml "Fesp cert A1.pfx" Unimed2025 nfts_assinado_original.xml
```

**Parâmetros:**
- `input_nfts.xml`: Arquivo XML de entrada contendo os blocos NFTS
- `cert.pfx`: Certificado digital em formato PKCS#12 (.pfx)
- `senha`: Senha do certificado (use "" para certificados sem senha)
- `output_soap.xml`: Arquivo de saída com o envelope SOAP gerado

#### 2. Modo Debug (com arquivos de diagnóstico)

```bash
python sign_xml_soap_nfts_original.py --debug input_nfts.xml cert.pfx senha output_soap.xml
```

**Exemplo:**
```bash
python sign_xml_soap_nfts_original.py --debug nfts_original.xml "Fesp cert A1.pfx" Unimed2025 nfts_assinado_original.xml
```

**Nota:** No código atual, os arquivos de debug estão comentados para evitar geração de arquivos temporários. Para habilitar, descomente as linhas que salvam:
- `canonical_NFTS_{i}.bin`: Representação canônica do bloco NFTS
- `signature_NFTS_{i}.bin`: Assinatura binária
- `signature_NFTS_{i}.b64`: Assinatura em Base64

#### 3. Modo Verificação

Verifica assinaturas já existentes em um XML assinado:

```bash
python sign_xml_soap_nfts_original.py --verify NFTS_assinado.xml cert.pfx senha
```

**Exemplo:**
```bash
python sign_xml_soap_nfts_original.py --verify nfts_assinado_original.xml "Fesp cert A1.pfx" Unimed2025
```

Este modo:
- Valida cada assinatura contra o certificado fornecido
- Gera arquivos `canonical_NFTS_{i}.bin` para análise manual
- Exibe hash SHA-1 e informações de diagnóstico
- Reporta se a assinatura é válida ou não

### Dependências Python

```bash
pip install lxml cryptography xmlsec
```

**Pacotes necessários:**
- `lxml`: Processamento XML com suporte a XPath
- `cryptography`: Operações criptográficas (PKCS#12, SHA-1, RSA)
- `xmlsec`: (Opcional) Assinatura do documento inteiro

### Como Funciona

1. **Leitura do XML**: Parse do arquivo de entrada
2. **Extração da Chave**: Lê certificado e chave privada do arquivo .pfx
3. **Para cada bloco NFTS**:
   - Remove elemento `<Assinatura>` existente
   - Constrói representação canônica `tpNFTS` com:
     - Ordem específica dos elementos
     - Normalização de valores numéricos
     - Normalização de valores booleanos
     - Formatação de valores monetários (2 casas decimais)
     - Normalização da série NFTS (5 caracteres)
   - Gera hash SHA-1 da representação canônica
   - Assina com RSA PKCS#1 v1.5
   - Insere assinatura em Base64 no elemento `<Assinatura>`
4. **Assinatura do Documento**: (Opcional) Assina o documento completo com xmlsec
5. **Geração do SOAP**: Cria envelope SOAP com XML assinado em CDATA

### Normalização de Dados (tpNFTS)

O script aplica normalizações específicas conforme o manual da Prefeitura:

- **Numéricos**: Remove espaços e converte para string numérica
- **Série NFTS**: Padroniza para exatamente 5 caracteres
- **Valores Monetários**: Formato com 2 casas decimais (ex: "1500.00")
- **Booleanos**: Converte para "true" ou "false"
- **Strings**: Remove espaços extras e caracteres não-imprimíveis

### Arquivos de Debug (Modo --verify)

No modo `--verify`, os seguintes arquivos são salvos em:
- **Windows**: `C:\temp\nfts_debug\`
- **Linux/Mac**: `/tmp/nfts_debug/`

Arquivos gerados:
- `canonical_NFTS_{i}.bin`: Bytes canônicos usados para assinatura
- Úteis para diagnóstico e validação manual

### Saída do Envelope SOAP

O arquivo de saída contém:

```xml
<?xml version="1.0" encoding="utf-8"?>
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
    <TesteEnvioLoteNFTSRequest xmlns="http://www.prefeitura.sp.gov.br/nfts">
      <VersaoSchema>2</VersaoSchema>
      <MensagemXML><![CDATA[...XML assinado...]]></MensagemXML>
    </TesteEnvioLoteNFTSRequest>
  </soap:Body>
</soap:Envelope>
```

### Troubleshooting

**Erro de verificação de assinatura:**
- Verifique se está usando o mesmo certificado usado para assinar
- Confirme que a senha está correta
- Verifique os arquivos `canonical_NFTS_{i}.bin` gerados

**Erro de importação xmlsec:**
- A biblioteca xmlsec é opcional
- Se não instalada, apenas a assinatura dos blocos NFTS será feita (suficiente para a maioria dos casos)

**Encoding/BOM issues:**
- O script remove automaticamente BOM (Byte Order Mark)
- Força encoding UTF-8 sem declaração XML nos fragmentos canônicos

## Observações

- A assinatura utiliza SHA-1 conforme especificação da Prefeitura
- O certificado usado na assinatura adicional deve ser o mesmo da assinatura da mensagem XML
- O script Python está configurado para modo CRITICAL de logging (silencioso), exibindo apenas mensagens essenciais
