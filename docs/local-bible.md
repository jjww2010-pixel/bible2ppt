# 로컬 개역개정 본문 사용

## 본문 파일 준비

`Bible2PPT.exe`에서는 파일이 없을 때 표시되는 안내 또는 설정 → **성경 본문 가져오기**로
JSON·TXT·TSV 파일을 직접 가져올 수 있습니다. 프로그램이 형식을 변환하고 본문을 즉시 다시 읽습니다.
Python을 실행하거나 프로그램을 재시작할 필요가 없습니다. [EXE 사용 안내](windows-exe.md)를 참고하세요.

1. 사용할 수 있는 개역개정 본문을 UTF-8 JSON 파일로 준비합니다. 전체 본문은 이 저장소에 포함되어 있지 않습니다.
2. `Bible2PPT.exe`와 같은 폴더에 `Bibles` 폴더를 만들고 `krv.json`을 넣습니다.
3. 프로그램을 시작합니다. 소스는 **로컬 성경 (JSON)**, 성경은 JSON의 `name`으로 표시됩니다.
4. 성경을 PPT 제작 대상에 추가하고 기존 방식으로 `창1:1-2` 등의 구절을 입력합니다.

```text
배포 폴더/
  Bible2PPT.exe
  Bibles/
    krv.json
```

파일 경로는 실행 파일 위치를 기준으로 합니다. 프로그램을 실행한 작업 폴더에 따라 바뀌지 않습니다.
소스에서 개발할 때는 `Bible2PPT/Bibles/krv.json`에 본문을 넣으면 빌드·게시 출력의 `Bibles` 폴더로 복사됩니다.
단일 실행 파일로 게시해도 JSON은 외부 파일로 남습니다. 본문 파일은 Git에서 제외합니다.

`Bible2PPT/Bibles/krv.example.json`은 **형식 예시**이며 실제 성경 본문이 아닙니다.
예시의 안내 문장을 실제 본문으로 교체하고 필요한 책·장·절을 추가한 뒤 `krv.json`으로 저장하세요.
프로그램은 예시 파일을 본문으로 자동 사용하지 않습니다.

## JSON 형식

```json
{
  "name": "개역개정",
  "languageCode": "ko",
  "books": [
    {
      "key": "Genesis",
      "name": "창세기",
      "abbreviation": "창",
      "chapters": [
        {
          "number": 1,
          "verses": [
            { "number": 1, "text": "실제 1절 본문" },
            { "number": 2, "text": "실제 2절 본문" }
          ]
        }
      ]
    }
  ]
}
```

- `name`, `languageCode`, 책의 `name`, `abbreviation`, 각 절의 `text`는 비어 있을 수 없습니다.
- `key`는 아래 표의 문자열을 사용합니다. 신약 앞에 외경 항목이 있는 내부 열거형이므로 1~66 등의 숫자 ID를 사용하지 마세요.
- `books`, 각 책의 `chapters`, 각 장의 `verses`에는 최소 한 항목이 필요합니다.
- 장·절의 `number`는 1 이상의 정수입니다. 같은 책의 장 번호, 같은 장의 절 번호, 책의 `key`는 중복될 수 없습니다.
- 책·장·절은 읽을 때 순서대로 정렬합니다. 본문 문자열은 그대로 유지합니다.
- 일부 책·장·절만 있는 파일도 읽을 수 있습니다. 전체 66권의 완전성을 검사하거나 누락된 본문을 자동 보충하지 않으므로, 필요한 구절이 모두 들어 있는지 확인하세요.
- TXT 파일과 구절별 JSON 배열은 아래 변환 도구로 이 구조로 변환해서 사용하세요.

## TXT·구절별 JSON 가져오기

Python 3.8 이상에서 `tools/import_bible.py`를 사용합니다. 사용할 수 있는 개역개정 본문 파일을
`local-bible-input` 폴더에 두세요. 이 폴더와 변환된 `krv.json`은 Git에 올라가지 않습니다.

TXT는 한 줄에 `창1:1 본문`, `창세기 1:1 본문` 형태를 지원합니다.
`창35:앞 구절에 이어지는 테스트 문장`처럼 책·장만 반복된 줄은 바로 앞의 같은 책·장 구절에 이어 붙입니다.
`신6:18-19 통합 테스트 문장`처럼 여러 절을 합친 본문도 지원합니다. JSON에는 `number: 18`,
`endNumber: 19`로 한 번만 저장하며, 19절만 요청해도 통합 본문을 포함하고 슬라이드 절 번호는 `18-19`로 표시합니다.
통합 본문을 임의로 나누거나 중복 삽입하지 않습니다. 겹치는 절 범위와 앞 구절이 없는 이어진 줄은 오류로 안내합니다.
TSV는 책·장·절·본문을 탭으로 구분합니다. 빈 줄과 `#`으로 시작하는 주석은 무시합니다.
일반 제목 줄은 임의로 버리지 않고 오류로 안내하므로 변환 전에 정리하세요.

구절별 JSON 입력은 다음과 같은 배열입니다. `text` 대신 `content`를 사용할 수도 있습니다.

```json
[
  { "book": "창", "chapter": 1, "verse": 1, "text": "실제 본문" },
  { "book": 40, "chapter": 1, "verse": 1, "content": "실제 본문" }
]
```

입력 `book`에는 한국어 이름·약자, 위의 `key` 문자열, 또는 **1~66의 정경 순서** 번호를 사용합니다.
입력 번호 40은 마태복음이며, 출력 JSON에는 `Matthew`로 저장됩니다.
번역 이름은 직접 지정해야 합니다. 파일이 실제 개역개정인지 자동으로 판별하지 않습니다.

```powershell
python tools/import_bible.py local-bible-input/nkrv.txt Bible2PPT/Bibles/krv.json --name 개역개정
python tools/import_bible.py local-bible-input/nkrv.json Bible2PPT/Bibles/krv.json --name 개역개정
```

UTF-8이 기본입니다. CP949 파일은 `--encoding cp949`를 추가하세요.
이미 있는 출력 파일을 교체할 때는 `--overwrite`가 필요합니다.
`--source`로 자료 출처를 출력 JSON에 기록할 수 있습니다.
변환 오류가 발생하면 기존 출력 파일을 유지하고 실패 상태를 반환합니다.

공개 본문 자료 조사 결과는 [본문 자료 출처](bible-data-sources.md)를 참고하세요.

## 파일 교체와 기존 데이터

프로그램은 시작 후 처음 읽은 파일을 실행 중에 공유합니다. 본문을 교체하거나 파일 오류를 수정한 뒤 **프로그램을 다시 시작**하세요.
설정의 **성경 본문 가져오기**를 사용하면 프로그램이 본문을 다시 읽으므로 재시작하지 않아도 됩니다.
로컬 목차·본문은 SQLite 캐시에서 읽지 않습니다. 이전 웹사이트 캐시를 지울 필요가 없습니다.
성경의 식별 정보만 저장하므로 파일을 교체해도 기존 로컬 성경 선택을 유지합니다.

기존 웹 소스의 ID 0~2는 재사용하지 않고 로컬 소스에는 ID 3을 사용합니다.
웹 소스는 선택 목록에서 제거했습니다. 이전 웹 성경 제작 대상은 자동 복원하지 않으므로 로컬 성경을 다시 추가하세요.
예전 웹 성경 제작 기록을 다시 제작할 때도 로컬 성경으로 새 작업을 만드세요.
웹 소스 구현 파일은 남겨 두었지만 로컬 소스에서 호출하지 않습니다.

본문 파일이 없거나 JSON이 잘못되면 파일 경로와 원인을 안내합니다. 웹사이트로 자동 전환하지 않습니다.

## 성경 책 key

| 책 이름 | 약자 | key |
| --- | --- | --- |
| 창세기 | 창 | `Genesis` |
| 출애굽기 | 출 | `Exodus` |
| 레위기 | 레 | `Leviticus` |
| 민수기 | 민 | `Numbers` |
| 신명기 | 신 | `Deuteronomy` |
| 여호수아 | 수 | `Joshua` |
| 사사기 | 삿 | `Judges` |
| 룻기 | 룻 | `Ruth` |
| 사무엘상 | 삼상 | `ISamuel` |
| 사무엘하 | 삼하 | `IISamuel` |
| 열왕기상 | 왕상 | `IKings` |
| 열왕기하 | 왕하 | `IIKings` |
| 역대상 | 대상 | `IChronicles` |
| 역대하 | 대하 | `IIChronicles` |
| 에스라 | 스 | `Ezra` |
| 느헤미야 | 느 | `Nehemiah` |
| 에스더 | 에 | `Esther` |
| 욥기 | 욥 | `Job` |
| 시편 | 시 | `Psalms` |
| 잠언 | 잠 | `Proverbs` |
| 전도서 | 전 | `Ecclesiastes` |
| 아가 | 아 | `SongOfSolomon` |
| 이사야 | 사 | `Isaiah` |
| 예레미야 | 렘 | `Jeremiah` |
| 예레미야애가 | 애 | `Lamentations` |
| 에스겔 | 겔 | `Ezekiel` |
| 다니엘 | 단 | `Daniel` |
| 호세아 | 호 | `Hosea` |
| 요엘 | 욜 | `Joel` |
| 아모스 | 암 | `Amos` |
| 오바댜 | 옵 | `Obadiah` |
| 요나 | 욘 | `Jonah` |
| 미가 | 미 | `Micah` |
| 나홈 | 나 | `Nahum` |
| 하박국 | 합 | `Habakkuk` |
| 스바냐 | 습 | `Zephaniah` |
| 학개 | 학 | `Haggai` |
| 스가랴 | 슥 | `Zechariah` |
| 말라기 | 말 | `Malachi` |
| 마태복음 | 마 | `Matthew` |
| 마가복음 | 막 | `Mark` |
| 누가복음 | 눅 | `Luke` |
| 요한복음 | 요 | `John` |
| 사도행전 | 행 | `Acts` |
| 로마서 | 롬 | `Romans` |
| 고린도전서 | 고전 | `ICorinthians` |
| 고린도후서 | 고후 | `IICorinthians` |
| 갈라디아서 | 갈 | `Galatians` |
| 에베소서 | 엡 | `Ephesians` |
| 빌립보서 | 빌 | `Philippians` |
| 골로새서 | 골 | `Colossians` |
| 데살로니가전서 | 살전 | `IThessalonians` |
| 데살로니가후서 | 살후 | `IIThessalonians` |
| 디모데전서 | 딤전 | `ITimothy` |
| 디모데후서 | 딤후 | `IITimothy` |
| 디도서 | 딛 | `Titus` |
| 빌레몬서 | 몬 | `Philemon` |
| 히브리서 | 히 | `Hebrews` |
| 야고보서 | 약 | `James` |
| 베드로전서 | 벧전 | `IPeter` |
| 베드로후서 | 벧후 | `IIPeter` |
| 요한일서 | 요일 | `IJohn` |
| 요한이서 | 요이 | `IIJohn` |
| 요한삼서 | 요삼 | `IIIJohn` |
| 유다서 | 유 | `Jude` |
| 요한계시록 | 계 | `Revelation` |

표의 책 이름·약자는 기존 한국어 검색 인덱스의 표기입니다. JSON의 `key`는 이 인덱스와 PPT 구절 입력을 연결합니다.

## 개발 및 검증

.NET 8 이상 SDK와 .NET 6 런타임을 설치한 환경에서 본문 조회 테스트를 실행할 수 있습니다.
다음 테스트는 PowerPoint 없이 실행되며 테스트용 문자열을 사용합니다.

```sh
dotnet test Bible2PPT.Services.BibleService.Tests/Bible2PPT.Services.BibleService.Tests.csproj
dotnet test Bible2PPT.Services.BibleIndexService.Tests/Bible2PPT.Services.BibleIndexService.Tests.csproj
python -m unittest discover -s tools -p "test_*.py"
```

전체 Windows 앱은 .NET SDK로 빌드할 수 있습니다. 빌드 머신에는 PowerPoint 설치가 필요하지 않습니다.
PPT 제작에는 PowerPoint가 필요합니다. 배포본에는 .NET 런타임이 포함됩니다.

```powershell
dotnet publish Bible2PPT/Bible2PPT.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -o artifacts/win-x64
```

Windows에서는 본문 파일을 배치하고 `창1:1-2`로 PPT를 제작해 슬라이드 본문을 확인하세요.
인터넷 연결을 끈 상태에서도 같은 PPT가 만들어지는지 확인하면 로컬 사용을 검증할 수 있습니다.
