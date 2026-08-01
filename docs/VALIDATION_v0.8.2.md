# v0.8.2 검증 기록

## 복원 오류 수정

이 버전은 프로젝트 루트의 `NuGet.Config`를 모든 복원 명령에 명시적으로 전달합니다.
따라서 사용자 전역 NuGet 설정에 nuget.org가 없거나 비활성화되어 있어도 다음 패키지를 공식 피드에서 복원합니다.

- Microsoft.ML.OnnxRuntime.DirectML 1.22.0
- Microsoft.ML.Tokenizers 2.0.0
- Microsoft.Windows.SDK.NET.Ref 10.0.19041.57

## 확인 항목

- `NuGet.Config` XML 파싱
- 패키지 소스가 `https://api.nuget.org/v3/index.json` 하나로 고정됨
- 검증·개발·배포 스크립트가 같은 설정 파일을 사용
- 배포용 `win-x64` 런타임 복원이 publish 전에 실행됨
- 프로젝트·테스트 프로젝트 XML 파싱
- 기존 소스 구조 검사

실제 패키지 다운로드와 Windows WPF 실행은 인터넷 연결이 가능한 Windows PC의 `verify_source.cmd`에서 최종 확인합니다.
