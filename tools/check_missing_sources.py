"""Check public provider pages for the seven empty slots in supplied text.

No credentials or access-control bypass. Only publish the requested missing
slots; adjacent supplied verses are used for comparison, never republished.
"""
from concurrent.futures import ThreadPoolExecutor
import html
import json
from pathlib import Path
import re
import sys
import urllib.parse
import urllib.request

CASES = [('Judges', 'jdg', '삿', 20, [22, 23]),
         ('IISamuel', '2sa', '삼하', 4, [6, 7]),
         ('IKings', '1ki', '왕상', 8, [41, 42]),
         ('Jeremiah', 'jer', '렘', 16, [2])]


def plain(value):
    return html.unescape(re.sub(r'<[^>]+>', '', value)).strip()


def normalized(value):
    return re.sub(r'\s+', '', plain(value)).translate(str.maketrans('“”‘’', '\"\"\'\''))


def probe(case):
    key, online, abbr, chapter, missing = case
    document = json.loads(Path('Bibles/easy.json').read_text(encoding='utf-8'))
    book = next(b for b in document['books'] if b['key'] == key)
    verses = next(c for c in book['chapters'] if c['number'] == chapter)['verses']
    local = {v['number']: v['text'] for v in verses}
    encoded = urllib.parse.quote((abbr + str(chapter)).encode('cp949'))
    book_number = {'Judges':7,'IISamuel':10,'IKings':11,'Jeremiah':24}[key]
    urls = [f'http://www.holybible.or.kr/B_AGAPE/cgi/bibleftxt.php?VR=AGAPE&VL={book_number}&CN={chapter}&CV=99',
            f'https://bible.godpia.com/read/reading.asp?ver=easy&vol={online}&chap={chapter}',
            f'https://find.godpeople.com/?page=bidx&kwrd={encoded}&vers=ezsn',
            f'http://find.godpeople.com/?page=bidx&kwrd={encoded}&vers=ezsn']
    attempts = []
    for url in urls:
        attempt = {'url': url}
        try:
            request = urllib.request.Request(url, headers={'User-Agent': 'Mozilla/5.0'})
            with urllib.request.urlopen(request, timeout=25) as response:
                raw = response.read(2_000_000)
                encoding = response.headers.get_content_charset() or ('cp949' if 'godpeople' in url or 'holybible' in url else 'utf-8')
                page = raw.decode(encoding, errors='replace')
                attempt.update(status=response.status, finalURL=response.geturl(), bytes=len(raw))
            if re.search(r'Just a moment|captcha|verify you are human', page, re.I):
                attempt['error'] = 'Access gate; stopped.'
                attempts.append(attempt)
                continue
            if 'holybible' in url:
                matches = []
                for fragment in re.findall(r'<li\b[^>]*>(.*?)(?:</li>|(?=<li\b)|</ol>)', page, re.I|re.S):
                    row = plain(fragment)
                    m = re.match(r'^\s*(\d+)\s*[.\s]?\s*(.+)', row, re.S)
                    if m: matches.append((m.group(1),m.group(2)))
                attempt['numberTags'] = re.findall(r'<[^>]+>\s*\d{1,3}\s*</[^>]+>',page)[:10]
                attempt['tagStructure'] = re.sub(r'>[^<]*<','><',page)[7000:14500]
                title = re.search(r'<title[^>]*>(.*?)</title>',page,re.I|re.S)
                attempt['title'] = plain(title.group(1)) if title else ''
                if not matches:
                    for n,body in re.findall(r'<li\b[^>]*>\s*(?:<[^>]+>\s*)*(\d+)\s*(.*?)<br\s*/?>',page,re.I|re.S):
                        matches.append((n,body))
            elif 'godpeople' in url:
                matches = re.findall(r'bidx_listTd_yak.+?>(\d+)[\s\S]+?bidx_listTd_phrase.+?>(.+?)</td', page)
            else:
                matches = re.findall(r'class=[\"\']num[\"\']>(\d+).*?</span>(.*?)</p>', page, re.S)
            remote = {int(n): plain(body) for n, body in matches}
            neighbors = [min(missing)-1, max(missing)+1]
            agreement = all(normalized(remote.get(n, '')) == normalized(local[n]) for n in neighbors)
            attempt.update(parsedVerseCount=len(remote), adjacentSourceMatches=agreement,
                           availableMissing=[n for n in missing if remote.get(n)])
            attempts.append(attempt)
            if agreement and all(remote.get(n) for n in missing):
                return {'book': key, 'chapter': chapter, 'attempts': attempts,
                        'repairs': [{'number': n, 'text': remote[n], 'sourceURL': url} for n in missing]}
        except Exception as error:
            attempt['error'] = str(error)
            attempts.append(attempt)
    return {'book': key, 'chapter': chapter, 'attempts': attempts, 'repairs': []}


if __name__ == '__main__':
    output = Path(sys.argv[1])
    output.mkdir(parents=True, exist_ok=True)
    with ThreadPoolExecutor(max_workers=4) as pool:
        results = list(pool.map(probe, CASES))
    (output / 'source-check.json').write_text(json.dumps(results, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Confirmed missing slots:', sum(len(r['repairs']) for r in results))
