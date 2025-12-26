#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
sign_nfts.py

Assina blocos <NFTS> (gerando Assinatura baseada na representação tpNFTS)
e cria o envelope SOAP TesteEnvioLoteNFTSRequest com o XML assinado em CDATA.

Suporta:
  * assinatura individual de cada <NFTS> (Assinatura em base64)
  * assinatura opcional do documento inteiro (xmlsec) - mantém comportamento anterior
  # * geração de arquivos de debug (canonical bytes, assinatura binária e base64) - COMENTADO
  * modo --verify para verificar assinaturas usando um .pfx
Usage:
  python sign_nfts.py input_nfts.xml cert.pfx senha output_soap.xml
  python sign_nfts.py --verify NFTS_assinado.xml cert.pfx senha
  python sign_nfts.py --debug input_nfts.xml cert.pfx senha output_soap.xml
"""

from __future__ import annotations
import sys
import os
import tempfile
import logging
import base64
import copy
from collections import OrderedDict
from typing import Tuple, Optional

from lxml import etree
from cryptography.hazmat.primitives.serialization import pkcs12, Encoding, PrivateFormat, NoEncryption
from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.asymmetric import padding

# xmlsec é opcional (assinatura do documento inteiro)
try:
    import xmlsec  # type: ignore
    XMLSEC_AVAILABLE = True
except Exception:
    XMLSEC_AVAILABLE = False

# Configuração de log (Definido como CRITICAL para silenciar a saída normal)
logging.basicConfig(level=logging.CRITICAL, format='%(levelname)s: %(message)s')
logger = logging.getLogger("sign_nfts")

NS_SP = "http://www.prefeitura.sp.gov.br/nfts"
NS_SOAP = "http://schemas.xmlsoap.org/soap/envelope/"

# ---------------- util ----------------

def ensure_path_for_debug() -> str:
    # A função ainda existe, mas não será usada pelo sign_file após as modificações.
    # No modo --verify, ela ainda será chamada para definir o caminho
    # onde os arquivos de debug (necessários para a verificação) serão salvos.
    # if os.name == 'nt':
    #     dirpath = r'\\unidcprog03\relatorios\NFTS\temp'
    # else:
    #     dirpath = '/mnt/file-server/relatorios/NFTS/temp'

    if os.name == 'nt':
        dirpath = r'D:\Workspace\FESP\Projeto_NTFS\python\nfts_debug'
    else:
        dirpath = '/tmp/nfts_debug'        
    try:
        os.makedirs(dirpath, exist_ok=True)
    except Exception:
        dirpath = tempfile.gettempdir()
    return dirpath

def find_child(parent: etree._Element, tagname: str) -> Optional[etree._Element]:
    """Encontra um filho por nome local, ignorando namespace."""
    nodes = parent.xpath('./*[local-name() = $name]', name=tagname)
    return nodes[0] if nodes else None

# ---------------- PKCS12 ----------------

def read_pkcs12(pfx_path: str, password: Optional[str]) -> Tuple[object, object]:
    """Lê private key e cert do PFX via cryptography. password pode ser ''."""
    with open(pfx_path, "rb") as f:
        data = f.read()
    pwd = None if password in (None, "") else password.encode("utf-8")
    private_key, cert, additional = pkcs12.load_key_and_certificates(data, pwd)
    if private_key is None or cert is None:
        raise RuntimeError("Não foi possível extrair chave privada / certificado do PFX")
    return private_key, cert

# ---------------- Normalizações ----------------

def normalize_numeric_string(text: Optional[str]) -> str:
    """
    Normaliza retirando os zeros a esquerda de strings numéricas.
    """
    if text is None:
        return ""
    clean_text = text.replace('\xa0', ' ').strip()
    if clean_text.isdigit():
        try:
            return str(int(clean_text))
        except ValueError:
            pass
    return clean_text

def normalize_serie_nfts(text: Optional[str]) -> str:
    if text is None:
        return "     "
    clean_text = text.replace('\xa0', ' ').strip()
    return (clean_text + "     ")[:5]

def normalize_float_value(text: Optional[str], format_decimals: bool = True) -> str:
    """
    Normaliza valor float.
    
    Args:
        text: Texto a ser normalizado
        format_decimals: Se True, formata para 2 casas decimais (ex: 3.5 -> 3.50)
                        Se False, mantém casas decimais originais mas remove zeros à esquerda (ex: 03.025 -> 3.025)
    """
    if text is None:
        return ""
    clean_text = text.replace('\xa0', ' ').replace(',', '.').strip()
    try:
        float_value = float(clean_text)
        if format_decimals:
            return "{:.2f}".format(float_value)
        else:
            # Converte para float e volta para string, removendo zeros à esquerda
            # mas mantendo as casas decimais originais
            return str(float_value)
    except Exception:
        return clean_text

def normalize_boolean_value(text: Optional[str]) -> str:
    if text is None:
        return "false"
    clean_text = text.replace('\xa0', ' ').strip().lower()
    if clean_text in ("true", "1", "s", "sim", "t", "y", "yes"):
        return "true"
    return "false"

# ---------------- Construir string canônica (tpNFTS) ----------------

def build_tpNFTS_bytes(nfts_node: etree._Element) -> bytes:
    """
    Constrói a representação canônica tpNFTS (bytes utf-8, sem declaração),
    usada para gerar o hash/assinatura conforme manual.
    """
    clean_tp = copy.deepcopy(nfts_node)
    assin = find_child(clean_tp, "Assinatura")
    if assin is not None:
        clean_tp.remove(assin)

    canonical_order_map = OrderedDict([
        ("TipoDocumento", "str"),
        ("ChaveDocumento", {
            "InscricaoMunicipal": "str",
            "SerieNFTS": "serie",
            "NumeroDocumento": "num_str",
        }),
        ("DataPrestacao", "str"),
        ("StatusNFTS", "str"),
        ("TributacaoNFTS", "str"),
        ("ValorServicos", "float_currency"),
        ("ValorDeducoes", "float_currency"),
        ("CodigoServico", "num_str"),
        ("CodigoSubItem", "num_str"),
        ("AliquotaServicos", "float"),
        ("ISSRetidoTomador", "bool"),
        ("ISSRetidoIntermediario", "bool"),
        ("Prestador", {
            "CPFCNPJ": {
                "CNPJ": "str",
                "CPF": "str",
            },
            "InscricaoMunicipal": "str",
            "RazaoSocialPrestador": "str",
            "Endereco": {
                "TipoLogradouro": "str",
                "Logradouro": "str",
                "NumeroEndereco": "str",
                "ComplementoEndereco": "str_opt",
                "Bairro": "str",
                "Cidade": "num_str",
                "UF": "str",
                "CEP": "str",
            },
            "Email": "str_opt",
        }),
        ("RegimeTributacao", "num_str"),
        ("DataPagamento", "str_opt"),
        ("Discriminacao", "str"),
        ("TipoNFTS", "num_str"),
        ("Tomador", {
            "CPFCNPJ": {
                "CPF": "str",
                "CNPJ": "str",
            },
            "RazaoSocial": "str",
        }),
    ])

    def build_fragment(node: etree._Element, order_map: OrderedDict) -> list:
        elems = []
        for tag_name, definition in order_map.items():
            original_child = find_child(node, tag_name)
            if original_child is None:
                continue
            if isinstance(definition, str):
                text_value = original_child.text or ""
                if definition == "num_str":
                    final = normalize_numeric_string(text_value)
                    # final = text_value.replace('\xa0', ' ').strip()
                elif definition == "float_currency":
                    final = normalize_float_value(text_value, format_decimals=True)
                elif definition == "float":
                    final = normalize_float_value(text_value, format_decimals=False)
                elif definition == "bool":
                    final = normalize_boolean_value(text_value)
                elif definition == "serie":
                    final = normalize_serie_nfts(text_value)
                else:
                    final = text_value.replace('\xa0', ' ').strip()
                if final == "":
                    continue
                el = etree.Element(tag_name)
                el.text = final
                elems.append(el)
            elif isinstance(definition, dict):
                nested = build_fragment(original_child, definition)
                if nested:
                    parent = etree.Element(tag_name)
                    parent.extend(nested)
                    elems.append(parent)
        return elems

    canonical_root = etree.Element("tpNFTS")
    canonical_root.extend(build_fragment(clean_tp, canonical_order_map))

    # serialize to bytes (utf-8), no xml declaration, no pretty print
    b = etree.tostring(canonical_root, encoding="utf-8", xml_declaration=False, pretty_print=False)
    # remove BOM if exists
    if b.startswith(b'\xef\xbb\xbf'):
        b = b[len(b'\xef\xbb\xbf'):]
    return b

# ---------------- assinatura SHA1 PKCS#1 v1.5 ----------------

def sign_bytes_sha1_pkcs1(private_key_obj, data_bytes: bytes) -> bytes:
    """
    Assina os bytes usando SHA1 + PKCS1v1.5.
    """
    signature = private_key_obj.sign(
        data_bytes,
        padding.PKCS1v15(),
        hashes.SHA1()
    )
    return signature

# ---------------- assinatura do documento (xmlsec) - opcional ----------------

def sign_document_xmlsec(root: etree._Element, key_pem_path: str, cert_pem_path: str):
    """Assina o documento inteiro usando xmlsec (opcional)."""
    if not XMLSEC_AVAILABLE:
        raise RuntimeError("xmlsec não está disponível")
    signature_node = xmlsec.template.create(
        root,
        xmlsec.Transform.EXCL_C14N,
        xmlsec.Transform.RSA_SHA1,
        ns='ds'
    )
    root.append(signature_node)
    ref = xmlsec.template.add_reference(signature_node, xmlsec.Transform.SHA1, uri="")
    xmlsec.template.add_transform(ref, xmlsec.Transform.ENVELOPED)
    key_info = xmlsec.template.ensure_key_info(signature_node)
    xmlsec.template.add_x509_data(key_info)
    ctx = xmlsec.SignatureContext()
    key = xmlsec.Key.from_file(key_pem_path, xmlsec.KeyFormat.PEM)
    key.load_cert_from_file(cert_pem_path, xmlsec.KeyFormat.PEM)
    ctx.key = key
    ctx.sign(signature_node)

# ---------------- SOAP envelope builder ----------------

def build_soap_envelope(xml_string: str) -> bytes:
    envelope = etree.Element("{%s}Envelope" % NS_SOAP, nsmap={'soap': NS_SOAP})
    body = etree.SubElement(envelope, "{%s}Body" % NS_SOAP)
    request = etree.SubElement(body, "{%s}TesteEnvioLoteNFTSRequest" % NS_SP)
    etree.SubElement(request, "{%s}VersaoSchema" % NS_SP).text = "2"
    mensagem = etree.SubElement(request, "{%s}MensagemXML" % NS_SP)
    mensagem.text = etree.CDATA(xml_string)
    return etree.tostring(envelope, encoding="utf-8", xml_declaration=True, pretty_print=True)

# ---------------- verify utility ----------------

def verify_signed_nfts(xml_path: str, pfx_path: str, pfx_pass: str):
    """
    Verifica assinaturas presentes dentro de cada <NFTS> contra o certificado do PFX.
    Imprime resultados e salva canonical_NFTS_{i}.bin quando necessário.
    """
    with open(pfx_path, "rb") as f:
        pfx_data = f.read()
    pwd = None if pfx_pass in (None, "") else pfx_pass.encode("utf-8")
    _, cert, _ = pkcs12.load_key_and_certificates(pfx_data, pwd)
    pubkey = cert.public_key()

    parser = etree.XMLParser(remove_blank_text=True)
    tree = etree.parse(xml_path, parser)
    root = tree.getroot()

    nfts_nodes = root.xpath('//*[local-name()="NFTS"]')
    if not nfts_nodes:
        logger.critical("Nenhum elemento <NFTS> encontrado no XML.")
        return

    # No modo --verify, os arquivos de debug (canonical) são necessários
    # para que o usuário possa verificar manualmente o que foi assinado.
    # O código abaixo salva o canonical.
    debug_dir = ensure_path_for_debug()

    for i, nfts in enumerate(nfts_nodes, start=1):
        logger.critical("Verificando NFTS #%d...", i)
        assin_node = nfts.xpath('./*[local-name()="Assinatura"]')
        if not assin_node:
            logger.critical(" Sem <Assinatura> em NFTS #%d", i)
            continue
        assinatura_b64 = "".join(assin_node[0].text.split())  # remove whitespace/newlines
        try:
            sig_bytes = base64.b64decode(assinatura_b64, validate=True)
        except Exception as e:
            logger.critical(" assinatura não é base64 válida: %s", e)
            continue

        canonical_bytes = build_tpNFTS_bytes(nfts)
        # save canonical for debugging
        dbgname = os.path.join(debug_dir, f"canonical_NFTS_{i}.bin")
        with open(dbgname, "wb") as df:
            df.write(canonical_bytes)
        logger.critical(" canonical salvo em: %s (len=%d)", dbgname, len(canonical_bytes))

        try:
            pubkey.verify(sig_bytes, canonical_bytes, padding.PKCS1v15(), hashes.SHA1())
            logger.critical(" VERIFICAÇÃO OK: assinatura válida para o certificado do PFX.")
        except Exception as e:
            logger.critical(" VERIFICAÇÃO FALHOU: %s", e)
            # dump diagnostics
            hasher = hashes.Hash(hashes.SHA1())
            hasher.update(canonical_bytes)
            digest = hasher.finalize()
            logger.critical(" SHA1(canonical) hex: %s", digest.hex())
            logger.critical(" assinatura len: %d bytes", len(sig_bytes))
            logger.critical(" certificado subject: %s", cert.subject.rfc4514_string())

# ---------------- Main signing flow ----------------

def sign_file(input_xml_path: str, pfx_path: str, pfx_pass: str, output_soap_path: str, debug: bool=False):
    logger.critical("Lendo XML de entrada: %s", input_xml_path)
    parser = etree.XMLParser(remove_blank_text=True)
    with open(input_xml_path, "rb") as f:
        xml_bytes = f.read()
    # parse as element tree
    tree = etree.fromstring(xml_bytes, parser=parser)
    root = tree if isinstance(tree, etree._Element) else tree.getroot()

    debug_dir = ensure_path_for_debug() # LINHA COMENTADA
    logger.critical("Debug dir: %s", debug_dir) # LINHA COMENTADA

    logger.critical("Extraindo chave privada e certificado do PFX...")
    private_key, cert = read_pkcs12(pfx_path, pfx_pass)

    # save PEMs for xmlsec (if necessary)
    tmpdir = tempfile.gettempdir()
    cert_pem_path = os.path.join(tmpdir, "tmp_cert_nfts.pem")
    key_pem_path = os.path.join(tmpdir, "tmp_key_nfts.pem")
    try:
        with open(cert_pem_path, "wb") as f:
            f.write(cert.public_bytes(Encoding.PEM))
        with open(key_pem_path, "wb") as f:
            f.write(private_key.private_bytes(Encoding.PEM, PrivateFormat.PKCS8, NoEncryption()))
    except Exception:
        logger.critical("Não foi possível gravar PEMs temporários (continua sem xmlsec).")

    # find NFTS nodes (ignore namespace)
    nfts_nodes = root.xpath('//*[local-name()="NFTS"]')
    if not nfts_nodes:
        logger.critical("Nenhum elemento <NFTS> encontrado no XML.")
        raise SystemExit(1)
    else:
        logger.critical("Encontrados %d NFTS nodes", len(nfts_nodes))

    for i, nfts in enumerate(nfts_nodes, start=1):
        logger.critical("Processando NFTS #%d ...", i)

        canonical_bytes = build_tpNFTS_bytes(nfts)  # bytes for signing

        canonical_file = os.path.join(debug_dir, f"canonical_NFTS_{i}.bin") # LINHA COMENTADA
        canonical_txt_file = os.path.join(debug_dir, f"canonical_NFTS_{i}.txt")
        with open(canonical_file, "wb") as cf: # LINHA COMENTADA
            cf.write(canonical_bytes) # LINHA COMENTADA
        with open(canonical_txt_file, "w", encoding="utf-8") as ctf:
            ctf.write(canonical_bytes.decode("utf-8"))
        logger.critical(" canonical salvo em: %s (len=%d)", canonical_file, len(canonical_bytes)) # LINHA COMENTADA
        logger.critical(" canonical (texto) salvo em: %s", canonical_txt_file)

        # sign with SHA1 PKCS#1 v1.5
        sig_bytes = sign_bytes_sha1_pkcs1(private_key, canonical_bytes)
        sig_b64 = base64.b64encode(sig_bytes).decode("ascii")

        # write signature debug files
        sig_bin_file = os.path.join(debug_dir, f"signature_NFTS_{i}.bin") # LINHA COMENTADA
        sig_b64_file = os.path.join(debug_dir, f"signature_NFTS_{i}.b64") # LINHA COMENTADA
        with open(sig_bin_file, "wb") as sf: # LINHA COMENTADA
            sf.write(sig_bytes) # LINHA COMENTADA
        with open(sig_b64_file, "w", encoding="utf-8") as sf64: # LINHA COMENTADA
            sf64.write(sig_b64) # LINHA COMENTADA
        logger.critical(" assinatura salva em: %s / %s", sig_bin_file, sig_b64_file) # LINHA COMENTADA

        # insert Assinatura element (clean - remove whitespace/newlines)
        assin = find_child(nfts, "Assinatura")
        if assin is None:
            assin = etree.Element("Assinatura")
            nfts.append(assin)
        # strip whitespace and set base64 (single line)
        assin_text = "".join(sig_b64.split())
        assin.text = assin_text

        # Optionally, ensure there are no extraneous namespace declarations on the NFTS subtree
        # (we preserve the rest of the document as-is, since schema expects xmlns="" on children)

    # sign entire document (opcional)
    try:
        if XMLSEC_AVAILABLE:
            logger.critical("Assinando documento inteiro com xmlsec (opcional)...")
            sign_document_xmlsec(root, key_pem_path, cert_pem_path)
            logger.critical("Assinatura do documento adicionada via xmlsec.")
        else:
            logger.critical("xmlsec não disponível — pulando assinatura do documento inteiro.")
    except Exception as e:
        logger.critical("xmlsec falhou (opcional): %s", e)

    # serialize signed XML (force UTF-8 without BOM)
    signed_xml_bytes = etree.tostring(root, encoding="utf-8", xml_declaration=True, pretty_print=False)
    if signed_xml_bytes.startswith(b'\xef\xbb\xbf'):
        signed_xml_bytes = signed_xml_bytes[len(b'\xef\xbb\xbf'):]
    signed_xml_str = signed_xml_bytes.decode("utf-8")

    # build SOAP with CDATA
    soap_bytes = build_soap_envelope(signed_xml_str)
    with open(output_soap_path, "wb") as out_f:
        out_f.write(soap_bytes)

    logger.critical("SOAP TesteEnvioLoteNFTS salvo em: %s", output_soap_path)
    logger.critical("Arquivos debug em: %s", debug_dir) # LINHA COMENTADA

    # cleanup temporary PEMs
    try:
        if os.path.exists(cert_pem_path):
            os.remove(cert_pem_path)
        if os.path.exists(key_pem_path):
            os.remove(key_pem_path)
    except Exception:
        pass

# ---------------- CLI ----------------

def print_usage():
    print("Usage:")
    print("  python sign_nfts.py input_nfts.xml cert.pfx senha output_soap.xml")
    print("  python sign_nfts.py --verify NFTS_assinado.xml cert.pfx senha")
    print("  python sign_nfts.py --debug input_nfts.xml cert.pfx senha output_soap.xml")

if __name__ == "__main__":
    args = sys.argv[1:]
    if not args or args[0] in ("-h", "--help"):
        print_usage()
        sys.exit(0)

    # --verify mode
    if args[0] == "--verify":
        if len(args) != 4:
            print("Usage: python sign_nfts.py --verify NFTS_assinado.xml cert.pfx senha")
            sys.exit(1)
        xml_path = args[1]
        pfx = args[2]
        pwd = args[3]
        # O modo --verify AINDA salva arquivos de debug (canonical), pois são úteis para a verificação.
        verify_signed_nfts(xml_path, pfx, pwd)
        sys.exit(0)

    # --debug mode toggles extra debug files
    debug_mode = False
    if args[0] == "--debug":
        # Nota: Mesmo no modo --debug, os arquivos NÃO serão salvos devido às linhas comentadas em sign_file
        debug_mode = True
        args = args[1:]

    if len(args) != 4:
        print_usage()
        sys.exit(1)

    input_xml, pfx_file, senha, output_soap = args
    sign_file(input_xml, pfx_file, senha, output_soap, debug=debug_mode)