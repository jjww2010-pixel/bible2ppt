# Windows 실행 파일 사용

이 변경판은 `Bible2PPT.exe`를 실행해서 사용하는 Windows 앱입니다.
배포본에는 .NET 런타임이 포함되며 Python이나 별도의 .NET 설치 없이 실행할 수 있습니다.
PPT 제작에는 원작과 같이 Microsoft PowerPoint가 설치되어 있어야 합니다.

## 실행

1. Windows 배포 ZIP을 **쓰기 가능한 폴더에 모두 압축 해제**합니다.
2. `Bible2PPT.exe`를 실행합니다. ZIP 안에서 바로 실행하지 마세요.
3. 본문 파일이 없으면 파일 가져오기 안내가 표시됩니다. 사용할 수 있는 개역개정 JSON 또는 TXT 파일을 선택합니다.
4. 로컬 성경이 제작 대상에 추가되면 `창1:1-2` 같은 구절을 입력하고 PPT를 제작합니다.

본문은 설정 → **성경 본문 가져오기**에서 교체할 수도 있습니다. Python 명령을 실행할 필요가 없습니다.
가져온 본문은 실행 파일 옆의 `Bibles/krv.json`으로 변환·저장됩니다.
JSON은 앱의 계층형 형식과 구절별 배열 형식을 지원하고, TXT는 `창1:1 본문` 또는 TSV 형식을 지원합니다.
UTF-8과 CP949를 지원하며, 잘못된 파일을 가져와도 기존 본문을 유지합니다.
책·장만 반복된 이어진 본문과 `18-19`처럼 합쳐진 절도 가져올 수 있습니다.
합쳐진 절은 본문을 한 번만 넣고 슬라이드에 원래 절 범위를 표시합니다.
자세한 형식은 [로컬 본문 안내](local-bible.md)를 참고하세요.

전체 개역개정 본문은 배포본에 포함되어 있지 않습니다. 형식 예시는 실제 본문이 아니며 자동으로 로드하지 않습니다.

## PowerPoint 동작 확인

압축을 해제한 폴더에서 `Check-PowerPoint.cmd`를 실행하면 **배포 EXE 자체**가 PowerPoint를 호출해
예시 문장 두 개를 PPT로 만들고, 저장된 슬라이드 XML에 본문이 들어 있는지 검사합니다.
성공하면 `PowerPoint-check.pptx`와 `PowerPoint-check.txt`가 생성됩니다.
이 확인은 실제 개역개정 본문을 사용하지 않으므로 본문 파일이 없어도 가능합니다.
PowerPoint 설치나 COM 초기화, 템플릿, 본문 치환이 실패하면 실패 결과를 기록합니다.

## 자동 배포본

저장소의 Actions → **Windows EXE**에서 성공한 실행을 열고
`Bible2PPT-win-x64` 또는 `Bible2PPT-win-x86` 산출물을 내려받을 수 있습니다.
실행 중인 프로세스, 개발 도구, 빌드 서버의 Office 설치에 의존하지 않는 배포본입니다.

## 개발자 빌드

.NET 8 이상 SDK에서 다음 명령으로 생성합니다. 빌드 머신에는 Office가 없어도 됩니다.
Linux에서 교차 빌드할 때는 `-p:EnableWindowsTargeting=true`를 추가합니다.

```sh
dotnet publish Bible2PPT/Bible2PPT.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -o artifacts/win-x64
```

Windows 자동 빌드는 생성한 EXE를 직접 실행해서 런타임 시작, SQLite 초기화, 본문 조회,
잘못된 본문 파일 거부를 검증합니다. 테스트 데이터는 형식 예시의 안내 문장입니다.
이 검증은 화면 조작이나 PowerPoint 슬라이드 생성 검증을 대신하지 않습니다.
최종 사용 환경에서는 실제 본문을 가져온 뒤 PPT를 제작하고 슬라이드 내용을 확인해야 합니다.
