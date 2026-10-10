"""Small deterministic, single-writer PEICD transaction and handoff validator."""
import hashlib
import json
import os
from pathlib import Path
import sys
import tarfile
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.peicd100/codex'
PACKS = ROOT / '.peicd100/codex_compressed'

def sha(data):
    return hashlib.sha256(data).hexdigest()

def atomic(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    temp = path.with_suffix(path.suffix + '.new')
    temp.write_bytes(data)
    os.replace(temp, path)

def encoded(value):
    return (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode('utf-8')

def verify():
    commit = json.loads((BASE / 'MEMORY_COMMIT.json').read_text('utf-8'))
    raw = (BASE / 'MEMORY_INDEX.yaml').read_bytes()
    index = json.loads(raw)  # JSON is valid YAML.
    assert commit['memory_state'] == 'CLEAN'
    assert commit['digest'] == sha(raw)
    assert commit['generation'] == index['generation']
    for item in index['manifest']:
        assert sha((BASE / item['path']).read_bytes()) == item['sha256'], item['path']
    assert len({x['topic'] for x in index['manifest']}) == len(index['manifest'])
    latest = json.loads((PACKS / 'LATEST.json').read_text('utf-8'))
    assert latest['generation'] == commit['generation']
    assert latest['digest'] == commit['digest']
    assert sha((PACKS / latest['path']).read_bytes()) == latest['sha256']
    with tarfile.open(PACKS / latest['path']) as archive:
        assert all('/tmp/' not in n and '/private/' not in n and '/artifacts/' not in n for n in archive.getnames())
        for item in index['manifest']:
            assert sha(archive.extractfile('.peicd100/codex/' + item['path']).read()) == item['sha256']
    return commit

def update(status, note):
    BASE.mkdir(parents=True, exist_ok=True)
    PACKS.mkdir(parents=True, exist_ok=True)
    lock = BASE / 'tmp/memory_txn.lock'
    lock.parent.mkdir(exist_ok=True)
    fd = os.open(lock, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
    os.write(fd, encoded({'owner_pid': os.getpid(), 'task_id': 'targz-progress-1-0-1'}))
    os.close(fd)
    try:
        previous = verify() if (BASE / 'MEMORY_COMMIT.json').exists() else {'generation': 0}
        generation = previous['generation'] + 1
        atomic(BASE / 'MEMORY_COMMIT.json', encoded({'memory_state': 'CONSOLIDATING', 'last_clean_generation': previous['generation']}))
        docs = {
            '00_START_HERE.md': '# TAR.GZ Desktop\nRead MEMORY_COMMIT.json and MEMORY_INDEX.yaml first.\nRequirements: USER_REQUIREMENTS.md. Plan and next action: ACTIVE_TASK.md.\nGeneration: %s\n' % generation,
            'PROJECT.md': '# Project\nWindows x64 tar.gz desktop utility, native Explorer extension and offline installer. Original script.txt is input evidence and must be preserved.\n',
            'USER_REQUIREMENTS.md': '# Canonical requirements / Effective Policy: desktop-v1\nTraditional Chinese (Taiwan). Menu title: 壓縮成 tar.gz. Black minimalist UI; private load 清松手寫體 9 文青. Use approved generated archive-box icon. Install, manual upgrade, uninstall. Modern Windows 11 menu. Multiple selected siblings become one archive. Display numeric percent on the progress bar using measured work; 100 only after verification and output publication. Release 1.0.1 using existing signing key. No original input deletion, no archive overwrite. No automatic Git publish. Installing OS registrations is a user-run final step; build/test under workspace only.\n',
            'ACTIVE_TASK.md': '# Task\ntask_id: targz-progress-1-0-1\ntask_title: TAR.GZ numeric progress\ngoal_revision: 1\ntask_status: %s\n\nAcceptance: measured numeric progress, monotonic 0..100, final 100 only after output publication, empty selections and cancellation covered, built and signed 1.0.1 upgrade using existing signer.\nHard constraints: USER_REQUIREMENTS.md (sole desktop-v1 Effective Policy).\n\nExecution plan: implement progress; test compression/progress/rendering; sign and verify package; clean handoff.\nEach done_when: artifact hash plus targeted test evidence.\n\nCurrent result / next_exact_action:\n%s\n\nResume rule: verify clean memory and workspace first; reuse hash-matching successful tests; never repeat running builds.\nNo agents or live operations. Input script.txt unchanged.\nsession_compaction_count: 0\ntask_compaction_total: 0\nrun_inventory: build/progress-tests.log, build/test-results.json, build/package-verification.json\nfiles_modified: src/Archive.cs, src/App.cs, src/Common.cs, tools/build.ps1, tests/, README.md\ncommands_run: tools/build.ps1; tests/smoke.ps1; tests/progress.ps1; tests/verify_package.py; tests/verify_signature.ps1\nknown_failures: sandbox package lifecycle verification denied; previous junction test SKIP.\nplan_changes: none\nagents_version: 1.12.0\n' % (status, note),
            'CURRENT_STATE.md': '# Verified state\n%s\nSee build/ for reproducible diagnostics and tests. See USER_REQUIREMENTS.md for execution policy.\n' % note,
            'GOTCHAS.md': '# Gotchas\nNo dotnet SDK available initially; .NET Framework 4.8 csc is available. Legacy C:/MinGW fails (DLL relocation); project-local LLVM-MinGW works. Do not modify Conda environments. Use full interpreter path. Windows tar.exe failed UTF-8 list and crashed on emoji argv; managed PAX TAR/GZip is the implementation. Windows 11 modern menu requires native IExplorerCommand and package identity. Package certificate trust requires explicit installer action. Sandbox Get-AppxPackage is denied; real installation/lifecycle/Explorer display remains unverified. Preserve ignored build/signing/local-signing.pfx to keep upgrade identity.\n',
            'VERIFY.md': '# Validation\nRun tools/build.ps1; tests/smoke.ps1; tests/verify_package.py; tests/verify_signature.ps1. Inspect metadata, native exports, signed MSIX and payload SHA-256. Managed PAX TAR/GZip validation plus independent Python extraction content comparison. Windows Explorer integration and lifecycle require user installation; sandbox Get-AppxPackage is denied.\n'
        }
        for name, value in docs.items():
            atomic(BASE / name, value.encode('utf-8'))
        manifest = [{'topic': n, 'path': n, 'lifecycle': 'active', 'sha256': sha((BASE / n).read_bytes())} for n in sorted(docs, key=lambda n: n.encode('utf-8'))]
        assert sum((BASE / n).stat().st_size for n in docs) < 65536
        index = encoded({'generation': generation, 'execution_domains': {'desktop-v1': 'USER_REQUIREMENTS.md'}, 'manifest': manifest})
        atomic(BASE / 'MEMORY_INDEX.yaml', index)
        commit = {'memory_state': 'CLEAN', 'generation': generation, 'last_clean_generation': generation, 'digest': sha(index), 'committed_at': datetime.now(timezone.utc).isoformat()}
        atomic(BASE / 'MEMORY_COMMIT.json', encoded(commit))
        package = PACKS / (datetime.now().strftime('%Y-%m-%d_%H%M%S') + '.tar.gz')
        temp = package.with_suffix('.new')
        with tarfile.open(temp, 'w:gz') as tar:
            for name in list(docs) + ['MEMORY_INDEX.yaml', 'MEMORY_COMMIT.json']:
                tar.add(BASE / name, arcname='.peicd100/codex/' + name)
        os.replace(temp, package)
        atomic(PACKS / 'LATEST.json', encoded({'generation': generation, 'digest': commit['digest'], 'path': package.name, 'sha256': sha(package.read_bytes())}))
        verify()
        lock.unlink()
        print('CLEAN generation', generation)
    except Exception:
        atomic(BASE / 'MEMORY_COMMIT.json', encoded({'memory_state': 'NEEDS_REPAIR'}))
        raise

if __name__ == '__main__':
    if sys.argv[1] == 'verify':
        print(verify())
    else:
        update(sys.argv[1], sys.argv[2])
