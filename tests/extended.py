"""Targeted race/link checks; complements cached successful smoke tests."""
from pathlib import Path
import subprocess
import time
import hashlib
import tarfile
import json
ROOT = Path(__file__).resolve().parents[1]
WORK = ROOT / 'build/extended' / time.strftime('%Y%m%d-%H%M%S')
WORK.mkdir(parents=True)
exe = ROOT / 'build/package/TarGz.exe'
source = WORK / 'race.txt'
source.write_bytes(b'concurrent archive content' * 2048)
reports = [WORK / ('report-%d.txt' % i) for i in range(2)]
processes = [subprocess.Popen([str(exe), '--compress', '--report', str(r), '--', str(source)]) for r in reports]
assert all(p.wait(timeout=30) == 0 for p in processes)
outputs = [Path(r.read_text('utf-8')) for r in reports]
assert len(set(outputs)) == 2
for output in outputs:
    with tarfile.open(output) as tar:
        assert tar.extractfile('./race.txt').read() == source.read_bytes()
print('Concurrent same-target compression: unique outputs and correct data PASS')

folder = WORK / 'junction-parent'
folder.mkdir()
link = folder / 'linked'
script = "param($Link,$Target) $ErrorActionPreference='Stop'; New-Item -ItemType Junction -Path $Link -Target $Target | Out-Null"
ps = WORK / 'junction.ps1'
ps.write_text(script, encoding='utf-8')
result = subprocess.run(['powershell.exe', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', str(ps), '-Link', str(link), '-Target', str(WORK)], capture_output=True)
if result.returncode != 0 or not link.is_dir():
    junction = 'SKIP: test environment could not create junction'
    (WORK / 'junction-error.txt').write_bytes(result.stderr)
    print(junction)
else:
    report = WORK / 'link-report.txt'
    result = subprocess.run([str(exe), '--compress', '--report', str(report), '--', str(folder)], timeout=30)
    assert result.returncode == 1 and 'junction' in report.read_text('utf-8')
    assert not list(WORK.glob('*.partial')) and not list(folder.glob('*.partial'))
    junction = 'PASS'
    print('Junction: stops without following cycle, no partial output PASS')
# Do not recursively enumerate/remove the junction. Keep evidence under ignored build/.
(ROOT / 'build/extended-results.json').write_text(json.dumps({'concurrent_output': 'PASS', 'junction_cycle': junction}, indent=2), encoding='utf-8')
