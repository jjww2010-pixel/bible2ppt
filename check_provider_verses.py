"""Inspect public reading pages without login or access-control bypass.

Publish verse presence, source selection and hashes. No full Bible chapters
are included in the report. A short quote budget is shared per provider.
"""
from concurrent.futures import ThreadPoolExecutor
import hashlib
import html
import json
from pathlib import Path
import re
import sys
import urllib.parse
import urllib.request
from bs4 import BeautifulSoup

CASES = [('Judges','jdg',7,20,[22,23]), ('IISamuel','2sa',10,4,[6,7]),
         ('IKings','1ki',11,8,[41,42]), ('Jeremiah','jer',24,16,[2])]
TASKS = []
for key,online,number,chapter,verses in CASES:
    for ver in ['easy','gae']:
        TASKS.append({'provider':'Godpia','translation':ver,'book':key,'chapter':chapter,'targets':verses,
                      'url':f'http://bible.godpia.com/read/reading.asp?ver={ver}&vol={online}&chap={chapter}'})
    TASKS.append({'provider':'GOODTV','translation':'2','book':key,'chapter':chapter,'targets':verses,
                  'url':'http://goodtvbible.goodtv.co.kr/bible.asp',
                  'post':{'bible_idx':str(number),'jang_idx':str(chapter),'bible_version_1':'2',
                          'bible_version_2':'0','bible_version_3':'0','count':'1'}})
    TASKS.append({'provider':'Korean Bible Society','translation':'GAE','book':key,'chapter':chapter,'targets':verses,
                  'url':f'https://www.bskorea.or.kr/bible/korbibReadpage.php?version=GAE&book={online}&chap={chapter}'})
TASKS.extend([
    {'provider':'Godpia','url':'http://bible.godpia.com/index.asp'},
    {'provider':'Godpia','url':'https://bible.godpia.com/index.asp'},
    {'provider':'GOODTV','url':'http://goodtvbible.goodtv.co.kr/bible.asp'},
    {'provider':'GOODTV','url':'https://goodtvbible.goodtv.co.kr/bible.asp'},
    {'provider':'Korean Bible Society','url':'https://www.bskorea.or.kr/bible/korbibReadpage.php'},
])


def text_only(value):
    return BeautifulSoup(value,'html.parser').get_text(' ',strip=True)


def fetch(task):
    result = dict(task)
    try:
        data = urllib.parse.urlencode(task['post']).encode() if 'post' in task else None
        request = urllib.request.Request(task['url'],data=data,headers={'User-Agent':'Mozilla/5.0'})
        with urllib.request.urlopen(request,timeout=25) as response:
            raw = response.read(3_000_000)
            charset = response.headers.get_content_charset()
            if not charset:
                detected = re.search(br'charset\s*=\s*[\"\']?([\w-]+)',raw[:8000],re.I)
                charset = detected.group(1).decode() if detected else 'utf-8'
            page = raw.decode(charset,errors='replace')
            result.update(status=response.status,finalURL=response.geturl(),bytes=len(raw),charset=charset)
        if re.search(r'Just a moment|cf-chl-|captcha|verify you are human',page,re.I):
            result['accessGate'] = True
            return result
        soup = BeautifulSoup(page,'html.parser')
        result['title'] = soup.title.get_text(' ',strip=True) if soup.title else ''
        result['translationOptions'] = [{'value':o.get('value'), 'label':o.get_text(' ',strip=True),
                                         'selected':o.has_attr('selected')}
                                        for o in soup.select('select option')
                                        if re.search(r'개역|쉬운|현대|공동|새번역|성경|NIV|KJV',o.get_text())]
        result['translationLabels'] = [s.get_text(' ',strip=True) for s in soup.select('[id^="span_"]')]
        result['readingLinks'] = [{'url':urllib.parse.urljoin(result['finalURL'],a.get('href','')),
                                  'label':a.get_text(' ',strip=True)}
                                 for a in soup.select('a[href]')
                                 if re.search(r'읽기|쉬운|개역|현대|성경|reading|bible',a.get_text()+' '+a.get('href',''),re.I)][:60]
        result['scripts'] = [s.get('src') for s in soup.select('script[src]')]
        rows = {}
        if task['provider']=='Godpia':
            for number,body in re.findall(r'class=[\"\']num[\"\']>(\d+).*?</span>(.*?)</p>',page,re.S):
                rows[int(number)] = text_only(body)
        elif task['provider']=='GOODTV':
            area = soup.find(id='one_jang')
            if area:
                for number,body in re.findall(r'<b[^>]*>\s*(\d+).*?</b>(.*?)(?:<br\s*/?>|$)',str(area),re.S|re.I):
                    rows[int(number)] = text_only(body)
        else:
            for node in soup.select('.verse, .verseText, .verse-text, .content, .txt'):
                number = node.get('data-verse') or node.get('data-number')
                if number and str(number).isdigit(): rows[int(number)] = node.get_text(' ',strip=True)
            for node in soup.select('.number, .verseNum, .verse-num'):
                number = node.get_text(' ',strip=True).strip('. ')
                if number.isdigit():
                    following = []
                    for sibling in node.next_siblings:
                        following.append(str(sibling))
                    body = text_only(''.join(following))
                    if body: rows[int(number)] = body
        result['parsedVerseCount'] = len(rows)
        result['versePresence'] = [{'number':n,'found':n in rows,'characters':len(rows.get(n,'')),
                                    'isExplicitNone':rows.get(n,'').strip() in ['(없음)','없음'],
                                    'textSHA256':hashlib.sha256(rows[n].encode()).hexdigest() if n in rows else None,
                                    'shortExcerpt': ' '.join(rows.get(n,'').split()[:4])}
                                   for n in task.get('targets',[])]
        if not rows and task.get('targets'):
            # Keep structure and numeric labels for diagnosing changed markup.
            result['numericElements'] = [str(n) for n in soup.find_all(['span','b','sup','a'])
                                         if n.get_text(strip=True) in [str(v) for v in task['targets']]][:12]
            result['relevantClasses'] = list(dict.fromkeys(' '.join(n.get('class',[])) for n in soup.find_all(class_=True)
                                                          if re.search(r'verse|bible|content|number|num|read', ' '.join(n.get('class',[])),re.I)))[:60]
    except Exception as error:
        result['error'] = str(error)
    return result


if __name__=='__main__':
    output=Path(sys.argv[1]);output.mkdir(parents=True,exist_ok=True)
    with ThreadPoolExecutor(max_workers=12) as pool:
        results=list(pool.map(fetch,TASKS))
    # At most 90 quoted words per provider across all translated passages.
    budgets={}
    for result in results:
        provider=result['provider']
        for verse in result.get('versePresence',[]):
            words=verse['shortExcerpt'].split();take=min(len(words),90-budgets.get(provider,0))
            verse['shortExcerpt']=' '.join(words[:take]);budgets[provider]=budgets.get(provider,0)+take
    (output/'provider-check.json').write_text(json.dumps(results,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Checked public requests:',len(results))
