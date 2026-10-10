"""Generate local release signing key in ignored build/; no system trust changes."""
from datetime import datetime, timedelta, timezone
from pathlib import Path
from cryptography import x509
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.hazmat.primitives.serialization import pkcs12
from cryptography.x509.oid import NameOID, ExtendedKeyUsageOID
root = Path(__file__).resolve().parents[1] / 'build/signing'
root.mkdir(parents=True, exist_ok=True)
if not (root / 'local-signing.pfx').exists():
    key = rsa.generate_private_key(public_exponent=65537, key_size=3072)
    subject = x509.Name([x509.NameAttribute(NameOID.COMMON_NAME, 'PEICD TarGz Local')])
    now = datetime.now(timezone.utc)
    cert = (x509.CertificateBuilder().subject_name(subject).issuer_name(subject).public_key(key.public_key())
            .serial_number(x509.random_serial_number()).not_valid_before(now - timedelta(days=1))
            .not_valid_after(now + timedelta(days=3650))
            .add_extension(x509.BasicConstraints(ca=False, path_length=None), critical=True)
            .add_extension(x509.KeyUsage(digital_signature=True, content_commitment=False, key_encipherment=False,
                                        data_encipherment=False, key_agreement=False, key_cert_sign=False,
                                        crl_sign=False, encipher_only=None, decipher_only=None), critical=True)
            .add_extension(x509.ExtendedKeyUsage([ExtendedKeyUsageOID.CODE_SIGNING]), critical=True)
            .sign(key, hashes.SHA256()))
    (root / 'local-signing.pfx').write_bytes(pkcs12.serialize_key_and_certificates(b'PEICD TarGz', key, cert, None, serialization.NoEncryption()))
    (root / 'local-signing.cer').write_bytes(cert.public_bytes(serialization.Encoding.DER))
print('Local signing material ready (ignored, never embedded private key).')
