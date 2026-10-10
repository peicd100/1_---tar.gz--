"""Verify MSIX block map, identity, resources, and packaged public-only material."""
from pathlib import Path
import base64
import hashlib
import json
import sys
import zipfile
from xml.etree import ElementTree as ET
ROOT = Path(__file__).resolve().parents[1]
package = ROOT / ('dist/TarGz-' + (sys.argv[1] if len(sys.argv) > 1 else '1.0.1') + '-x64.msix')
with zipfile.ZipFile(package) as archive:
    names = archive.namelist()
    assert not any(n.lower().endswith(('.pfx', '.key', '.py', '.ps1')) for n in names)
    manifest = ET.fromstring(archive.read('AppxManifest.xml'))
    ns = {'p': 'http://schemas.microsoft.com/appx/manifest/foundation/windows10',
          'd': 'http://schemas.microsoft.com/appx/manifest/desktop/windows10/5',
          'c': 'http://schemas.microsoft.com/appx/manifest/com/windows10'}
    assert manifest.find('p:Identity', ns).attrib['Name'] == 'PEICD.TarGz'
    cls = manifest.find('.//c:Class', ns).attrib['Id']
    verbs = manifest.findall('.//d:Verb', ns)
    assert len(verbs) == 2 and all(v.attrib['Clsid'] == cls for v in verbs)
    assert {x.attrib['Type'] for x in manifest.findall('.//d:ItemType', ns)} == {'*', 'Directory'}
    blockmap = ET.fromstring(archive.read('AppxBlockMap.xml'))
    bns = {'b': 'http://schemas.microsoft.com/appx/2010/blockmap'}
    count = 0
    for file in blockmap.findall('b:File', bns):
        data = archive.read(file.attrib['Name'].replace('\\', '/'))
        assert len(data) == int(file.attrib['Size'])
        chunks = [data[i:i + 65536] for i in range(0, len(data), 65536)]
        blocks = file.findall('b:Block', bns)
        # Empty MSIX payload file may have one empty block.
        if not chunks and blocks:
            chunks = [b'']
        assert len(blocks) == len(chunks)
        for block, chunk in zip(blocks, chunks):
            assert base64.b64decode(block.attrib['Hash']) == hashlib.sha256(chunk).digest()
            count += 1
    assert archive.read('AppxSignature.p7x').startswith(b'PKCX')
    assert b'SIL OPEN FONT LICENSE Version 1.1' in archive.read('FONT-LICENSE.txt')
    assert len(archive.read('TarGz.exe')) > 8000000, 'Private font must be embedded'
results = {'result': 'PASS', 'checked_blocks': count, 'files': names,
           'sha256': hashlib.sha256(package.read_bytes()).hexdigest()}
(ROOT / 'build/package-verification.json').write_text(json.dumps(results, indent=2), encoding='utf-8')
print('MSIX identity, file/folder verbs, all payload block hashes, font license, no private key: PASS')
