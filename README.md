# Bible2PPT Windows 배포본

실제 Windows에서 x64·x86 배포 EXE를 직접 실행해 검증한 수정판입니다.
TXT의 이어진 본문·합쳐진 절 처리, 누락된 절과 빈 선택, 취소·저장 오류, 손상된 설정 파일 처리를 수정했습니다.

[64비트 Windows ZIP 다운로드](https://github.com/jjww2010-pixel/bible2ppt/raw/refs/heads/windows-download/Bible2PPT-win-x64.zip)

[32비트 Windows ZIP 다운로드](https://github.com/jjww2010-pixel/bible2ppt/raw/refs/heads/windows-download/Bible2PPT-win-x86.zip)

비공개 저장소이므로 접근 권한이 있는 GitHub 계정으로 로그인해야 합니다.
링크로 다운로드되지 않으면 위 파일 목록에서 ZIP 파일을 열고 **Download raw file** 버튼을 누르세요.

1. 기존 프로그램을 닫고, 새 ZIP을 쓰기 가능한 폴더에 모두 압축 해제합니다.
2. 기존에 가져온 본문을 유지하려면 기존 폴더의 `Bibles` 폴더를 새 폴더로 복사합니다.
3. `Bible2PPT.exe`를 실행합니다. 본문이 없으면 기존 JSON 또는 TXT 파일을 가져옵니다.

.NET 런타임은 포함되어 있습니다. PPT 제작에는 Microsoft PowerPoint가 필요합니다.
전체 개역개정 본문은 포함되어 있지 않습니다.

## 확인한 결과

검증한 소스: main 커밋 `8fa5d016ac871e848c2bc27f2c2dbee084f6907f`.

[실제 Windows 검증 실행](https://github.com/jjww2010-pixel/bible2ppt/actions/runs/38155387537)

- 각 Windows 실행에서 자동 테스트 125개 통과: C# 111개, Python 14개.
- 각 EXE로 66권·1,189장 형식의 31,101개 개별 절 요청을 확인했습니다. 테스트 본문은 직접 만든 문장입니다.
- 실제 메인 화면의 탐색, 책 검색·선택, 성경 선택, 설정, 기본 PPT 템플릿 추출을 확인했습니다.
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
