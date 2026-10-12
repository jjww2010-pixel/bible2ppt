# Bible2PPT Windows 배포본

개역개정·쉬운성경·현대인의 성경을 각각 등록하고 선택할 수 있는 Windows 배포본입니다.
실제 Windows에서 x64·x86 EXE를 직접 실행해 세 번역본의 선택과 독립 저장을 검증했습니다.

[64비트 Windows ZIP 다운로드](https://github.com/jjww2010-pixel/bible2ppt/raw/refs/heads/windows-download/Bible2PPT-win-x64.zip)

[32비트 Windows ZIP 다운로드](https://github.com/jjww2010-pixel/bible2ppt/raw/refs/heads/windows-download/Bible2PPT-win-x86.zip)

비공개 저장소이므로 접근 권한이 있는 GitHub 계정으로 로그인해야 합니다.
링크로 다운로드되지 않으면 위 파일 목록에서 ZIP 파일을 열고 **Download raw file** 버튼을 누르세요.

1. 기존 프로그램을 닫고, 새 ZIP을 쓰기 가능한 폴더에 모두 압축 해제합니다.
2. 기존에 가져온 본문을 유지하려면 기존 폴더의 `Bibles` 폴더를 새 폴더로 복사합니다.
3. `Bible2PPT.exe`를 실행합니다. 기존 개역개정 본문은 그대로 사용할 수 있습니다.
4. 설정에서 쉬운성경 또는 현대인의 성경을 고르고 **성경 본문 가져오기**로 해당 번역의 JSON/TXT를 가져옵니다.
5. 제작 화면의 소스 목록에서 사용할 번역을 선택합니다. 하나만 제작하려면 다른 번역을 제작 목록에서 제거합니다.

본문은 `Bibles/krv.json`(개역개정), `Bibles/easy.json`(쉬운성경), `Bibles/modern.json`(현대인의 성경)에 따로 저장됩니다.
다른 번역을 가져와도 기존 번역의 파일을 덮어쓰지 않습니다.

.NET 런타임은 포함되어 있습니다. PPT 제작에는 Microsoft PowerPoint가 필요합니다.
세 번역본의 전체 본문은 포함되어 있지 않습니다. 이번 배포는 선택·가져오기 기능이며, 사용할 본문 파일은 별도로 준비해야 합니다.

## 확인한 결과

검증한 소스: main 커밋 `be4c37fafcc456b54b2856ef8f640ea3ff64c90a`.

[실제 Windows 검증 실행](https://github.com/jjww2010-pixel/bible2ppt/actions/runs/38277535169)

- 각 Windows 실행에서 자동 테스트 130개 통과: C# 116개, Python 14개.
- 각 EXE의 세 번역 저장 경로에서 66권·1,189장 형식의 31,101개 개별 절 요청을 확인했습니다. 테스트 본문은 직접 만든 문장으로, 실제 번역 원문은 아닙니다.
- 실제 메인 화면의 탐색, 책 검색·선택, 세 번역본 선택·제작 대상 추가, 설정, 기본 PPT 템플릿 추출을 확인했습니다.
- 실제 제작 로직에서 세 번역이 선택한 순서대로 전달되는지, 교체·재시작 후 번역별 ID와 파일이 유지되는지 확인했습니다.
- UTF-8/CP949 TXT, JSON, 잘못된 파일과 없는 파일, 한글·공백 경로, 잘린 설정 파일과 잘못된 설정 값을 확인했습니다.
- ZIP에는 검사를 통과한 Windows 빌드 파일을 그대로 담았습니다. SHA-256 파일도 함께 제공합니다.

상세 결과: [x64](Verification/win-x64/README.md), [x86](Verification/win-x86/README.md).

Windows 검증 서버에는 Microsoft Office가 없어 **PowerPoint를 통한 실제 슬라이드 생성·렌더링은 검증하지 못했습니다**.
제작 로직의 오류·취소 테스트는 Office 쓰기 부분을 테스트 대역으로 교체합니다.
모든 오류가 없다는 보장은 아니며, 위 결과는 기록된 검사 범위에 해당합니다.

`Check-Bible.cmd`는 가져온 본문 전체를 한 번에 검사합니다.
`Check-PowerPoint.cmd`는 PowerPoint가 설치된 PC에서 예시 PPT 생성과 저장 내용을 자동 검사합니다.
향후 main 소스 변경에도 Windows 자동 검증이 실행됩니다.

이 브랜치는 배포 파일 다운로드용입니다. 수정할 소스 코드는 main 브랜치에 있습니다.
