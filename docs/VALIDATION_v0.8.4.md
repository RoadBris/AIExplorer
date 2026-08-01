# v0.8.4 검증 기록

- 매핑 드라이브 루트 `Z:\`가 `Z:`로 축약되지 않는지 Windows 스모크 테스트를 추가했습니다.
- UNC 공유 루트 정규화와 UNC 경로 판별 스모크 테스트를 추가했습니다.
- 네트워크 위치 창이 고정 높이·고정 크기 구조로 되돌아가지 않는지 구조 검사에 추가했습니다.
- `WNetAddConnection3`, `Registry.CurrentUser`, `WNetGetConnection` 연결 계약과 12초 응답 제한을 구조 검사합니다.
- XAML 문법, 이벤트 핸들러, C# 괄호 구조, 기존 검색·AI·NuGet 계약을 함께 검사했습니다.
- 제한: 현재 Linux 작업 환경에는 Windows WPF·MPR 네트워크 API 실행 환경이 없어 실제 NAS 인증 창과 공유 폴더 접근은 Windows PC의 `verify_source.cmd` 및 수동 연결 확인이 필요합니다.
