#!/usr/bin/env python3
"""
Hardware-Locked Cryptographic Licensing System
Seller's Offline Key Generation & Signing Utility
RSA-2048 Digital Signature Generator for ApparelPOS
"""

import sys
import os
import json
import base64
import argparse
from datetime import datetime
from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.asymmetric import rsa, padding
from cryptography.hazmat.primitives import serialization

KEY_DIR = os.path.dirname(os.path.abspath(__file__))
PRIVATE_KEY_PATH = os.path.join(KEY_DIR, "private_key.pem")
PUBLIC_KEY_PATH = os.path.join(KEY_DIR, "public_key.pem")

def generate_keypair():
    """Generates RSA-2048 key pair if not already present."""
    if os.path.exists(PRIVATE_KEY_PATH) and os.path.exists(PUBLIC_KEY_PATH):
        print(f"[*] Keys already exist:\n    - {PRIVATE_KEY_PATH}\n    - {PUBLIC_KEY_PATH}")
        with open(PUBLIC_KEY_PATH, "r", encoding="utf-8") as f:
            pub_pem = f.read()
        print("\n=== C# EMBEDDABLE PUBLIC KEY ===")
        print(pub_pem.strip())
        print("================================")
        return

    print("[*] Generating new RSA-2048 key pair...")
    private_key = rsa.generate_private_key(
        public_exponent=65537,
        key_size=2048
    )

    # Export private key
    pem_private = private_key.private_bytes(
        encoding=serialization.Encoding.PEM,
        format=serialization.PrivateFormat.PKCS8,
        encryption_algorithm=serialization.NoEncryption()
    )
    with open(PRIVATE_KEY_PATH, "wb") as f:
        f.write(pem_private)

    # Export public key
    public_key = private_key.public_key()
    pem_public = public_key.public_bytes(
        encoding=serialization.Encoding.PEM,
        format=serialization.PublicFormat.SubjectPublicKeyInfo
    )
    with open(PUBLIC_KEY_PATH, "wb") as f:
        f.write(pem_public)

    print(f"[+] Private key saved to: {PRIVATE_KEY_PATH}")
    print(f"[+] Public key saved to:  {PUBLIC_KEY_PATH}")
    print("\n=== C# EMBEDDABLE PUBLIC KEY ===")
    print(pem_public.decode("utf-8").strip())
    print("================================")

def load_private_key():
    """Loads RSA private key from disk."""
    if not os.path.exists(PRIVATE_KEY_PATH):
        raise FileNotFoundError(f"Private key not found at {PRIVATE_KEY_PATH}. Run with --init-keys first.")
    with open(PRIVATE_KEY_PATH, "rb") as f:
        return serialization.load_pem_private_key(f.read(), password=None)

def load_public_key():
    """Loads RSA public key from disk."""
    if not os.path.exists(PUBLIC_KEY_PATH):
        raise FileNotFoundError(f"Public key not found at {PUBLIC_KEY_PATH}. Run with --init-keys first.")
    with open(PUBLIC_KEY_PATH, "rb") as f:
        return serialization.load_pem_public_key(f.read())

def issue_license(machine_code: str, customer_name: str, expiry: str, store_name: str, output_path: str = None) -> str:
    """Signs machine code and metadata, producing base64 license payload."""
    machine_code = machine_code.strip().upper()
    if len(machine_code.replace("-", "")) != 16:
        print(f"[!] Warning: Machine code '{machine_code}' does not match standard 16-character format (XXXX-XXXX-XXXX-XXXX).")

    private_key = load_private_key()

    payload_dict = {
        "MachineCode": machine_code,
        "CustomerName": customer_name,
        "StoreName": store_name,
        "ExpiryDate": expiry.strip().upper(),  # 'LIFETIME' or 'YYYY-MM-DD'
        "IssuedAt": datetime.now().strftime("%Y-%m-%d"),
        "LicenseType": "Commercial Offline"
    }

    # Deterministic JSON serialization
    json_bytes = json.dumps(payload_dict, separators=(',', ':'), sort_keys=True).encode('utf-8')

    # Sign using RSA-SHA256 PKCS#1 v1.5
    signature = private_key.sign(
        json_bytes,
        padding.PKCS1v15(),
        hashes.SHA256()
    )

    payload_b64 = base64.b64encode(json_bytes).decode('ascii')
    signature_b64 = base64.b64encode(signature).decode('ascii')

    license_string = f"{payload_b64}.{signature_b64}"

    if output_path:
        with open(output_path, "w", encoding="utf-8") as f:
            f.write(license_string)
        print(f"[+] License saved to file: {output_path}")

    return license_string

def verify_license(license_str_or_file: str) -> bool:
    """Verifies a license string or license file."""
    if os.path.exists(license_str_or_file):
        with open(license_str_or_file, "r", encoding="utf-8") as f:
            content = f.read().strip()
    else:
        content = license_str_or_file.strip()

    parts = content.split('.')
    if len(parts) != 2:
        print("[-] Invalid license format. Expected '<base64_payload>.<base64_signature>'")
        return False

    payload_b64, sig_b64 = parts
    try:
        json_bytes = base64.b64decode(payload_b64)
        signature = base64.b64decode(sig_b64)
    except Exception as e:
        print(f"[-] Base64 decoding failed: {e}")
        return False

    public_key = load_public_key()
    try:
        public_key.verify(
            signature,
            json_bytes,
            padding.PKCS1v15(),
            hashes.SHA256()
        )
        payload = json.loads(json_bytes.decode('utf-8'))
        print("[+] Signature is cryptographically VALID!")
        print(f"    - Machine Code:  {payload.get('MachineCode')}")
        print(f"    - Customer:      {payload.get('CustomerName')}")
        print(f"    - Store:         {payload.get('StoreName')}")
        print(f"    - Expiry:        {payload.get('ExpiryDate')}")
        print(f"    - Issued At:     {payload.get('IssuedAt')}")
        return True
    except Exception as e:
        print(f"[-] Signature verification FAILED: {e}")
        return False

def main():
    parser = argparse.ArgumentParser(description="ApparelPOS Offline RSA-2048 License Generator")
    parser.add_argument("--init-keys", action="store_true", help="Generate initial RSA-2048 keypair")
    parser.add_argument("--machine-code", "-m", help="Client Machine Code (e.g. 5A3B-7F1C-9E2D-4B6A)")
    parser.add_argument("--customer", "-c", default="Retail Client", help="Customer/Owner Name")
    parser.add_argument("--store", "-s", default="Apparel Store", help="Store Name")
    parser.add_argument("--expiry", "-e", default="LIFETIME", help="Expiry date 'LIFETIME' or 'YYYY-MM-DD'")
    parser.add_argument("--output", "-o", default="license.lic", help="Output license file path (default: license.lic)")
    parser.add_argument("--verify", "-v", help="Verify a license string or license file")

    args = parser.parse_args()

    if args.init_keys:
        generate_keypair()
        return

    if args.verify:
        verify_license(args.verify)
        return

    if not args.machine_code:
        # Check if keys exist, if not generate them first
        if not os.path.exists(PRIVATE_KEY_PATH):
            generate_keypair()
        
        # Interactive mode
        print("=== ApparelPOS Offline License Generator (Interactive) ===")
        m_code = input("Enter Machine Code (from Activation Screen): ").strip().upper()
        if not m_code:
            print("[-] Machine Code cannot be empty.")
            return
        cust = input("Enter Customer Name [Retail Client]: ").strip() or "Retail Client"
        st = input("Enter Store Name [Apparel Store]: ").strip() or "Apparel Store"
        exp = input("Enter Expiry Date (LIFETIME or YYYY-MM-DD) [LIFETIME]: ").strip().upper() or "LIFETIME"
        out = input("Enter Output File Path [license.lic]: ").strip() or "license.lic"

        lic = issue_license(m_code, cust, exp, st, out)
        print("\n==================== LICENSE KEY ====================")
        print(lic)
        print("=====================================================")
        print(f"[+] License written to {out}. Copy either the key string or the file to activate.")
        return

    lic = issue_license(args.machine_code, args.customer, args.expiry, args.store, args.output)
    print("\n==================== LICENSE KEY ====================")
    print(lic)
    print("=====================================================")

if __name__ == "__main__":
    main()
