# AIExplorer v0.11.1 검증 기록

## 수정 대상

- `tests/AIExplorer.SmokeTests/Program.cs`의 중복 `accountResponse` 선언을
  `mullvadAccountResponse`로 분리했습니다.
- `MainWindow.xaml.cs`의 백그라운드 `Dispatcher.BeginInvoke` 반환값을
  명시적으로 버려 CS4014 경고를 제거했습니다.

## 새 릴리스 차단 규칙

- 앱과 스모크 테스트 프로젝트에 `TreatWarningsAsErrors=true`를 적용했습니다.
- 교차 플랫폼 검증기는 같은 중괄호 범위에 있는 `var name =` 중복 선언을
  CS0128 회귀로 차단합니다.
- 검증기는 중복 선언 표본을 내부에서 주입해 탐지 로직 자체도 검사합니다.
- Windows PowerShell 사전 검사는 계정 검색 시나리오 변수 분리와
  Dispatcher 호출 처리를 확인합니다.
- `build_release.cmd`는 기존과 같이 `verify_source.cmd`가 성공한 뒤에만
  게시와 AI 번들 압축을 시작합니다.

## 이 환경에서 실행한 검사

- `python3 tools/validate_source.py`
- 중복 `accountResponse` 표본 주입 및 탐지 확인
- XAML XML·이벤트·정적 리소스 검사
- C# 구문 구조와 동일 범위 지역 변수 검사
- PowerShell UTF-8 BOM 및 CMD CRLF 검사

현재 Linux 환경에는 .NET 10 Windows WPF SDK가 없어 실제 Windows WPF 빌드와
스모크 테스트 실행은 수행하지 못했습니다. Windows에서 `verify_source.cmd`를
통과해야만 `build_release.cmd`가 배포 패키지를 생성합니다.
