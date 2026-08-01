# 외부 AI 구성 요소

AI 탐색기 v0.82.4는 다음 공개 구성 요소를 로컬에서만 사용합니다.

| 구성 요소 | 용도 | 고정 버전 | 라이선스 |
| --- | --- | --- | --- |
| ExcelDataReader | XLS·XLSX·XLSM·XLSB 시트와 셀의 로컬 스트리밍 읽기 | NuGet 3.9.0 | MIT |
| Multilingual E5 Base GGUF | 파일명·경로·본문·OCR의 768차원 다국어 임베딩 | `dinab/multilingual-e5-base-Q4_K_M-GGUF`, 커밋 `ff190f44542a3ee01e865c936450c41c8b159805`, Q4_K_M | MIT |
| SigLIP 2 Base Patch16 224 ONNX | 이미지·PDF 화면과 다국어 문장의 768차원 공통 임베딩 | `onnx-community/siglip2-base-patch16-224-ONNX`, 커밋 `ba1f3b0843f24bc5417d38e19c37b287d719b2f4`, INT8 | Apache-2.0 원본 모델 계열 |
| Qwen3 1.7B GGUF | 한국어 검색 문장·이전 검색 문맥을 구조화된 `SearchPlan`으로 해석 | `ggml-org/Qwen3-1.7B-GGUF`, 커밋 `daeb8e2`, Q4_K_M | Apache-2.0 |
| Microsoft.ML.Tokenizers | SigLIP 2 SentencePiece 토큰화 | NuGet 2.0.0 | MIT |
| ONNX Runtime DirectML | SigLIP 2 내장그래픽 추론과 CPU 폴백 | NuGet 1.22.0 | MIT |
| llama.cpp | E5 임베딩과 Qwen3 자연어 해석을 위한 로컬 CPU 서버 | 빌드 시 최신 Windows x64 CPU 릴리스 | MIT |
| Windows OCR / PDF API | 이미지·PDF 로컬 문자 인식 | 운영체제 제공 | Windows 구성 요소 |

## 고정 파일과 SHA-256

- `multilingual-e5-base-q4_k_m.gguf`
  - SHA-256: `3c33cbe9ce46b45ab71f47ddc8ae3bc6af0e049aef29de15cefbc494fba1732b`
- `siglip2-base-patch16-224-int8.onnx`
  - SHA-256: `bfe28fe2ccdb685874586648035ea349593e487ce33bd0939b28813681a8f167`
- `siglip2-tokenizer.model`
  - SHA-256: `61a7b147390c64585d6c3543dd6fc636906c9af3865a5548f27f31aee1d4c8e2`
- `Qwen3-1.7B-Q4_K_M.gguf`
  - SHA-256: `d2387ca2dbfee2ffabce7120d3770dadca0b293052bc2f0e138fdc940d9bc7b5`

## 출처

- E5 원본: <https://huggingface.co/intfloat/multilingual-e5-base>
- ExcelDataReader: <https://github.com/ExcelDataReader/ExcelDataReader>
- E5 GGUF: <https://huggingface.co/dinab/multilingual-e5-base-Q4_K_M-GGUF>
- SigLIP 2 원본: <https://huggingface.co/google/siglip2-base-patch16-224>
- SigLIP 2 ONNX: <https://huggingface.co/onnx-community/siglip2-base-patch16-224-ONNX>
- Qwen3 원본: <https://huggingface.co/Qwen/Qwen3-1.7B>
- Qwen3 GGUF: <https://huggingface.co/ggml-org/Qwen3-1.7B-GGUF>
- ONNX Runtime DirectML: <https://onnxruntime.ai/docs/execution-providers/DirectML-ExecutionProvider.html>
- Microsoft.ML.Tokenizers: <https://www.nuget.org/packages/Microsoft.ML.Tokenizers>
- llama.cpp: <https://github.com/ggml-org/llama.cpp>

모델과 실행기는 다운로드 시 SHA-256을 확인합니다. 파일 내용과 검색어는 외부
서비스로 전송하지 않으며, E5와 Qwen3는 `127.0.0.1`의 인증된 로컬 llama.cpp
서버에서, SigLIP 2는 앱 프로세스 내부 ONNX Runtime에서 실행합니다. Qwen3는
파일을 직접 열거나 경로를 생성하지 않고 스키마로 제한된 검색 계획만 반환합니다.
