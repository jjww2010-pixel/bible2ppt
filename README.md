# 성경2PPT

> 성경 구절을 PPT로 만들어주는 프로그램

<p align="center"><img src="https://user-images.githubusercontent.com/4927894/59970937-1377a600-95ad-11e9-93b4-66eed61dd932.png" alt="성경2PPT 스크린샷"></p>
<p align="center">⬇️</p>
<p align="center"><img src="https://user-images.githubusercontent.com/4927894/97580541-caecba00-1a36-11eb-9135-979d7e68dc16.png" alt="성경2PPT로 만든 PPT 스크린샷"></p>


## 사용 성경 목록

이 버전은 **로컬 JSON 파일**에서 성경 본문을 읽습니다. 웹사이트 인증이나 페이지 구조에 의존하지 않습니다.

| 소스 | 언어 | 성경 |
| --- | --- | --- |
| 로컬 성경 (JSON) | 한국어 | 사용자가 준비한 개역개정 본문 |

실행 파일 옆에 `Bibles/krv.json`을 준비하세요. 전체 본문은 저장소에 포함되어 있지 않습니다.
파일 형식과 설치 방법은 [로컬 성경 사용 안내](docs/local-bible.md)를 참고하세요.
TXT나 구절별 JSON은 `tools/import_bible.py`로 변환할 수 있습니다. [자료 조사 결과](docs/bible-data-sources.md)도 함께 확인하세요.

## 성경 구절 입력 방법

![성경2PPT 성경 구절 입력 칸 강조 스크린샷](https://user-images.githubusercontent.com/4927894/36576619-1bbd85aa-1895-11e8-9d3c-7b4a58cf807f.png)

**성경 구절 입력 칸**에 **성경 구절**을 아래 형식으로 입력하면 PPT를 만들 수 있습니다.
여러 **성경 구절**을 PPT로 만드려면 띄어쓰기(<kbd>Space</kbd>)로 구분해서 입력하세요.

### 형식

```
책_이름_약자[시작_장[:시작_절][-[끝_장[:끝_절]|끝_절]]]
```

### 예시

| 성경 구절 | 설명 |
| --- | --- |
| `창` | 창세기 전체 |
| `창1` | 창세기 1장 전체 |
| `롬1-3` | 로마서 1장 1절 - 3장 전체 |
| `레1-3:9` | 레위기 1장 1절 - 3장 9절 |
| `전1:3` | 전도서 1장 3절 |
| `스1:3-9` | 에스라 1장 3절 - 1장 9절 |
| `사1:3-3:9` | 이사야 1장 3절 - 3장 9절 |


## 템플릿

![성경2PPT 템플릿 스크린샷](https://user-images.githubusercontent.com/4927894/36580193-9972bece-18aa-11e8-93f2-035283e1a387.png)

**성경2PPT**는 PPT를 만들 때 **템플릿**을 사용합니다.
**템플릿**을 꾸미고 좋아하는 스타일로 PPT를 만드세요!

### 기능

* **치환자**: **템플릿**에 텍스트로 **치환자**를 사용하면
    반복되는 내용을 자동으로 입력할 수 있습니다.

    | 치환자 | 내용 | *접미사* 지원 |
    | --- | --- | :---: |
    | `[TITLE]` | 책 이름 | 예 |
    | `[STITLE]` | 책 이름 약자 | 예 |
    | `[CHAP]` | 장 번호 | 예 |
    | `[PARA]` | 절 번호 | 아니요 |
    | `[BODY]` | 내용 | 아니요 |
    | `[BODY1]` ~ `[BODY9]` | 내용(다중 성경 지원) | 아니요 |

    | 실험용 치환자 | 내용 | *접미사* 지원 |
    | --- | --- | :---: |
    | `[CPAS]` | 시작 절 번호 | 아니요 |
    | `[CPAE]` | 끝 절 번호 | 아니요 |
* **접미사**: **치환자** 뒤에 오는 텍스트를 `:`로 구분하여 입력할 수 있습니다.
    **치환자**를 표시하지 않으면 **접미사**도 표시하지 않습니다.

    | 예시 | 내용 | 책 이름 생략 시 | 장 번호 생략 시 |
    | --- | --- | --- | --- |
    | `[TITLE: ][CHAP::[PARA]]` |  `창세기 1:1` |  `1:1` |  `창세기` |


## 기타 기능

* **로컬 본문**: `Bibles/krv.json`에서 본문을 읽어 인터넷 연결 없이 PPT를 만듭니다. 파일을 교체한 뒤에는 프로그램을 다시 시작하세요.
* **장별로 PPT 나누기**: `책 이름/장 번호.pptx`의 구조로 장별로 PPT를 만들어 저장합니다.


## 설치 방법

로컬 본문 변경판은 [64비트 Windows ZIP](https://github.com/jjww2010-pixel/bible2ppt/raw/refs/heads/windows-download/Bible2PPT-win-x64.zip) 또는 [32비트 Windows ZIP](https://github.com/jjww2010-pixel/bible2ppt/raw/refs/heads/windows-download/Bible2PPT-win-x86.zip)을 내려받아 압축을 모두 풀고 `Bible2PPT.exe`를 실행합니다.
비공개 저장소에 접근할 수 있는 GitHub 계정으로 로그인해야 합니다. 직접 다운로드되지 않으면 [배포 파일 목록](https://github.com/jjww2010-pixel/bible2ppt/tree/windows-download)에서 ZIP을 열고 **Download raw file**을 누르세요.
압축 해제·본문 가져오기·PowerPoint 확인 방법은 [Windows 실행 파일 안내](docs/windows-exe.md)를 참고하세요.


## 실행 요구 사항

### .NET 6 Desktop Runtime (Windows)
현재 소스 코드는 `net6.0-windows`를 대상으로 합니다. 실행 파일과 같은 아키텍처의 .NET 6 Desktop Runtime이 필요합니다. 런타임이 포함된 배포본은 별도 설치가 필요하지 않습니다.

### Microsoft PowerPoint 2007 이상
PPT를 만들고 보는 데 필요한 프로그램입니다. 프로그램 구성 요소로 *Office 공유 기능* - *Visual Basic for Applications*를 설치해야 합니다.

### 로컬 본문 파일
실행 파일 옆의 `Bibles/krv.json`이 필요합니다. 본문 조회에는 인터넷 연결이 필요하지 않습니다.

원본 저장소의 배포본에는 이 로컬 본문 변경 기능이 포함되어 있지 않습니다.


## 기여 방법
**성경2PPT**는 당신의 기여를 기다리고 있습니다~ 사용 중 발생한 오류 혹은 추가하고 싶은 기능을 [Issues](https://github.com/sunghwan2789/Bible2PPT/issues) 페이지에 올려주세요!


## License
This software is licenced under the [MIT](LICENSE) © [Sunghwan Bang](https://github.com/sunghwan2789).
