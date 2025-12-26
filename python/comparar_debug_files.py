#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Script para comparar arquivos de debug entre C# e Python
"""

import os
import sys

def compare_files(file1, file2, description):
    """Compara dois arquivos binários"""
    if not os.path.exists(file1):
        print(f"❌ {description}: Arquivo 1 não encontrado: {file1}")
        return False
    if not os.path.exists(file2):
        print(f"❌ {description}: Arquivo 2 não encontrado: {file2}")
        return False
    
    with open(file1, 'rb') as f1:
        data1 = f1.read()
    with open(file2, 'rb') as f2:
        data2 = f2.read()
    
    if data1 == data2:
        print(f"✅ {description}: IGUAIS ({len(data1)} bytes)")
        return True
    else:
        print(f"❌ {description}: DIFERENTES")
        print(f"   Arquivo 1: {len(data1)} bytes")
        print(f"   Arquivo 2: {len(data2)} bytes")
        
        # Mostrar primeiros bytes diferentes
        min_len = min(len(data1), len(data2))
        for i in range(min_len):
            if data1[i] != data2[i]:
                print(f"   Primeira diferença no byte {i}:")
                start = max(0, i-5)
                end = min(min_len, i+6)
                print(f"   Arquivo 1 [{start}:{end}]: {data1[start:end].hex()}")
                print(f"   Arquivo 2 [{start}:{end}]: {data2[start:end].hex()}")
                break
        
        return False

def main():
    # Diretórios
    csharp_dir = r"D:\Workspace\FESP\Projeto_NTFS\processamento\nfts_debug"
    python_dir = r"D:\Workspace\FESP\Projeto_NTFS\python\nfts_debug"
    
    print("="*70)
    print("COMPARAÇÃO DE ARQUIVOS DE DEBUG - C# vs Python")
    print("="*70)
    print()
    
    # Comparar canonical
    print("1. CANONICAL (string a ser assinada)")
    print("-"*70)
    compare_files(
        os.path.join(csharp_dir, "canonical_NFTS_1.bin"),
        os.path.join(python_dir, "canonical_NFTS_1.bin"),
        "canonical_NFTS_1.bin"
    )
    print()
    
    # Comparar hash
    print("2. HASH SHA1")
    print("-"*70)
    hash_equal = compare_files(
        os.path.join(csharp_dir, "hash_NFTS_1.bin"),
        os.path.join(python_dir, "hash_NFTS_1.bin"),
        "hash_NFTS_1.bin"
    )
    
    # Mostrar hash em hex
    hash_file_csharp = os.path.join(csharp_dir, "hash_NFTS_1.bin")
    hash_file_python = os.path.join(python_dir, "hash_NFTS_1.bin")
    
    if os.path.exists(hash_file_csharp):
        with open(hash_file_csharp, 'rb') as f:
            hash_csharp = f.read()
        print(f"   C# Hash (hex):     {hash_csharp.hex()}")
    
    if os.path.exists(hash_file_python):
        with open(hash_file_python, 'rb') as f:
            hash_python = f.read()
        print(f"   Python Hash (hex): {hash_python.hex()}")
    print()
    
    # Comparar assinatura
    print("3. ASSINATURA")
    print("-"*70)
    
    # C# gera signature_NFTS_1.bin
    # Python gera signature_NFTS_1.bin e possivelmente signature_NFTS_1_method1.bin
    
    sig_equal = compare_files(
        os.path.join(csharp_dir, "signature_NFTS_1.bin"),
        os.path.join(python_dir, "signature_NFTS_1.bin"),
        "signature_NFTS_1.bin"
    )
    
    # Verificar se existe method1
    sig_method1 = os.path.join(python_dir, "signature_NFTS_1_method1.bin")
    if os.path.exists(sig_method1):
        print()
        compare_files(
            os.path.join(csharp_dir, "signature_NFTS_1.bin"),
            sig_method1,
            "signature_NFTS_1.bin (C#) vs method1 (Python)"
        )
    
    print()
    print("="*70)
    print("CONCLUSÃO")
    print("="*70)
    
    if hash_equal:
        print("✅ Os hashes são IDÊNTICOS")
        print("   → O problema NÃO está no cálculo do hash ou na construção do canonical")
        if not sig_equal:
            print("❌ As assinaturas são DIFERENTES")
            print("   → O problema está na OPERAÇÃO DE ASSINATURA")
            print("   → Possíveis causas:")
            print("      - Diferença na implementação RSA PKCS#1 v1.5")
            print("      - Diferença no formato da chave privada")
            print("      - Diferença na forma como o hash é 'wrapped' antes de assinar")
    else:
        print("❌ Os hashes são DIFERENTES")
        print("   → O problema está no cálculo do hash ou na construção do canonical")

if __name__ == "__main__":
    main()
