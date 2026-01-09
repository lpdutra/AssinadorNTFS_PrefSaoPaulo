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

# Configuração de log - MANTIDO COMO CRITICAL conforme solicitado
logging.basicConfig(level=logging.CRITICAL, format='%(levelname)s: %(message)s')
logger = logging.getLogger("sign_nfts")

NS_SP = "http://www.prefeitura.sp.gov.br/nfts"
NS_SOAP = "http://schemas.xmlsoap.org/soap/envelope/"

# ---------------- LISTA DE ORDENAÇÃO RIGOROSA PARA O XML (XSD PREFEITURA SP) ----------------
NFTS_ELEMENT_ORDER = [
    "TipoDocumento", "ChaveDocumento", "DataPrestacao", "StatusNFTS", "TributacaoNFTS",
    "ValorServicos", "ValorDeducoes", "CodigoServico", "CodigoSubItem", "AliquotaServicos",
    "ISSRetidoTomador", "ISSRetidoIntermediario", "Prestador", "RegimeTributacao",
    "DataPagamento", "Discriminacao", "TipoNFTS", "Tomador",
    "Assinatura",
    #"CodigoCEI",
    #"MatriculaObra",
    #"clocalPrestServ",
    #"cPaisPrestServ",
    #"ValorPIS",
    #"ValorCOFINS", 
    #"ValorINSS",
    #"ValorIR", 
    #"ValorCSLL", 
    #"ValorIPI", 
    #"RetornoComplementarIBSCBS",
    #"ValorInicialCobrado",
    #"ValorFinalCobrado",
    #"ValorMulta",
    #"ValorJuros", 
    "ExigibilidadeSuspensa",
    "PagamentoParceladoAntecipado"
]

# ---------------- util ----------------

def find_child(parent: etree._Element, tagname: str) -> Optional[etree._Element]:
    nodes = parent.xpath('./*[local-name() = $name]', name=tagname)
    return nodes[0] if nodes else None

def read_pkcs12(pfx_path: str, password: Optional[str]) -> Tuple[object, object]:
    with open(pfx_path, "rb") as f:
        data = f.read()
    pwd = None if password in (None, "") else password.encode("utf-8")
    private_key, cert, additional = pkcs12.load_key_and_certificates(data, pwd)
    if private_key is None or cert is None:
        raise RuntimeError("Não foi possível extrair chave privada / certificado do PFX")
    return private_key, cert

# ---------------- Normalizações (Para o Hash) ----------------

def normalize_numeric_string(text: Optional[str]) -> str:
    if text is None: return ""
    clean_text = text.replace('\xa0', ' ').strip()
    if clean_text.isdigit():
        try: return str(int(clean_text))
        except: pass
    return clean_text

def normalize_serie_nfts(text: Optional[str]) -> str:
    if text is None: return "     "
    return text.replace('\xa0', ' ').strip()

def normalize_float_value(text: Optional[str], format_decimals: bool = True) -> str:
    if text is None: return ""
    clean_text = text.replace('\xa0', ' ').replace(',', '.').strip()
    try:
        float_value = float(clean_text)
        return "{:.2f}".format(float_value) if format_decimals else str(float_value)
    except: return clean_text

def normalize_boolean_value(text: Optional[str]) -> str:
    if text is None: return "false"
    clean_text = text.replace('\xa0', ' ').strip().lower()
    return "true" if clean_text in ("true", "1", "s", "sim", "t", "y", "yes") else "false"

# ---------------- Construir string canônica (tpNFTS) COMPLETA ----------------

def build_tpNFTS_bytes(nfts_node: etree._Element) -> bytes:
    clean_tp = copy.deepcopy(nfts_node)
    assin = find_child(clean_tp, "Assinatura")
    if assin is not None:
        clean_tp.remove(assin)

    canonical_order_map = OrderedDict([
        ("TipoDocumento", "str"),
        ("ChaveDocumento", {"InscricaoMunicipal": "str", "SerieNFTS": "serie", "NumeroDocumento": "str"}),
        ("DataPrestacao", "str"), ("StatusNFTS", "str"), ("TributacaoNFTS", "str"),
        ("ValorServicos", "float_currency"), ("ValorDeducoes", "float_currency"),
        ("CodigoServico", "num_str"), ("CodigoSubItem", "num_str"), ("AliquotaServicos", "float"),
        ("ISSRetidoTomador", "bool"), ("ISSRetidoIntermediario", "bool"),
        ("Prestador", {
            "CPFCNPJ": {"CNPJ": "str", "CPF": "str"},
            "InscricaoMunicipal": "str", "RazaoSocialPrestador": "str",
            "Endereco": {
                "TipoLogradouro": "str", "Logradouro": "str", "NumeroEndereco": "str",
                "ComplementoEndereco": "str", "Bairro": "str", "Cidade": "num_str", "UF": "str", "CEP": "num_str"
            },
            "Email": "str",
        }),
        ("RegimeTributacao", "num_str"), ("DataPagamento", "str"), ("Discriminacao", "str"),
        ("TipoNFTS", "num_str"),
        ("Tomador", {"CPFCNPJ": {"CPF": "str", "CNPJ": "str"}, "RazaoSocial": "str"}),
        #("CodigoCEI", "str"),
        #("MatriculaObra", "str"),
        #("clocalPrestServ", "num_str"),
        #("cPaisPrestServ", "num_str"),
        #("ValorPIS", "float_currency"),
        #("ValorCOFINS", "float_currency"), 
        #("ValorINSS", "float_currency"), 
        #("ValorIR", "float_currency"),
        #("ValorCSLL", "float_currency"), 
        #("ValorIPI", "float_currency"),
        #("RetornoComplementarIBSCBS", "str"),
        #("ValorInicialCobrado", "float_currency"), 
        #("ValorFinalCobrado", "float_currency"), 
        #("ValorMulta", "float_currency"),
        #("ValorJuros", "float_currency"), 
        #("ExigibilidadeSuspensa", "str"),
        #("PagamentoParceladoAntecipado", "str"),
    ])

    def build_fragment(node: etree._Element, order_map: OrderedDict) -> list:
        elems = []
        for tag_name, definition in order_map.items():
            original_child = find_child(node, tag_name)
            if original_child is None: continue
            if isinstance(definition, str):
                text_value = original_child.text or ""
                if definition == "num_str": final = normalize_numeric_string(text_value)
                elif definition == "float_currency": final = normalize_float_value(text_value, True)
                elif definition == "float": final = normalize_float_value(text_value, False)
                elif definition == "bool": final = normalize_boolean_value(text_value)
                elif definition == "serie": final = normalize_serie_nfts(text_value)
                else: final = text_value.replace('\xa0', ' ').strip()
                if final == "": continue
                el = etree.Element(tag_name); el.text = final; elems.append(el)
            elif isinstance(definition, dict):
                nested = build_fragment(original_child, definition)
                if nested:
                    parent = etree.Element(tag_name); parent.extend(nested); elems.append(parent)
        return elems

    canonical_root = etree.Element("tpNFTS")
    canonical_root.extend(build_fragment(clean_tp, canonical_order_map))
    b = etree.tostring(canonical_root, encoding="utf-8", xml_declaration=False, pretty_print=False)
    if b.startswith(b'\xef\xbb\xbf'): b = b[len(b'\xef\xbb\xbf'):]
    return b

# ---------------- Assinatura do documento inteiro (xmlsec) ----------------

def sign_document_xmlsec(root: etree._Element, key_pem_path: str, cert_pem_path: str):
    if not XMLSEC_AVAILABLE:
        logger.critical("xmlsec não disponível — pulando assinatura do documento inteiro.")
        return
    signature_node = xmlsec.template.create(root, xmlsec.Transform.EXCL_C14N, xmlsec.Transform.RSA_SHA1, ns='ds')
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
    logger.debug("Assinatura do documento adicionada via xmlsec.")

# ---------------- Main Flow ----------------

def sign_file(input_xml_path: str, pfx_path: str, pfx_pass: str, output_soap_path: str):
    logger.debug("Lendo XML de entrada: %s", input_xml_path)
    parser = etree.XMLParser(remove_blank_text=True)
    tree = etree.parse(input_xml_path, parser)
    root = tree.getroot()

    logger.debug("Extraindo chave privada e certificado do PFX...")
    private_key, cert = read_pkcs12(pfx_path, pfx_pass)

    tmpdir = tempfile.gettempdir()
    cert_pem = os.path.join(tmpdir, "tmp_cert_nfts.pem")
    key_pem = os.path.join(tmpdir, "tmp_key_nfts.pem")
    with open(cert_pem, "wb") as f: f.write(cert.public_bytes(Encoding.PEM))
    with open(key_pem, "wb") as f: f.write(private_key.private_bytes(Encoding.PEM, PrivateFormat.PKCS8, NoEncryption()))

    nfts_nodes = root.xpath('//*[local-name()="NFTS"]')
    if not nfts_nodes:
        logger.critical("Nenhum elemento <NFTS> encontrado no XML.")
        return
    logger.debug("Encontrados %d NFTS nodes", len(nfts_nodes))

    for i, nfts in enumerate(nfts_nodes, start=1):
        logger.debug("Processando NFTS #%d ...", i)
        canonical_bytes = build_tpNFTS_bytes(nfts)
        
        #DEBUG LINHA COMENTADA
        # --- EXPORTAÇÃO DA STRING CANÔNICA ---
        # export_folder = r"C:\temp\nfts"
        # export_file = os.path.join(export_folder, "canonica.lst")
        # try:
        #     if not os.path.exists(export_folder):
        #         os.makedirs(export_folder)
        #     with open(export_file, "wb") as f_can:
        #         f_can.write(canonical_bytes)
        #     logger.debug("String canônica exportada para: %s", export_file)
        # except Exception as e:
        #     logger.critical("ERRO ao exportar string canônica: %s", str(e))

        sig_bytes = private_key.sign(canonical_bytes, padding.PKCS1v15(), hashes.SHA1())
        sig_b64 = base64.b64encode(sig_bytes).decode("ascii")

        elements_dict = {el.tag: el for el in nfts}
        assin_el = etree.Element("Assinatura")
        assin_el.text = "".join(sig_b64.split())
        elements_dict["Assinatura"] = assin_el

        for el in list(nfts): nfts.remove(el)
        for tag in NFTS_ELEMENT_ORDER:
            if tag in elements_dict: nfts.append(elements_dict[tag])

    if XMLSEC_AVAILABLE:
        logger.debug("Assinando documento inteiro com xmlsec (opcional)...")
        sign_document_xmlsec(root, key_pem, cert_pem)
    else:
        logger.critical("xmlsec não disponível — pulando assinatura do documento inteiro.")

    signed_xml_str = etree.tostring(root, encoding="utf-8", xml_declaration=True).decode("utf-8")
    envelope = etree.Element("{%s}Envelope" % NS_SOAP, nsmap={'soap': NS_SOAP})
    body = etree.SubElement(envelope, "{%s}Body" % NS_SOAP)
    req = etree.SubElement(body, "{%s}TesteEnvioLoteNFTSRequest" % NS_SP)
    etree.SubElement(req, "{%s}VersaoSchema" % NS_SP).text = "2"
    msg = etree.SubElement(req, "{%s}MensagemXML" % NS_SP)
    msg.text = etree.CDATA(signed_xml_str)
    
    with open(output_soap_path, "wb") as f:
        f.write(etree.tostring(envelope, encoding="utf-8", xml_declaration=True, pretty_print=True))
    
    logger.debug("SOAP TesteEnvioLoteNFTS salvo em: %s", output_soap_path)

    try:
        if os.path.exists(cert_pem): os.remove(cert_pem)
        if os.path.exists(key_pem): os.remove(key_pem)
    except: pass

if __name__ == "__main__":
    if len(sys.argv) == 5:
        sign_file(sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4])
    else:
        print("Uso: python sign_nfts.py input.xml cert.pfx senha output.xml")