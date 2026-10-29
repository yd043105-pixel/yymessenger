"""Validate the curated public kit without reading any operational school data."""
import argparse
import hashlib
import json
import re
import zipfile
from pathlib import Path
from pypdf import PdfReader

parser = argparse.ArgumentParser()
parser.add_argument('kit', type=Path)
args = parser.parse_args()
kit = args.kit.resolve()
manifest = json.loads((kit/'99_검증결과/파일목록.json').read_text(encoding='utf-8-sig'))
assert manifest['version'] == '1.0.0' and not manifest['releasePublished'] and not manifest['iosInstallable']
private_tokens = ['김동욱', 'yd043105', 'rlaehddnr1355', 'poo1355', '여양', '061-689-5870', '10.85.88.101', 'C:\\Users\\user', 'C:/Users/user']
forbidden = re.compile(r'(?i)(\.lnk$|\.db($|-)|\.sqlite3?($|-)|\.dpapi$|\.pfx$|\.p12$|\.pem$|\.key$|\.mobileprovision$|\.secrets\.xml$|\.local\.json$|^\.env|^school-request-|^school-registration-decision-)')
def public_text(text, filename):
    assert not any(x.casefold() in text.casefold() for x in private_tokens), f'Private text in {filename}'

files = [f for f in kit.rglob('*') if f.is_file()]
expected = {x['file'] for x in manifest['files']} | {'99_검증결과/파일목록.json', '99_검증결과/SHA256SUMS.txt'}
assert {f.relative_to(kit).as_posix() for f in files} == expected, 'Unlisted or missing delivered files'
for item in manifest['files']:
    f = (kit/item['file']).resolve()
    assert f.is_relative_to(kit), 'Manifest traversal'
    assert f.stat().st_size == item['bytes']
    assert hashlib.sha256(f.read_bytes()).hexdigest() == item['sha256'], f'Hash mismatch: {f.name}'
for f in files:
    assert not forbidden.search(f.name), f'Operational/private file: {f.name}'
    if f.suffix.lower() in {'.md', '.txt', '.json', '.yml'}:
        public_text(f.read_text(encoding='utf-8-sig'), f.name)
    if f.suffix.lower() == '.zip':
        with zipfile.ZipFile(f) as archive:
            assert archive.testzip() is None, 'Corrupt Mac archive'
            names = archive.namelist()
            assert any('.app/' in name for name in names), 'Mac app missing'
            for name in names:
                assert not name.startswith('/') and '..' not in Path(name).parts
                assert not forbidden.search(Path(name).name), 'Operational file in Mac archive'

pdfs = [f for f in files if f.suffix == '.pdf']
assert len(pdfs) == 4
pages = 0
for f in pdfs:
    reader = PdfReader(f)
    public_text(str(reader.metadata), f.name)
    for page in reader.pages:
        text = page.extract_text()
        assert len(text) > 500, f'Empty or overflow page in {f.name}'
        public_text(text, f.name)
    pages += len(reader.pages)
assert pages == 16
config = kit/'03_관리자_준비양식'
policy = json.loads((config/'registration-policy.draft.json').read_text(encoding='utf-8-sig'))
assert policy['approvedForProduction'] is False
for document in policy['documents']:
    source = next(config.glob('서비스*' if document['id'] == 'terms' else '개인정보*'))
    assert source.read_text(encoding='utf-8-sig').strip() == document['body'].strip()
review = json.loads((config/'deployment-review.draft.json').read_text(encoding='utf-8-sig'))
assert review['status'] == 'draft' and not review['school'] and not review['reviewAuthority']
operations = json.loads((config/'server-operations.example.json').read_text(encoding='utf-8-sig'))
assert not operations['alerts']['enabled'] and not operations['alerts']['recipient']
assert len([f for f in files if f.suffix == '.exe']) == 2
assert len([f for f in files if f.suffix == '.apk']) == 2
assert len([f for f in files if f.suffix == '.zip']) == 2
print(f'PASS {len(files)} delivered files; 4 guides / {pages} pages; hashes, private-text exclusions, draft templates, platform labels and Mac archive integrity')
