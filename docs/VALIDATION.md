# 검증

## 소스 구조 검사

Windows가 아닌 환경에서도 다음 명령으로 XAML과 배포 계약을 검사할 수 있습니다.

```bash
python tools/validate_source.py
```

검사 항목:

- XAML XML 문법, 이벤트 처리기, StaticResource 참조
- 필수 소스와 SigLIP 2 SentencePiece 토크나이저 존재
- E5·SigLIP 2 고정 커밋, 파일명, SHA-256, 번들 스크립트 일치
- PowerShell 5.1용 UTF-8 BOM과 ASCII 안전 본문
- E5 `query:`/`passage:` 접두사와 768차원 색인
- `ctx 512 / batch 2048 / ubatch 512` 및 배치 한도 오류 재시도 구조
- DirectML 등록, CPU 폴백, 시각 입력·출력 이름
- 상위 50개 정밀 재평가, 의미·시각 신규 색인 예산
- 검색 입력 placeholder, 이미지 미리보기, 저장 위치 변경, 프로세스 종료 계약
- 즐겨찾기 하단 안내, 우클릭 추가, 드래그 순서 변경, 별도 네트워크 트리 제거 계약

## Windows 전체 검증

Windows 10/11과 .NET 10 SDK가 있는 PC에서는 다음을 실행합니다.

```cmd
verify_source.cmd
```

이 명령은 Release 빌드와 `AIExplorer.SmokeTests`를 실행합니다. 실제 배포 번들은
다음 명령으로 만듭니다.

```cmd
build_release.cmd
```

빌드 스크립트는 모델과 토크나이저의 SHA-256을 확인하고 self-contained Windows
x64 배포 폴더에 E5·SigLIP 2·llama.cpp를 포함합니다.

## 수동 확인 권장 항목

1. 모델이 없는 새 데이터 폴더에서 초기 설치를 완료합니다.
2. 내장그래픽 PC에서 로그에 DirectML 세션 오류가 없는지 확인합니다.
3. DirectML이 지원되지 않는 PC에서도 CPU 폴백으로 이미지 검색이 계속되는지 확인합니다.
4. `노을 사진`, `강아지 이미지`, 실제 캐릭터명 등 파일명에 없는 시각 질의를 시험합니다.
5. 한국어와 영어가 섞인 문서 질의에서 E5 결과가 반환되는지 확인합니다.
6. 137토큰 이상이던 정밀 재평가 사례가 오류 없이 완료되는지 확인합니다.
7. 넓은 상위 폴더에서 여러 자식 폴더의 이미지가 점진적으로 색인되는지 확인합니다.
8. 앱 종료 시 앱이 새로 연 사진 뷰어와 로컬 AI 프로세스가 남지 않는지 확인합니다.

## 현재 환경의 한계

이 소스를 수정한 환경에는 .NET 10 SDK와 Windows WPF·DirectML 런타임이 없어
실제 WPF 컴파일, Windows OCR, PDF WinRT 렌더링, llama.cpp GGUF 추론과 SigLIP 2
DirectML 실행은 수행하지 못했습니다. `validate_source.py`의 구조 검사와 압축파일
무결성 검사는 수행할 수 있지만, 배포 전에 Windows에서 `verify_source.cmd`를
반드시 실행해야 합니다.
