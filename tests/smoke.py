"""Real tar round-trip tests; all test inputs/outputs stay under build/tests."""
import hashlib
import json
from pathlib import Path
import subprocess
import tarfile
import time
import os
import re
from datetime import datetime, timedelta

ROOT = Path(__file__).resolve().parents[1]
EXE = ROOT / 'build/package/TarGz.exe'
WORK = ROOT / 'build/tests' / time.strftime('%Y%m%d-%H%M%S')
WORK.mkdir(parents=True)
results = []

def run(paths, expected=0, cancel=0):
    report = WORK / ('report-%d.txt' % len(results))
    cmd = [str(EXE), '--compress', '--report', str(report)]
    if cancel:
        cmd += ['--cancel-after-ms', str(cancel)]
    started = datetime.now().replace(microsecond=0)
    process = subprocess.run(cmd + ['--'] + [str(p) for p in paths], timeout=90)
    finished = datetime.now()
    text = report.read_text('utf-8')
    assert process.returncode == expected, (process.returncode, text)
    assert not list(WORK.rglob('*.partial')), 'Partial archive leaked'
    if expected == 0:
        output = Path(text)
        unique = list(dict.fromkeys(paths))
        stem = unique[0].name if len(unique) == 1 else '選取項目_%d個' % len(unique)
        match = re.fullmatch(re.escape(stem) + r'_(\d{8}_\d{6})(?:_v0\.0\.\d+)?\.tar\.gz', output.name)
        assert match, output.name
        assert started <= datetime.strptime(match[1], '%Y%m%d_%H%M%S') <= finished
        return output
    return text

def record(name):
    results.append({'test': name, 'result': 'PASS'})

folder = WORK / "中文 空白 [測試] & 100% ! '引號"
folder.mkdir()
contents = {'文字🌙.txt': '清松手寫體 9 文青\n'.encode(), '-dash.txt': b'dash', '.hidden': b'hidden', 'empty': b''}
for name, value in contents.items():
    (folder / name).write_bytes(value)
(folder / '空資料夾').mkdir()
archive = run([folder])
with tarfile.open(archive) as tar:
    for name, value in contents.items():
        assert tar.extractfile('./' + folder.name + '/' + name).read() == value
    assert tar.getmember('./' + folder.name + '/空資料夾').isdir()
record('single directory: Unicode, spaces, punctuation, hidden, empty files and folder, byte-for-byte round trip')

old_hash = hashlib.sha256(archive.read_bytes()).hexdigest()
# Preoccupy the possible start times, so crossing a second cannot weaken the collision check.
occupied = []
for seconds in range(120):
    target = WORK / (folder.name + '_' + (datetime.now() + timedelta(seconds=seconds)).strftime('%Y%m%d_%H%M%S') + '.tar.gz')
    if not target.exists():
        target.write_bytes(b'existing archive must not be overwritten')
        occupied.append(target)
second = run([folder])
assert second.name.endswith('_v0.0.1.tar.gz')
assert hashlib.sha256(archive.read_bytes()).hexdigest() == old_hash
assert all(p.read_bytes() == b'existing archive must not be overwritten' for p in occupied)
record('collision: preserves existing archive and increments version')

multi = run([folder / '文字🌙.txt', folder / '-dash.txt', folder / '空資料夾', folder / '文字🌙.txt'])
with tarfile.open(multi) as tar:
    names = tar.getnames()
    assert len(names) == 3, names
    assert tar.extractfile('./文字🌙.txt').read() == contents['文字🌙.txt']
    assert tar.extractfile('./-dash.txt').read() == b'dash'
record('multiple siblings: one archive, deduplicated selection, option-like filename')

single = run([folder / '文字🌙.txt'])
with tarfile.open(single) as tar:
    assert tar.extractfile('./文字🌙.txt').read() == contents['文字🌙.txt']
record('single file: original extension plus local date/time, byte-for-byte round trip')

assert '同一個' in run([folder, folder / '文字🌙.txt'], expected=1)
record('reject mixed parents without partial output')
assert '找不到' in run([WORK / 'missing.txt'], expected=1)
record('reject missing input')
assert '根目錄' in run([Path(WORK.anchor)], expected=1)
record('reject drive root')
assert '請選取' in run([], expected=1)
record('reject empty selection')

large = WORK / 'cancel-test.bin'
with large.open('wb') as stream:
    for _ in range(16):
        stream.write(os.urandom(1024 * 1024))
before = hashlib.sha256(large.read_bytes()).hexdigest()
assert 'OperationCanceledException' in run([large], expected=3, cancel=30)
assert not list(WORK.glob('cancel-test.bin_*.tar.gz'))
assert hashlib.sha256(large.read_bytes()).hexdigest() == before
record('cancellation: worker stopped, partial removed, source preserved')

# Command-line length is independent of selection size: tar receives NUL file list.
batch = WORK / 'many files'
batch.mkdir()
paths = []
for i in range(180):
    path = batch / ('長檔名 空白_' + str(i) + '_' + 'x' * 48 + '.txt')
    path.write_bytes(str(i).encode())
    paths.append(path)
# This CLI is bounded by Windows argv; Explorer itself uses a manifest.
many = run(paths[:100])
with tarfile.open(many) as tar:
    assert len(tar.getnames()) == 100
record('100 selected files: PAX UTF-8 entries')
(WORK / 'results.json').write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding='utf-8')
(ROOT / 'build/test-results.json').write_text(json.dumps({'directory': str(WORK.relative_to(ROOT)), 'tests': results}, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(results, ensure_ascii=False, indent=2))
