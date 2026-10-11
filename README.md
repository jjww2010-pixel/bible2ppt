# Bible2PPT Windows 배포본

TXT 가져오기 오류 수정판: 1049번째 줄에서 실패하던 이어진 본문과 여러 절을 합친 본문을 지원합니다. 기존 TXT를 수정하지 않고 다시 가져올 수 있습니다.

[64비트 Windows ZIP 다운로드](https://github.com/jjww2010-pixel/bible2ppt/raw/refs/heads/windows-download/Bible2PPT-win-x64.zip)

[32비트 Windows ZIP 다운로드](https://github.com/jjww2010-pixel/bible2ppt/raw/refs/heads/windows-download/Bible2PPT-win-x86.zip)

비공개 저장소이므로 이 저장소에 접근할 수 있는 GitHub 계정으로 로그인해야 합니다.
링크로 다운로드되지 않으면 위 파일 목록에서 ZIP 파일을 열고 **Download raw file** 버튼을 누르세요.

1. ZIP을 쓰기 가능한 폴더에 모두 압축 해제합니다.
2. `Bible2PPT.exe`를 실행합니다.
3. 본문 가져오기 창에서 준비한 JSON 또는 TXT 파일을 선택합니다.

.NET 런타임은 포함되어 있습니다. PPT 제작에는 Microsoft PowerPoint가 필요합니다.
전체 개역개정 본문은 포함되어 있지 않습니다.

소스: main 커밋 `fc9c6e151f86245ce4aa18f12a0565a7b3dede77`.
Windows x64/x86 빌드와 ZIP 무결성은 확인했으며, 실제 Windows/PowerPoint 실행은 아직 확인하지 못했습니다.
`Check-PowerPoint.cmd`로 사용 중인 PC에서 PPT 생성 동작을 검사할 수 있습니다.

이 브랜치는 배포 파일 다운로드용입니다. 수정할 소스 코드는 main 브랜치에 있습니다.
