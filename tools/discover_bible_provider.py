from concurrent.futures import ThreadPoolExecutor
import html
import json
from pathlib import Path
import re
import sys
import urllib.request

URLS = ['https://www.bible.com/ko/languages/kor',
        'https://www.bible.com/ko/versions',
        'https://www.bible.com/json/bible/versions/kor',
        'https://bible.godpeople.com/',
        'https://www.holybible.or.kr/B_EASY/cgi/bibleftxt.php?VR=0&VL=7&CN=20&CV=99']


def probe(url):
    result = {'url': url}
    try:
        with urllib.request.urlopen(urllib.request.Request(url, headers={'User-Agent': 'Mozilla/5.0'}), timeout=25) as r:
            raw = r.read(2_000_000)
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
