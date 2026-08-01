# AIExplorer v0.12.0 검증 기록

## 엑셀 내용 검색 계약

- `ExcelDataReader 3.9.0`의 행 단위 리더로 XLS·XLSX·XLSM·XLSB를 읽습니다.
- 시트명과 셀 값을 중복 제거해 검색 텍스트로 저장합니다.
- 위치당 본문 색인에서 스프레드시트를 먼저 처리합니다.
- 현재 질의와 파일명·경로가 맞는 스프레드시트는 점진 색인의 최우선 대상입니다.
- 파일명 단서와 셀 단서가 합쳐져 전체 검색어를 충족하면
  `SearchEvidenceKind.Combined`로 분류합니다.
- 콘텐츠 색인 형식 6 이전 파일은 재사용하지 않습니다.

## 결과창 분리 계약

- 제목 검색 진행 콜백은 `TitleSearchResults`만 갱신합니다.
- 제목 검색 콜백에서 `MergeProgressiveSearchResults`를 호출하면
  사전 검사가 실패합니다.
- 통합 검색은 저장된 메타데이터·본문·OCR·의미·시각 색인을 독립적으로 병합합니다.

## 회귀 자료

스모크 테스트가 실제 Open XML XLSX 파일을 생성합니다. 파일 제목은
`계정 목록.xlsx`, 내부 셀 장비명은 `NEPTUNE-FW-8842`입니다.

- 시트명 `장비 계정`과 내부 장비명이 추출되는지 확인합니다.
- `NEPTUNE-FW-8842 계정 문서를 찾아줘`에서 해당 파일이 첫 결과인지 확인합니다.
- 결과가 `이름·내용 일치`이며 엑셀 셀 근거를 표시하는지 확인합니다.

## 이 환경에서 실행한 검사

- `python3 tools/validate_source.py`
- Tree-sitter C# 구문 분석 49개 파일
- XAML XML·이벤트·정적 리소스 검사
- 동일 범위 지역 변수 중복 및 경고 오류 승격 계약 검사
- PowerShell UTF-8 BOM 및 CMD CRLF 검사

현재 Linux 환경에는 .NET 10 Windows WPF SDK가 없어 실제 Windows 빌드와
스모크 테스트 실행은 수행하지 못했습니다. Windows에서 `verify_source.cmd`가
통과해야 `build_release.cmd`가 배포 패키지를 생성합니다.
