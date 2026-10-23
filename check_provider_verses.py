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

CASES = [('Genesis','gen',1,10,[26]), ('Genesis','gen',1,46,[21]),
         ('IISamuel','2sa',10,23,[31]), ('IChronicles','1ch',13,1,[20]),
         ('IChronicles','1ch',13,7,[12,15]), ('IChronicles','1ch',13,8,[36]),
         ('IChronicles','1ch',13,9,[42]), ('IChronicles','1ch',13,11,[33]),
         ('IChronicles','1ch',13,12,[3]), ('IChronicles','1ch',13,24,[13]),
         ('IChronicles','1ch',13,27,[25]), ('Ezra','ezr',15,2,[24]),
         ('Nehemiah','neh',16,7,[28]), ('Nehemiah','neh',16,12,[29]),
         ('Isaiah','isa',23,62,[4])]
TASKS = []
for key,online,number,chapter,verses in CASES:
    for ver in ['gae']:
        TASKS.append({'provider':'Godpia','translation':ver,'book':key,'chapter':chapter,'targets':verses,
                      'url':f'https://www.godpia.com/read/reading.asp?ver={ver}&vol={online}&chap={chapter}'})
        TASKS.append({'provider':'Godpia','translation':ver,'book':key,'chapter':chapter,'targets':verses,
                      'url':'https://www.godpia.com/read/reading_body.asp',
                      'post':{'readFlag':'','calDate':'','seq':'','ver':ver,'ver2':'','vol':online,
                              'chap':str(chapter),'vol2':'','chap2':'','handwriting':'','curUrl':'/read/reading.asp'}})
    TASKS.append({'provider':'GOODTV','translation':'2','book':key,'chapter':chapter,'targets':verses,
                  'url':'https://goodtvbible.goodtv.co.kr/bible.asp',
                  'post':{'bible_idx':str(number),'jang_idx':str(chapter),'bible_version_1':'2',
                          'bible_version_2':'0','bible_version_3':'0','count':'1'}})
    TASKS.append({'provider':'Korean Bible Society','translation':'GAE','book':key,'chapter':chapter,'targets':verses,
                  'url':f'https://www.bskorea.or.kr/bible/korbibReadpage.php?version=GAE&book={online}&chap={chapter}'})
TASKS.extend([
    {'provider':'Godpia','url':'https://www.godpia.com/read/reading.asp'},
    {'provider':'GOODTV','url':'http://goodtvbible.goodtv.co.kr/bible.asp'},
    {'provider':'GOODTV','url':'https://goodtvbible.goodtv.co.kr/bible.asp'},
    {'provider':'Korean Bible Society','url':'https://www.bskorea.or.kr/bible/korbibReadpage.php'},
])
TASKS=[t for t in TASKS if t['provider'] in ['Godpia','Korean Bible Society'] and t.get('book')]


def text_only(value):
    return BeautifulSoup(value,'html.parser').get_text(' ',strip=True)


SUPPLIED=json.loads(Path('supplied-data/Bibles/krv.json').read_text()) if Path('supplied-data/Bibles/krv.json').exists() else None


def normalized(value):
    return re.sub(r'\s+', '', re.sub(r'<[^>]*>', '', value))


def character_repairs(task, rows):
    if SUPPLIED is None:return []
    book=next(b for b in SUPPLIED['books'] if b['key']==task['book'])
    chapter=next(c for c in book['chapters'] if c['number']==task['chapter'])
    result=[]
    for number in task['targets']:
        verse=next(v for v in chapter['verses'] if v['number']==number)
        original=verse['text']; local=normalized(original); remote=normalized(rows.get(number,''))
        for offset,old in enumerate(local):
            if old!='?':continue
            replacement=None
            for width in [16,12,8]:
                before=local[max(0,offset-width):offset]; after=local[offset+1:offset+width+1]
                pattern=re.escape(before)+r'(?P<restored>[가-힣])'+re.escape(after)
                matches=list(re.finditer(pattern,remote))
                if len(matches)==1:
                    replacement=matches[0]['restored'];break
            if replacement is None:continue
            original_index=original.index('?')
            token=next(m.group() for m in re.finditer(r'[가-힣?]+',original) if m.start()<=original_index<m.end())
            result.append({'book':task['book'],'chapter':task['chapter'],'verse':number,
                           'reference':f'{book["abbreviation"]}{task["chapter"]}:{number}',
                           'originalCharacter':'?','restoredCharacter':replacement,
                           'originalToken':token,'restoredToken':token.replace('?',replacement),
                           'localTextSHA256':hashlib.sha256(original.encode()).hexdigest(),
                           'sourceTextSHA256':hashlib.sha256(rows[number].encode()).hexdigest(),
                           'wholeNormalizedPassageMatches':local.replace('?',replacement)==remote})
    return result


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
        result['readerSelection'] = dict(re.findall(r'let\s+(jsCurVer|jsCurVol|jsCurChap)\s*=\s*[\"\']([^\"\']*)',page))
        if task['provider']=='Godpia' and 'book' not in task:
            # Public application code identifies the actual reading endpoint.
            pieces=[]
            for script in soup.find_all('script'):
                if script.get('src'):
                    address=urllib.parse.urljoin(result['finalURL'],script['src'])
                    parsed=urllib.parse.urlsplit(address)
                    if parsed.hostname!='www.godpia.com':continue
                    if not parsed.path.endswith('/read.js'): continue
                    address=urllib.parse.quote(address,safe=':/?=&%+!')
                    try:
                        with urllib.request.urlopen(address,timeout=15) as js:
                            code=js.read(1_500_000).decode('utf-8',errors='replace')
                    except Exception as error:
                        pieces.append({'url':address,'error':str(error)});continue
                else:
                    address=result['finalURL'];code=script.get_text()
                matches=[code[max(0,m.start()-150):m.end()+230] for m in re.finditer(r'ajax|\.asp|getBible|BibleVer|fnBible',code,re.I)]
                if matches:pieces.append({'url':address,'codeExcerpts':matches[:35]})
            result['applicationEndpoints']=pieces
        rows = {}
        ranges = {}
        if task['provider']=='Godpia':
            for label,body in re.findall(r'class=[\"\']num[\"\']>(.*?)</span>(.*?)</p>',page,re.S):
                numbers = re.findall(r'\d+',text_only(label))
                if numbers:
                    start=int(numbers[0]); end=int(numbers[-1])
                    if end<start or end-start>10: continue
                    for number in range(start,end+1):
                        rows[number] = text_only(body)
                        ranges[number] = (start,end)
            for node in soup.select('[datasec], [data-sec]'):
                body=node.select_one('.bible-read-cont')
                if body is None:continue
                label=node.get('datasec') or node.get('data-sec')
                numbers=re.findall(r'\d+',label)
                if not numbers:continue
                start=int(numbers[0]);end=int(numbers[-1])
                if end<start or end-start>10:continue
                for number in range(start,end+1):
                    rows[number]=body.get_text('',strip=False).strip()
                    ranges[number]=(start,end)
            result['dataVerseSamples'] = [{'tag':n.name,'attributes':n.attrs,'bodyClasses':[x.get('class') for x in n.find_all(class_=True)][:5]}
                                          for n in soup.select('[datasec], [data-sec]')[:2]]
            result['versionNameFields'] = [{'name':n.get('id') or n.get('name'),'value':n.get('value')}
                                          for n in soup.select('input') if 'vername' in str(n.attrs).lower()]
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
        result['unicodeRepairs']=character_repairs(task,rows)
        result['versePresence'] = [{'number':n,'found':n in rows,'characters':len(rows.get(n,'')),
                                    'sourceRange':ranges.get(n,[n,n]),
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
        # Preserve only the requested Easy Bible repair passages and supplied
        # neighboring text for local comparison; never include full chapters.
        if task['provider']=='Godpia' and task.get('translation')=='easy' and rows:
            repair = {'book':task['book'],'chapter':task['chapter'],'url':result['finalURL'],
                      'translationOptions':result['translationOptions'],
                      'targets':[{'number':n,'text':rows.get(n,''),'range':ranges.get(n,[n,n])} for n in task['targets']],
                      'neighbors':[{'number':n,'text':rows.get(n,''),'range':ranges.get(n,[n,n])}
                                   for n in [min(task['targets'])-1,max(task['targets'])+1]]}
            output=Path(sys.argv[1]);output.mkdir(parents=True,exist_ok=True)
            (output/(task['book']+'-easy-repair.json')).write_text(json.dumps(repair,ensure_ascii=False,indent=2),encoding='utf-8')
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
