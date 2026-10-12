from concurrent.futures import ThreadPoolExecutor
import html
import json
from pathlib import Path
import re
import sys
import urllib.request
import urllib.parse

URLS = ['https://www.bing.com/search?' + urllib.parse.urlencode({'q': q}) for q in [
    '"쉬운성경" "사사기" "20" "22"',
    '"쉬운성경" "사무엘하" "4" "6"',
    '"쉬운성경" "열왕기상" "8" "41"',
    '"쉬운성경" "예레미야" "16" "2"']]
URLS += ['http://www.holybible.or.kr/', 'https://bible.goodtv.co.kr/bible.asp']


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
                            local = json.loads(Path('Bibles/easy.json').read_text(encoding='utf-8'))
                            def norm(text):
                                text = re.sub(r'<[^>]+>', '', text)
                                return ''.join(c for c in text if c.isalnum())
                            cases = [('Judges',20,[22,23]),('IISamuel',4,[6,7]),('IKings',8,[41,42]),('Jeremiah',16,[2])]
                            for key,chapter,missing in cases:
                                book = next(b for b in local['books'] if b['key']==key)
                                vs = next(c for c in book['chapters'] if c['number']==chapter)['verses']
                                bynum = {v['number']:v['text'] for v in vs}
                                first,last = min(missing)-1,max(missing)+1
                                for col in range(len(rows[0])):
                                    starts = [i for i,row in enumerate(rows[1:],1) if len(row)>col and norm(row[col])==norm(bynum[first])]
                                    ends = [i for i,row in enumerate(rows[1:],1) if len(row)>col and norm(row[col])==norm(bynum[last])]
                                    candidate = {'book':key,'chapter':chapter,'column':rows[0][col], 'starts':starts,'ends':ends}
                                    if len(starts)==len(ends)==1 and ends[0]-starts[0]==len(missing)+1:
                                        candidate['repairs']=[{'number':n,'text':rows[starts[0]+i+1][col]} for i,n in enumerate(missing)]
                                    result.setdefault('candidates',[]).append(candidate)
                result.update(status=r.status, finalURL=r.geturl().split('?')[0], bytes=len(raw))
                return result
            charset = r.headers.get_content_charset() or ('cp949' if 'holybible' in url else 'utf-8')
            page = raw.decode(charset, errors='replace')
            result.update(status=r.status, finalURL=r.geturl().split('?')[0], bytes=len(raw))
        title = re.search(r'<title[^>]*>(.*?)</title>', page, re.S)
        result['title'] = html.unescape(title.group(1)) if title else ''
        result['links'] = [(html.unescape(a), re.sub('<[^>]*>', '', html.unescape(b)))
                           for a,b in re.findall(r'<a[^>]+href=[\"\']([^\"\']+)[\"\'][^>]*>(.*?)</a>', page, re.S)
                           if re.search(r'version|bible|쉬운|easy|성경', a+' '+b, re.I)][:100]
        if 'bing.com' in url:
            result['searchResults'] = [re.sub(r'\s+', ' ', re.sub(r'<[^>]+>', ' ', html.unescape(part)))[:1400]
                                       for part in re.findall(r'<li[^>]+class=[\"\']b_algo[\"\'][^>]*>(.*?)</li>', page, re.S)][:6]
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
