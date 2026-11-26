"""Ephemeral cross-platform DH 5 plugin Authenticode + manifest signer and packager."""
import argparse
import base64
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import zipfile

PLUGIN_NAME = 'Alch Reaction Timer'
ASSEMBLY_BASE = 'VoK.ReactionTimer'
DEFAULT_PRODUCT_URL = 'https://github.com/Hagaygur/ReactionTimer'


def _tool_env():
    env = dict(os.environ)
    if sys.platform == 'win32':
        # Prevent MSYS2/Git Bash OpenSSL from rewriting '/CN=Warned' as a Windows path.
        env['MSYS_NO_PATHCONV'] = '1'
        env['MSYS2_ARG_CONV_EXCL'] = '*'
    return env


def _find_openssl():
    candidates = [
        shutil.which('openssl'),
        r'C:\Program Files\Git\usr\bin\openssl.exe',
        r'C:\Program Files\Git\mingw64\bin\openssl.exe',
    ]
    for candidate in candidates:
        if candidate and Path(candidate).is_file():
            return str(candidate)
    raise RuntimeError('openssl executable not found.')


def _find_signtool():
    found = shutil.which('signtool')
    if found:
        return found
    kits = Path(os.environ.get('ProgramFiles(x86)', r'C:\Program Files (x86)')) / 'Windows Kits' / '10' / 'bin'
    if kits.is_dir():
        matches = sorted(kits.glob('*/x64/signtool.exe'), reverse=True)
        if matches:
            return str(matches[0])
    raise RuntimeError('signtool.exe not found under Windows Kits 10.')


def _find_osslsigncode():
    found = shutil.which('osslsigncode')
    if found:
        return found
    raise RuntimeError('osslsigncode executable not found (install osslsigncode on Linux).')


def _load_key_bytes(key_file_arg):
    key_file = key_file_arg or os.environ.get('DH_AUTHOR_KEY_FILE')
    if key_file:
        path = Path(key_file)
        if not path.is_file():
            raise RuntimeError(f'Author key file not found: {key_file}')
        return path.read_bytes()

    key_b64 = os.environ.get('DH_AUTHOR_KEY_B64') or os.environ.get('DH5_AUTHOR_KEY_B64')
    if key_b64:
        stripped = key_b64.strip()
        if stripped.startswith('-----BEGIN'):
            return stripped.encode('utf-8')
        return base64.b64decode(stripped)
    return None


def _run_quiet(cmd, env):
    result = subprocess.run(cmd, env=env, capture_output=True)
    if result.returncode != 0:
        raise RuntimeError(f'{Path(cmd[0]).name} failed with exit code {result.returncode}')
    return result.stdout


def _sign_dll(openssl, key_path, cert_path, dll_path, temp_dir, env):
    cnf_path = temp_dir / 'openssl.cnf'
    cnf_path.write_text(
        '[req]\n'
        'distinguished_name = req_dn\n'
        'x509_extensions = v3_ext\n'
        'prompt = no\n\n'
        '[req_dn]\n'
        'CN = Warned\n\n'
        '[v3_ext]\n'
        'basicConstraints = critical, CA:false\n'
        'keyUsage = critical, digitalSignature\n'
        'extendedKeyUsage = codeSigning\n',
        encoding='utf-8',
    )
    _run_quiet(
        [openssl, 'req', '-x509', '-new', '-key', str(key_path), '-days', '730',
         '-config', str(cnf_path), '-subj', '/CN=Warned', '-out', str(cert_path)],
        env,
    )
    if sys.platform == 'win32':
        pfx_path = temp_dir / 'ephemeral.pfx'
        _run_quiet(
            [openssl, 'pkcs12', '-export',
             '-keypbe', 'PBE-SHA1-3DES', '-certpbe', 'PBE-SHA1-3DES', '-macalg', 'sha1',
             '-out', str(pfx_path), '-inkey', str(key_path), '-in', str(cert_path), '-passout', 'pass:'],
            env,
        )
        signtool = _find_signtool()
        _run_quiet([signtool, 'sign', '/fd', 'SHA256', '/f', str(pfx_path), str(dll_path)], env)
    else:
        osslsigncode = _find_osslsigncode()
        signed_dll = temp_dir / f'{ASSEMBLY_BASE}.signed.dll'
        _run_quiet(
            [osslsigncode, 'sign', '-certs', str(cert_path), '-key', str(key_path),
             '-h', 'sha256', '-in', str(dll_path), '-out', str(signed_dll)],
            env,
        )
        shutil.move(str(signed_dll), str(dll_path))


def package_dh5(input_dir, output_zip, version, entitlement_path, entitlement_sig_path,
                key_file=None, allow_unsigned=False, product_info_url=DEFAULT_PRODUCT_URL):
    input_dir = Path(input_dir)
    output_zip = Path(output_zip)
    dll_src = input_dir / f'{ASSEMBLY_BASE}.dll'
    deps_src = input_dir / f'{ASSEMBLY_BASE}.deps.json'
    if not dll_src.is_file() or not deps_src.is_file():
        raise FileNotFoundError(f'Missing DH 5 build outputs in {input_dir}')

    key_bytes = _load_key_bytes(key_file)
    if key_bytes is None and not allow_unsigned:
        raise RuntimeError(
            'DH 5 signing key is required (set --key-file, DH_AUTHOR_KEY_FILE, or DH_AUTHOR_KEY_B64). '
            'Use --allow-unsigned only for untrusted fork PR validation.'
        )

    if key_bytes is not None:
        ent_path = Path(entitlement_path)
        ent_sig_path = Path(entitlement_sig_path)
        if not ent_path.is_file() or not ent_sig_path.is_file():
            raise FileNotFoundError('Missing public DH 5 entitlement pair in ci/.')
        entitlement = json.loads(ent_path.read_text(encoding='utf-8'))

    output_zip.parent.mkdir(parents=True, exist_ok=True)

    with tempfile.TemporaryDirectory(prefix='alch-dh5-sign-') as tmp:
        tmp_dir = Path(tmp)
        stage_dir = tmp_dir / 'stage'
        stage_dir.mkdir()

        dll_stage = stage_dir / f'{ASSEMBLY_BASE}.dll'
        deps_stage = stage_dir / f'{ASSEMBLY_BASE}.deps.json'
        shutil.copyfile(dll_src, dll_stage)
        shutil.copyfile(deps_src, deps_stage)

        meta = {
            'PluginName': PLUGIN_NAME,
            'ProductInfoUrl': product_info_url,
            'UpdateSource': '',
            'CurrentVersion': f'{version}.0',
            'AllowedVersions': [{'Version': f'{version}.0'}],
        }
        (stage_dir / 'plugin.metadata').write_text(json.dumps(meta, indent=2) + '\n', encoding='utf-8')

        if key_bytes is not None:
            env = _tool_env()
            openssl = _find_openssl()
            key_path = tmp_dir / 'author.key'
            key_path.write_bytes(key_bytes)
            if os.name != 'nt':
                os.chmod(key_path, 0o600)

            pem_path = stage_dir / f'{PLUGIN_NAME}.pem'
            pub_pem = _run_quiet([openssl, 'pkey', '-in', str(key_path), '-pubout'], env)
            pem_path.write_bytes(pub_pem)
            pub_der = _run_quiet([openssl, 'pkey', '-pubin', '-in', str(pem_path), '-outform', 'DER'], env)
            derived_fp = hashlib.sha256(pub_der).hexdigest().upper()
            expected_fp = entitlement['authorKeyFingerprint'].upper()
            if derived_fp != expected_fp:
                raise RuntimeError('Author private key does not match entitlement authorKeyFingerprint.')

            cert_path = tmp_dir / 'author.crt'
            _sign_dll(openssl, key_path, cert_path, dll_stage, tmp_dir, env)

            shutil.copyfile(ent_path, stage_dir / f'{PLUGIN_NAME}.entitlement')
            shutil.copyfile(ent_sig_path, stage_dir / f'{PLUGIN_NAME}.entitlement.sig')

            entries = []
            for file_path in sorted(stage_dir.iterdir(), key=lambda p: p.name):
                digest = hashlib.sha256(file_path.read_bytes()).hexdigest().upper()
                entries.append({'path': file_path.name, 'sha256': digest})

            manifest_bytes = json.dumps({
                'formatVersion': 1,
                'pluginName': PLUGIN_NAME,
                'entries': entries,
            }).encode('utf-8')
            manifest_path = stage_dir / f'{PLUGIN_NAME}.manifest'
            manifest_sig_path = stage_dir / f'{PLUGIN_NAME}.manifest.sig'
            manifest_path.write_bytes(manifest_bytes)
            _run_quiet(
                [openssl, 'dgst', '-sha256', '-sign', str(key_path),
                 '-out', str(manifest_sig_path), str(manifest_path)],
                env,
            )
            _run_quiet(
                [openssl, 'dgst', '-sha256', '-verify', str(pem_path),
                 '-signature', str(manifest_sig_path), str(manifest_path)],
                env,
            )

        tmp_zip = tmp_dir / output_zip.name
        with zipfile.ZipFile(tmp_zip, 'w', zipfile.ZIP_DEFLATED) as archive:
            for file_path in sorted(stage_dir.iterdir(), key=lambda p: p.name):
                archive.write(file_path, file_path.name)
        shutil.copyfile(tmp_zip, output_zip)

    status = 'signed' if key_bytes is not None else 'unsigned'
    print(f'Packaged DH 5 plugin ({status}): {output_zip}')
    return output_zip


def main():
    ci_dir = Path(__file__).resolve().parent
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input-dir', type=Path, default=Path('src-dh5/bin/x64/Release/net10.0'))
    parser.add_argument('--output', type=Path, default=None)
    parser.add_argument('--version', default=None)
    parser.add_argument('--key-file', default=None)
    parser.add_argument('--entitlement', type=Path, default=ci_dir / f'{PLUGIN_NAME}.entitlement')
    parser.add_argument('--entitlement-sig', type=Path, default=ci_dir / f'{PLUGIN_NAME}.entitlement.sig')
    parser.add_argument('--product-info-url', default=DEFAULT_PRODUCT_URL)
    parser.add_argument('--allow-unsigned', action='store_true')
    args = parser.parse_args()

    version = args.version
    if not version:
        version_file = Path('dist/version.json')
        version = json.loads(version_file.read_text(encoding='utf-8'))['version'] if version_file.is_file() else '1.0.0'
    output = args.output or Path(f'dist/ReactionTimer-{version}-dh5-plugin.zip')
    package_dh5(
        args.input_dir,
        output,
        version,
        args.entitlement,
        args.entitlement_sig,
        key_file=args.key_file,
        allow_unsigned=args.allow_unsigned,
        product_info_url=args.product_info_url,
    )


if __name__ == '__main__':
    main()
