"""Create and verify a recipient ZIP from existing, hash-checked build outputs."""
from pathlib import Path
import argparse
import hashlib
import os
import re
import zipfile
from xml.etree import ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]


def sha(data):
    return hashlib.sha256(data).hexdigest()


def release(version):
    if not re.fullmatch(r'\d+\.\d+\.\d+', version):
        raise ValueError('Version must be major.minor.patch')
    dist = ROOT / 'dist'
    msix = dist / ('TarGz-' + version + '-x64.msix')
    expected = {}
    for line in (dist / 'SHA256SUMS.txt').read_text('ascii').splitlines():
        digest, name = line.split('  ', 1)
        expected[name] = digest.lower()
    for path in (dist / 'Setup.exe', msix):
        if sha(path.read_bytes()) != expected[path.name]:
            raise ValueError('Build checksum mismatch: ' + path.name)
    with zipfile.ZipFile(msix) as package:
        manifest = ET.fromstring(package.read('AppxManifest.xml'))
        identity = manifest.find('{http://schemas.microsoft.com/appx/manifest/foundation/windows10}Identity')
        if identity.attrib['Version'] != version + '.0':
            raise ValueError('MSIX version mismatch')
    if not (dist / 'signer.cer').is_file():
        raise ValueError('Missing public certificate')
    guide = f'''壓縮成 tar.gz — v{version}（Windows 11 x64）

安裝：先將整個 ZIP 解壓縮，再執行 Setup.exe。
首次安裝勾選「允許管理此工具的簽章憑證」，按「安裝」，依 Windows 提示授權。
本版使用自簽憑證，Windows 可能顯示未知發行者／SmartScreen 提示。
請先確認提供者與檔案來源；本程式不會關閉 Windows 保護功能。

使用：在檔案總管選取檔案或資料夾，右鍵 →「壓縮成 tar.gz」。
輸出名稱包含開始壓縮時的本機日期與時間，例如：資料夾_20261014_153025.tar.gz。
單檔保留原副檔名；多選使用「選取項目_數量個_日期_時間.tar.gz」。
同一秒重名時另加 _v0.0.N，不覆蓋既有壓縮檔。
選單未刷新時，儲存工作後登出並重新登入 Windows。

更新：下載新版 ZIP、解壓縮，等壓縮工作完成並關閉程式，再執行新版 Setup.exe。
安裝程式會辨識已安裝版本並升級；同版可修復註冊，不允許降級。
目前採手動下載更新，不含自動檢查更新。

解除安裝：執行 Setup.exe 選「解除安裝」，或使用 Windows 的已安裝應用程式。
如需一併清除本工具憑證信任，請在 Setup.exe 勾選憑證管理授權。
由 Windows 設定解除安裝時會保留憑證信任，可再以 Setup.exe 清除。
產生的壓縮檔不會因解除安裝而刪除。

無需安裝 Python、Conda 或額外壓縮軟體。Setup.exe 已內含安裝套件。
signer.cer 是公開簽章憑證；請透過 Setup.exe 管理信任。
SHA256SUMS.txt 可用 PowerShell Get-FileHash -Algorithm SHA256 比對。
字體作者與授權請見 FONT-AUTHOR.md、FONT-LICENSE.txt。
'''
    payload = {
        'Setup.exe': (dist / 'Setup.exe').read_bytes(),
        '使用說明.txt': guide.encode('utf-8'),
        'signer.cer': (dist / 'signer.cer').read_bytes(),
        'FONT-AUTHOR.md': (ROOT / 'assets/FONT-AUTHOR.md').read_bytes(),
        'FONT-LICENSE.txt': (ROOT / 'assets/FONT-LICENSE.txt').read_bytes(),
    }
    payload['SHA256SUMS.txt'] = ''.join(
        sha(data) + '  ' + name + '\n' for name, data in payload.items()).encode('utf-8')
    output = dist / ('TarGz-' + version + '-Windows-x64.zip')
    temporary = output.with_suffix('.zip.new')
    try:
        with zipfile.ZipFile(temporary, 'w', compression=zipfile.ZIP_DEFLATED) as archive:
            for name, data in payload.items():
                info = zipfile.ZipInfo(name, (2020, 1, 1, 0, 0, 0))
                info.compress_type = zipfile.ZIP_DEFLATED
                archive.writestr(info, data)
        with zipfile.ZipFile(temporary) as archive:
            if set(archive.namelist()) != set(payload) or archive.testzip() is not None:
                raise ValueError('Release ZIP verification failed')
            for name, data in payload.items():
                if archive.read(name) != data:
                    raise ValueError('Release content mismatch: ' + name)
        os.replace(temporary, output)
    finally:
        if temporary.exists():
            temporary.unlink()
    checksum = sha(output.read_bytes())
    output.with_suffix('.zip.sha256').write_text(checksum + '  ' + output.name + '\n', encoding='ascii')
    print(str(output))
    print('SHA256 ' + checksum)
    print('PASS: six allowlisted files; CRC and byte-for-byte contents verified')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--version', default='1.0.2')
    release(parser.parse_args().version)
