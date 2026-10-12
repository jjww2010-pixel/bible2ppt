from concurrent.futures import ThreadPoolExecutor
import html
import json
from pathlib import Path
import re
import sys
import urllib.request

URLS = ['https://www.kaggle.com/api/v1/datasets/download/rycont/korean-bible-classic-modern-pair',
        'https://nocr.net/korea_easy',
        'https://bible.godpeople.com/index.html',
        'https://www.godpeople.com/?GO=bible']


def probe(url):
    result = {'url': url}
    try:
        with urllib.request.urlopen(urllib.request.Request(url, headers={'User-Agent': 'Mozilla/5.0'}), timeout=25) as r:
            raw = r.read(32_000_000)
            if raw.startswith(b'PK'):
                import csv, io, zipfile
                with zipfile.ZipFile(io.BytesIO(raw)) as archive:
                    result['archiveFiles'] = archive.namelist()
                    for name in archive.namelist():
                        if name.endswith('.csv'):
                            content = archive.read(name).decode('utf-8-sig')
                            rows = list(csv.reader(io.StringIO(content)))
                            result.setdefault('tables', []).append({'name': name, 'header': rows[0], 'rows': len(rows)-1})
                            output = Path(sys.argv[1]); output.mkdir(parents=True, exist_ok=True)
                            (output / (Path(name).name + '.json')).write_text(json.dumps(rows,ensure_ascii=False), encoding='utf-8')
                result.update(status=r.status, finalURL=r.geturl(), bytes=len(raw))
                return result
            charset = r.headers.get_content_charset() or ('cp949' if 'holybible' in url else 'utf-8')
            page = raw.decode(charset, errors='replace')
            result.update(status=r.status, finalURL=r.geturl(), bytes=len(raw))
        title = re.search(r'<title[^>]*>(.*?)</title>', page, re.S)
        result['title'] = html.unescape(title.group(1)) if title else ''
        result['links'] = [(html.unescape(a), re.sub('<[^>]*>', '', html.unescape(b)))
                           for a,b in re.findall(r'<a[^>]+href=[\"\']([^\"\']+)[\"\'][^>]*>(.*?)</a>', page, re.S)
                           if re.search(r'version|bible|쉬운|easy', a+' '+b, re.I)][:100]
        data = re.search(r'<script[^>]+id=[\"\']__NEXT_DATA__[\"\'][^>]*>(.*?)</script>', page, re.S)
        if data:
            result['nextData'] = json.loads(data.group(1))
        if page.lstrip().startswith(('{', '[')):
            result['data'] = json.loads(page)
    except Exception as error:
        result['error'] = str(error)
    return result


out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
with ThreadPoolExecutor(max_workers=5) as pool:
    result = list(pool.map(probe, URLS))
(out / 'provider-discovery.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
