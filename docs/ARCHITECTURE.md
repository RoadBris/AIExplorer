# 구조

## 주요 계층

| 계층 | 역할 |
| --- | --- |
| `MainWindow` | 탐색기 + 하단 세로 검색 패널 UI, 검색·색인·파일 작업 조정 |
| `MetadataIndexService` | 범위별 파일명·경로·형식 메타데이터 색인 |
| `ContentIndexService` | 문서 본문과 Windows OCR 결과의 압축 색인 |
| `SearchQueryInterpreter` | 한국어 질의, 동의어, 파일 형식, 수정 시기 해석 |
| `NaturalLanguageSearchService` | Qwen3 로컬 LLM으로 검색 문장을 스키마 제한 `SearchPlan`으로 변환 |
| `SearchPlanCompiler` | LLM 계획과 명시적인 규칙 조건을 결합하고 기존 `SearchIntent`로 검증 |
| `FileMetadataDescriptor` | 모든 파일의 이름·확장자 의미·상위 경로를 안전한 의미 색인 문장으로 구성 |
| `SearchRankingService` | 이름·경로·본문·AI 후보의 혼합 순위 |
| `AiModelManager` | Qwen3·E5·SigLIP 2·llama.cpp 설치, 버전·SHA-256 검증 |
| `LocalEmbeddingService` | Multilingual E5 GGUF를 인증된 로컬 llama.cpp 서버로 실행 |
| `SemanticIndexService` | E5 768차원 의미 벡터의 int8 증분 색인·검색 |
| `SiglipTokenizer` | SigLIP 2 SentencePiece 토큰화와 64토큰 입력 구성 |
| `VisualFrameLoader` | 이미지·PDF 표본 페이지를 224×224 RGB 텐서로 변환 |
| `LocalVisualEmbeddingService` | SigLIP 2 ONNX DirectML 우선 추론과 CPU 폴백 |
| `VisualIndexService` | SigLIP 2 768차원 시각 벡터의 int8 증분 색인·검색 |
| `VisualQueryPromptBuilder` | 원문·영문 별칭·고유명 음역을 포함한 다중 시각 문구 생성 |
| `AdvancedAnalysisService` | 상위 50개 결과의 E5 전체 768차원 정밀 재평가 |
| `MetadataSearchService` | 메타데이터·본문·E5·SigLIP 2 후보 혼합 |
| `SettingsService` | 포터블 설정과 데이터 폴더 이동 |
| `LaunchedProcessTracker` | 앱이 새로 연 외부 뷰어를 종료 시 정리 |

개별 AI 추론이 실패해도 이미 계산한 메타데이터와 본문 결과는 유지합니다. 파일
변경 작업은 검색·임베딩 계층과 분리되어 있습니다.

## 검색 파이프라인

1. 정확 규칙과 Qwen3가 문장을 파일/폴더, 핵심 개념, 관련 표현, 날짜·정렬, 이전 결과 범위가 포함된 `SearchPlan`으로 변환합니다.
2. JSON 스키마와 허용 목록을 검사한 뒤 명시적인 확장자·파일/폴더·날짜 조건을 우선해 `SearchIntent`로 컴파일합니다.
3. 준비된 검색 위치에서 메타데이터·본문·OCR 색인을 읽거나 갱신합니다.
4. 파일명·경로·확장자 의미·수정 시기의 직접 단서를 계산하며, 일부 단서만 맞는 후보도 후속 결합을 위해 보존합니다.
5. 지원 문서는 본문을, 이미지·PDF는 Windows 로컬 OCR 결과를 최대 12,000자로 저장합니다.
6. E5 입력은 질의에 `query:`, 파일 정보에 `passage:`를 붙입니다.
7. 짧은 검색은 대표 240자 한 구간, 정밀 재평가는 최대 5개 대표 구간으로 나눕니다.
8. 각 E5 구간의 768차원 벡터를 평균 정규화하고 int8로 저장합니다.
9. 이미지·PDF 표본은 SigLIP 2용 224×224, 평균 0.5·표준편차 0.5 텐서로 변환합니다.
10. SigLIP 2는 원문과 여러 시각 문구를 비교하고 가장 높은 유사도를 사용합니다.
11. 이름·경로·본문·OCR·E5·SigLIP 2의 독립 후보 순위를 결합해 최대 500개를 반환합니다.
12. 사용자가 `정밀 재평가`를 누르면 상위 50개의 파일명·경로·본문을 다시 읽어 E5 전체 벡터로 재정렬합니다.

## 정밀 재평가 입력 처리

E5 로컬 서버는 `ctx-size 512`, `batch-size 2048`, `ubatch-size 512`로 실행합니다.
한 번에 최대 4개 대표 구간만 전송하며, 서버가 물리 배치 또는 토큰 한도 오류를
반환하면 요청을 한 문장씩 분리합니다. 한 문장도 길면 본문을 더 줄여 재시도해
이전의 `137 tokens > physical batch size 128` 오류가 버튼 전체를 무력화하지 않게
했습니다.

## DirectML 시각 추론

`LocalVisualEmbeddingService`는 먼저 DirectML 장치 0으로 SigLIP 2 세션을 엽니다.
DirectML 규칙에 맞게 순차 실행과 메모리 패턴 비활성화를 사용합니다. 세션 생성이나
실제 추론 중 호환되지 않는 연산이 발견되면 세션을 폐기하고 CPU 세션을 다시 열어
같은 검색을 이어갑니다. 시각 추론은 한 파일씩 직렬화해 내장그래픽 공유 메모리의
급격한 증가를 막습니다.

## 데이터 저장

기본 위치는 EXE 옆 `_AIExplorer_Data`입니다.

- `settings.json`: UI와 등록 네트워크 위치
- `index/`: 메타데이터 색인
- `content-index/`: 본문·OCR 압축 색인
- `semantic-index/`: E5 768차원 int8 색인, 형식 버전 6
- `visual-index/`: SigLIP 2 768차원 int8 색인, 형식 버전 3
- `models/semantic/`: E5 GGUF, llama.cpp, 버전 표식과 로그
- `models/visual/`: SigLIP 2 INT8 ONNX, SentencePiece 모델과 버전 표식
- `models/language/`: Qwen3 GGUF, 버전 표식과 로컬 실행 로그
- `logs/`: 앱 진단 로그

모델 ID와 색인 형식이 v0.6.4와 달라 기존 Nomic·OpenCLIP 벡터는 자동으로
재사용되지 않습니다. 설치가 끝나면 오래된 모델 파일을 정리하고 새 색인을
백그라운드에서 만듭니다.

## 성능 원칙

- 시작 자동 색인: 위치당 의미 192개, 시각 48개
- 활성 검색: 의미 신규 최대 384개, 시각 신규 최대 256개
- 시각 비교 후보 최대 500개, 최종 검색 결과 최대 500개
- 넓은 상위 폴더에서는 자식 폴더별 라운드 로빈으로 이미지를 선택
- 검색 입력 중 자동 색인을 중단하고, 검색 후 낮은 우선순위로 재개
- E5 요청은 최대 4개씩, CPU 스레드는 최대 6개
- SigLIP 2 추론은 DirectML 또는 CPU에서 한 파일씩 실행
- PDF는 첫쪽·가운데쪽·마지막쪽 최대 3개 표본만 OCR·시각 분석
- 손상 이미지·PDF 실패는 24시간 기억하고 파일 변경 시 다시 시도
- 정밀 재평가는 상위 50개만 계산하고 별도 색인에는 저장하지 않음
- 본문 미지원 형식도 실제 내용을 열지 않고 파일명·확장자·상위 경로만 의미 색인


## v0.9 점진 검색 파이프라인

1. 현재 사용 가능한 메타데이터·본문 색인만 읽고 AI를 실행하지 않는 빠른 결과를 먼저 표시합니다.
2. 색인이 없는 범위는 파일명·경로 직접 탐색으로 한 번 보완해 사용 가능한 결과를 빠르게 늘립니다.
3. 메타데이터, 본문·OCR, Multilingual E5, SigLIP 2 색인을 작은 단계로 확장합니다.
4. 각 단계가 끝날 때 새 부분 색인을 다시 검색하고, 결과 목록 전체를 지우지 않은 채 새 항목을 삽입하거나 기존 항목의 근거와 순위를 강화합니다.
5. 사용자가 중지하면 지금까지 찾은 결과는 유지하고, 미완료 색인은 유휴 시간 자동 색인으로 이어서 처리합니다.
6. 파일명·본문 정확 일치는 AI 후보보다 항상 우선하며, 빠른 첫 단계에서는 E5·SigLIP 모델을 기동하지 않습니다.
