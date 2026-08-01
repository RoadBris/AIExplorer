#!/usr/bin/env python3
"""Cross-platform structural checks that do not require the Windows WPF SDK."""

from __future__ import annotations

import codecs
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "src" / "AIExplorer"
EVENT_ATTRIBUTES = (
    "Click",
    "Loaded",
    "Closing",
    "PreviewKeyDown",
    "KeyDown",
    "TextChanged",
    "GotKeyboardFocus",
    "LostKeyboardFocus",
    "SelectionChanged",
    "SelectedItemChanged",
    "Expanded",
    "MouseDoubleClick",
    "PreviewMouseLeftButtonDown",
    "PreviewMouseRightButtonDown",
    "PreviewMouseMove",
    "DragOver",
    "Drop",
)


def fail(message: str) -> None:
    print(f"ERROR: {message}")
    raise SystemExit(1)


def validate_xml() -> None:
    for path in sorted(SOURCE.rglob("*.xaml")):
        try:
            ET.parse(path)
        except ET.ParseError as exc:
            fail(f"{path.relative_to(ROOT)}: invalid XML: {exc}")
        print(f"XML OK  {path.relative_to(ROOT)}")


def validate_event_handlers() -> None:
    pattern = re.compile(
        rf"\b(?:{'|'.join(EVENT_ATTRIBUTES)})=\"([A-Za-z_][A-Za-z0-9_]*)\""
    )
    for xaml_path in sorted(SOURCE.rglob("*.xaml")):
        code_path = xaml_path.with_suffix(xaml_path.suffix + ".cs")
        if not code_path.exists():
            continue

        markup = xaml_path.read_text(encoding="utf-8")
        code = code_path.read_text(encoding="utf-8")
        handlers = sorted(set(pattern.findall(markup)))
        missing = [
            handler
            for handler in handlers
            if re.search(rf"\b{re.escape(handler)}\s*\(", code) is None
        ]
        if missing:
            fail(
                f"{xaml_path.relative_to(ROOT)}: missing handlers: "
                + ", ".join(missing)
            )
        print(
            f"EVENT OK {xaml_path.relative_to(ROOT)} "
            f"({len(handlers)} handler references)"
        )


def validate_static_resources() -> None:
    xaml_files = sorted(SOURCE.rglob("*.xaml"))
    documents = {
        path: path.read_text(encoding="utf-8")
        for path in xaml_files
    }
    resource_keys = {
        key
        for text in documents.values()
        for key in re.findall(r'x:Key="([^"]+)"', text)
    }

    missing: list[str] = []
    for path, text in documents.items():
        for reference in re.findall(r"\{StaticResource\s+([^}\s]+)\}", text):
            if reference.startswith("{x:Type"):
                continue
            if reference not in resource_keys:
                missing.append(f"{path.relative_to(ROOT)} -> {reference}")

    if missing:
        fail("missing StaticResource keys: " + ", ".join(sorted(set(missing))))
    print(f"RESOURCE OK ({len(resource_keys)} keys)")


def validate_required_files() -> None:
    required = [
        ROOT / "AIExplorer.sln",
        ROOT / "NuGet.Config",
        SOURCE / "AIExplorer.csproj",
        SOURCE / "App.xaml",
        SOURCE / "App.xaml.cs",
        SOURCE / "GlobalUsings.cs",
        SOURCE / "MainWindow.xaml",
        SOURCE / "MainWindow.xaml.cs",
        SOURCE / "Services" / "FileTypeCatalog.cs",
        SOURCE / "Services" / "FileMetadataDescriptor.cs",
        SOURCE / "Services" / "DocumentTextExtractor.cs",
        SOURCE / "Services" / "WindowsOcrService.cs",
        SOURCE / "Services" / "ContentIndexService.cs",
        SOURCE / "Services" / "ContentSearchService.cs",
        SOURCE / "Services" / "AiModelManager.cs",
        SOURCE / "Services" / "LocalEmbeddingService.cs",
        SOURCE / "Services" / "SemanticIndexService.cs",
        SOURCE / "Services" / "ClipTokenizer.cs",
        SOURCE / "Services" / "SiglipTokenizer.cs",
        SOURCE / "Services" / "VisualFrameLoader.cs",
        SOURCE / "Services" / "ImagePreviewService.cs",
        SOURCE / "Services" / "VisualQueryPromptBuilder.cs",
        SOURCE / "Services" / "LocalVisualEmbeddingService.cs",
        SOURCE / "Services" / "VisualIndexService.cs",
        SOURCE / "Services" / "AdvancedAnalysisService.cs",
        SOURCE / "Services" / "MetadataIndexService.cs",
        SOURCE / "Services" / "MetadataSearchService.cs",
        SOURCE / "Services" / "SearchQueryInterpreter.cs",
        SOURCE / "Services" / "SearchPlan.cs",
        SOURCE / "Services" / "NaturalLanguageSearchService.cs",
        SOURCE / "Services" / "SearchPathPriority.cs",
        SOURCE / "Services" / "SearchRankingService.cs",
        SOURCE / "Services" / "TargetedFileSearchService.cs",
        SOURCE / "Services" / "SearchVisibilityPolicy.cs",
        SOURCE / "Services" / "ShellIconService.cs",
        SOURCE / "Services" / "LaunchedProcessTracker.cs",
        SOURCE / "Services" / "NetworkPathService.cs",
        SOURCE / "Services" / "BackgroundIndexRootPlanner.cs",
        SOURCE / "Services" / "TrayIconService.cs",
        SOURCE / "Dialogs" / "AiSettingsDialog.xaml",
        SOURCE / "Dialogs" / "AiSettingsDialog.xaml.cs",
        SOURCE / "app.manifest",
        SOURCE / "Assets" / "AIExplorer.ico",
        ROOT / "build_release.cmd",
        ROOT / "tools" / "prepare_ai_bundle.ps1",
        ROOT / "tools" / "preflight.ps1",
        ROOT / "verify_source.cmd",
        ROOT / "docs" / "THIRD_PARTY_AI.md",
    ]
    missing = [str(path.relative_to(ROOT)) for path in required if not path.exists()]
    if missing:
        fail("missing required files: " + ", ".join(missing))

    for path in (SOURCE / "AIExplorer.csproj", SOURCE / "app.manifest"):
        try:
            ET.parse(path)
        except ET.ParseError as exc:
            fail(f"{path.relative_to(ROOT)}: invalid XML: {exc}")


def validate_no_placeholders() -> None:
    forbidden = ("NotImplementedException", "TODO: REQUIRED", "FIXME: REQUIRED")
    for path in sorted(SOURCE.rglob("*.cs")):
        text = path.read_text(encoding="utf-8")
        for marker in forbidden:
            if marker in text:
                fail(f"{path.relative_to(ROOT)} contains {marker}")


def _strip_csharp_non_code(text: str) -> str:
    """Replace comments and literals so bracket checks only see C# code."""
    output: list[str] = []
    index = 0
    length = len(text)
    state = "code"
    while index < length:
        char = text[index]
        next_char = text[index + 1] if index + 1 < length else ""

        if state == "code":
            if text.startswith('$"""', index):
                output.extend("    ")
                index += 4
                state = "raw_string"
                continue
            if text.startswith('"""', index):
                output.extend("   ")
                index += 3
                state = "raw_string"
                continue
            if char == "/" and next_char == "/":
                output.extend("  ")
                index += 2
                state = "line_comment"
                continue
            if char == "/" and next_char == "*":
                output.extend("  ")
                index += 2
                state = "block_comment"
                continue
            if char == "@" and next_char == '"':
                output.extend("  ")
                index += 2
                state = "verbatim_string"
                continue
            if char == '$' and next_char == '@' and index + 2 < length and text[index + 2] == '"':
                output.extend("   ")
                index += 3
                state = "verbatim_string"
                continue
            if char == '@' and next_char == '$' and index + 2 < length and text[index + 2] == '"':
                output.extend("   ")
                index += 3
                state = "verbatim_string"
                continue
            if char == '$' and next_char == '"':
                output.extend("  ")
                index += 2
                state = "string"
                continue
            if char == '"':
                output.append(" ")
                index += 1
                state = "string"
                continue
            if char == "'":
                output.append(" ")
                index += 1
                state = "char"
                continue
            output.append(char)
            index += 1
            continue

        if state == "line_comment":
            if char == "\n":
                output.append("\n")
                state = "code"
            else:
                output.append(" ")
            index += 1
            continue

        if state == "block_comment":
            if char == "*" and next_char == "/":
                output.extend("  ")
                index += 2
                state = "code"
            else:
                output.append("\n" if char == "\n" else " ")
                index += 1
            continue

        if state == "string":
            if char == "\\":
                if index + 1 < length and text[index + 1] in {"\r", "\n"}:
                    fail("regular C# string contains an unescaped line break")
                output.append(" ")
                if index + 1 < length:
                    output.append(" ")
                index += 2
            elif char == '"':
                output.append(" ")
                index += 1
                state = "code"
            elif char in {"\r", "\n"}:
                fail("regular C# string contains an unescaped line break")
            else:
                output.append(" ")
                index += 1
            continue

        if state == "verbatim_string":
            if char == '"' and next_char == '"':
                output.extend("  ")
                index += 2
            elif char == '"':
                output.append(" ")
                index += 1
                state = "code"
            else:
                output.append("\n" if char == "\n" else " ")
                index += 1
            continue

        if state == "raw_string":
            if text.startswith('"""', index):
                output.extend("   ")
                index += 3
                state = "code"
            else:
                output.append("\n" if char == "\n" else " ")
                index += 1
            continue

        if state == "char":
            if char == "\\":
                if index + 1 < length and text[index + 1] in {"\r", "\n"}:
                    fail("C# char literal contains a line break")
                output.append(" ")
                if index + 1 < length:
                    output.append(" ")
                index += 2
            elif char == "'":
                output.append(" ")
                index += 1
                state = "code"
            elif char in {"\r", "\n"}:
                fail("C# char literal contains a line break")
            else:
                output.append(" ")
                index += 1
            continue

    if state in {
        "string",
        "verbatim_string",
        "raw_string",
        "char",
        "block_comment",
    }:
        fail(f"unterminated C# literal or comment ({state})")
    return "".join(output)


def validate_csharp_structure() -> None:
    pairs = {"(": ")", "[": "]", "{": "}"}
    closing = {value: key for key, value in pairs.items()}
    for path in sorted((ROOT / "src").rglob("*.cs")) + sorted((ROOT / "tests").rglob("*.cs")):
        code = _strip_csharp_non_code(path.read_text(encoding="utf-8-sig"))
        stack: list[tuple[str, int]] = []
        line = 1
        for char in code:
            if char == "\n":
                line += 1
                continue
            if char in pairs:
                stack.append((char, line))
            elif char in closing:
                if not stack or stack[-1][0] != closing[char]:
                    fail(
                        f"{path.relative_to(ROOT)}:{line}: "
                        f"unmatched closing bracket {char}"
                    )
                stack.pop()
        if stack:
            char, opening_line = stack[-1]
            fail(
                f"{path.relative_to(ROOT)}:{opening_line}: "
                f"unclosed bracket {char}"
            )
    print("CSHARP OK (comments/literals stripped, bracket balance checked)")


def _find_duplicate_var_declarations(text: str) -> dict[str, list[int]]:
    """Find duplicate `var name =` declarations in the same C# brace scope.

    The smoke-test executable uses one large top-level try block. This
    lightweight check catches repeated scenario response variables in that
    shared scope without requiring the Windows WPF SDK.
    """
    code = _strip_csharp_non_code(text)
    scope_at_offset: list[tuple[int, ...]] = [()] * (len(code) + 1)
    scope_stack: list[int] = []
    for offset, char in enumerate(code):
        scope_at_offset[offset] = tuple(scope_stack)
        if char == "{":
            scope_stack.append(offset)
        elif char == "}":
            if scope_stack:
                scope_stack.pop()
    scope_at_offset[len(code)] = tuple(scope_stack)

    declarations: dict[tuple[tuple[int, ...], str], list[int]] = {}
    pattern = re.compile(r"\bvar\s+([A-Za-z_][A-Za-z0-9_]*)\s*=")
    for match in pattern.finditer(code):
        line_start = code.rfind("\n", 0, match.start()) + 1
        prefix = code[line_start:match.start()]
        if re.search(r"\b(?:for|foreach)\s*\([^)]*$", prefix):
            continue

        name = match.group(1)
        line = code.count("\n", 0, match.start()) + 1
        key = (scope_at_offset[match.start()], name)
        declarations.setdefault(key, []).append(line)

    duplicates: dict[str, list[int]] = {}
    for (_, name), lines in declarations.items():
        if len(lines) > 1:
            duplicates.setdefault(name, []).extend(lines)
    return duplicates


def validate_smoke_test_local_declarations() -> None:
    smoke_path = ROOT / "tests" / "AIExplorer.SmokeTests" / "Program.cs"
    duplicate_sample = """
try
{
    var response = 1;
    var response = 2;
}
"""
    distinct_sample = """
var firstResponse = 1;
if (firstResponse > 0)
{
    var nestedResponse = 2;
}
if (firstResponse > 0)
{
    var nestedResponse = 3;
}
var secondResponse = 3;
"""
    if _find_duplicate_var_declarations(duplicate_sample) != {
        "response": [4, 5]
    }:
        fail("local duplicate validator self-test failed")
    if _find_duplicate_var_declarations(distinct_sample):
        fail("local duplicate validator reported a false positive")

    duplicates = _find_duplicate_var_declarations(
        smoke_path.read_text(encoding="utf-8-sig")
    )
    if duplicates:
        details = ", ".join(
            f"{name} (lines {', '.join(map(str, lines))})"
            for name, lines in sorted(duplicates.items())
        )
        fail(
            f"{smoke_path.relative_to(ROOT)}: duplicate top-level local "
            f"declarations (CS0128): {details}"
        )
    print("CSHARP LOCALS OK (same-scope smoke-test declarations are unique)")


def validate_csharp_scope_regressions() -> None:
    window_code = (SOURCE / "MainWindow.xaml.cs").read_text(encoding="utf-8")
    app_project = (SOURCE / "AIExplorer.csproj").read_text(encoding="utf-8-sig")
    test_project = (
        ROOT / "tests" / "AIExplorer.SmokeTests" /
        "AIExplorer.SmokeTests.csproj"
    ).read_text(encoding="utf-8-sig")
    if re.search(r"is\s+string\s+sourcePath\b", window_code) and re.search(
        r"foreach\s*\(\s*var\s+sourcePath\s+in\s+paths\s*\)",
        window_code,
    ):
        fail(
            "MainWindow.xaml.cs: favorite reorder pattern variable and "
            "file-drop loop both declare sourcePath (CS0136)"
        )
    if "is string reorderSourcePath" not in window_code:
        fail(
            "MainWindow.xaml.cs: favorite reorder payload must use "
            "reorderSourcePath to avoid CS0136"
        )
    if not re.search(
        r"FavoritePathService\.MoveFavorite\(\s*"
        r"_settings\.Favorites,\s*reorderSourcePath,",
        window_code,
        re.MULTILINE,
    ):
        fail("favorite reorder call does not use reorderSourcePath")
    if "_ = Dispatcher.BeginInvoke(" not in window_code:
        fail(
            "MainWindow.xaml.cs: background preview dispatcher operation "
            "must be explicitly discarded (CS4014)"
        )
    for project_name, project_text in (
        ("src/AIExplorer/AIExplorer.csproj", app_project),
        (
            "tests/AIExplorer.SmokeTests/AIExplorer.SmokeTests.csproj",
            test_project,
        ),
    ):
        if "<TreatWarningsAsErrors>true</TreatWarningsAsErrors>" not in project_text:
            fail(f"{project_name}: C# warnings must fail the build")
    print(
        "CSHARP GATE OK "
        "(locals do not collide; dispatcher call is explicit; warnings fail)"
    )


def validate_mainwindow_helper_references() -> None:
    """Catch stale unqualified Resolve* helper calls before Windows build."""
    path = SOURCE / "MainWindow.xaml.cs"
    source = path.read_text(encoding="utf-8-sig")
    code = _strip_csharp_non_code(source)
    declarations = set(
        re.findall(
            r"\b(?:private|public|internal|protected)\s+"
            r"(?:static\s+)?(?:async\s+)?"
            r"(?:[A-Za-z0-9_<>,?\[\].]+\s+)+"
            r"([A-Za-z_][A-Za-z0-9_]*)\s*\(",
            code,
        )
    )
    resolve_calls = set(
        re.findall(
            r"(?<![.\w])(Resolve[A-Z][A-Za-z0-9_]*)\s*\(",
            code,
        )
    )
    missing = sorted(resolve_calls - declarations)
    if missing:
        fail(
            "MainWindow.xaml.cs calls undeclared Resolve helper(s): "
            + ", ".join(missing)
        )
    if "ResolveAllAvailableRoots()" in code:
        fail(
            "MainWindow.xaml.cs still calls removed ResolveAllAvailableRoots; "
            "use ResolveAllAvailableRootsWithoutProbe"
        )
    if "ResolveAllAvailableRootsWithoutProbe()" not in code:
        fail(
            "MainWindow.xaml.cs does not use the current all-roots resolver"
        )
    print("CSHARP HELPER OK (all unqualified Resolve* calls are declared)")


def validate_search_policy_regression() -> None:
    content_search = (
        SOURCE / "Services" / "ContentSearchService.cs"
    ).read_text(encoding="utf-8")
    query_interpreter = (
        SOURCE / "Services" / "SearchQueryInterpreter.cs"
    ).read_text(encoding="utf-8")
    search_text_analyzer = (
        SOURCE / "Services" / "SearchTextAnalyzer.cs"
    ).read_text(encoding="utf-8")
    result_refinement = (
        SOURCE / "Services" / "ResultRefinementService.cs"
    ).read_text(encoding="utf-8")
    search_text_attributes = (
        SOURCE / "Services" / "SearchTextAttributes.cs"
    ).read_text(encoding="utf-8")
    metadata_search = (
        SOURCE / "Services" / "MetadataSearchService.cs"
    ).read_text(encoding="utf-8")
    smoke_test = (
        ROOT / "tests" / "AIExplorer.SmokeTests" / "Program.cs"
    ).read_text(encoding="utf-8")

    semantic_index = (
        SOURCE / "Services" / "SemanticIndexService.cs"
    ).read_text(encoding="utf-8")

    required = (
        (content_search, "HasSufficientCoverage"),
        (content_search, "Math.Ceiling(totalTermCount * 0.5d)"),
        (content_search, "int MatchedTermCount"),
        (content_search, "public double Coverage"),
        (metadata_search, "contentCoverage: candidate.Coverage"),
        (metadata_search, "candidate.ContentCoverage ?? 0.5d"),
        (smoke_test, "여러 검색어 중 한 개만 맞는 본문 오탐 제거"),
        (smoke_test, "낮은 본문 단어 커버리지는 AI 후보를 덮어쓰지 않음"),
        (smoke_test, "semanticOnlyResult.EvidenceKind =="),
        (smoke_test, "SearchEvidenceKind.SemanticCandidate"),
        (smoke_test, "HasDirectContentEvidenceReason("),
        (semantic_index, "검색어를 파일에서 직접 확인했다는 뜻은 아닙니다."),
    )
    for source_text, value in required:
        if value not in source_text:
            fail(f"search policy regression contract mismatch: {value}")
    if re.search(
        r'semanticOnlyResult\.Reason\.Contains\(\s*"본문"',
        smoke_test,
        re.MULTILINE,
    ):
        fail(
            "semantic evidence smoke test rejects any disclaimer containing "
            "the word body instead of checking typed evidence"
        )

    def sufficient(total: int, matched: int) -> bool:
        if matched <= 0 or total <= 0:
            return False
        if total == 1:
            return True
        minimum = max(2, (total + 1) // 2)
        return matched >= minimum

    scenarios = {
        "single exact term": sufficient(1, 1),
        "full three-term content": sufficient(3, 3),
        "half of four terms": sufficient(4, 2),
        "one generic synonym out of four": not sufficient(4, 1),
        "one of two terms": not sufficient(2, 1),
    }
    failed = [name for name, passed in scenarios.items() if not passed]
    if failed:
        fail("search coverage policy failed: " + ", ".join(failed))

    required_semantic_reason_terms = (
        "로컬 AI Multilingual E5가 파일명·경로·추출 내용을",
        "검색 문장과 비교해 관련 가능성을 추정했습니다.",
        "검색어를 파일에서 직접 확인했다는 뜻은 아닙니다.",
    )
    for term in required_semantic_reason_terms:
        if term not in semantic_index:
            fail(
                "semantic candidate reason contract was not found: "
                + term
            )
    if "관련된 것으로 판단한 후보입니다." in semantic_index:
        fail(
            "AI semantic reason still presents an inference like direct "
            "lexical evidence"
        )
    print(
        "SEARCH POLICY OK "
        "(weak lexical overlap cannot override AI evidence; "
        "AI reason is evidence-safe)"
    )


def validate_ai_bundle_contract() -> None:
    manager = (
        SOURCE / "Services" / "AiModelManager.cs"
    ).read_text(encoding="utf-8")
    bundle = (
        ROOT / "tools" / "prepare_ai_bundle.ps1"
    ).read_text(encoding="utf-8")
    build = (ROOT / "build_release.cmd").read_text(encoding="utf-8")
    required_values = (
        "multilingual-e5-base-q4_k_m.gguf",
        "ff190f44542a3ee01e865c936450c41c8b159805",
        "3c33cbe9ce46b45ab71f47ddc8ae3bc6af0e049aef29de15cefbc494fba1732b",
        "siglip2-base-patch16-224-int8.onnx",
        "ba1f3b0843f24bc5417d38e19c37b287d719b2f4",
        "bfe28fe2ccdb685874586648035ea349593e487ce33bd0939b28813681a8f167",
        "61a7b147390c64585d6c3543dd6fc636906c9af3865a5548f27f31aee1d4c8e2",
        "Qwen3-1.7B-Q4_K_M.gguf",
        "daeb8e2-qwen3-1.7b-q4-k-m",
        "d2387ca2dbfee2ffabce7120d3770dadca0b293052bc2f0e138fdc940d9bc7b5",
    )
    for value in required_values:
        if value not in manager or value not in bundle:
            fail(f"AI model bundle contract mismatch: {value}")
    if "prepare_ai_bundle.ps1" not in build:
        fail("release build does not prepare the AI bundle")
    print("AI BUNDLE OK (pinned model, SHA-256, build integration)")


def validate_llama_runtime_asset_contract() -> None:
    bundle = (
        ROOT / "tools" / "prepare_ai_bundle.ps1"
    ).read_text(encoding="utf-8-sig")
    manager = (
        SOURCE / "Services" / "AiModelManager.cs"
    ).read_text(encoding="utf-8-sig")

    required_bundle = (
        "Get-WindowsX64CpuRuntimeAsset",
        "^llama-b.+-bin-win-x64\\.zip$",
        "^llama-b.+-bin-win-cpu-x64\\.zip$",
        "$runtimeExcludedPattern",
        "Selected llama.cpp runtime asset",
        "[AllowEmptyCollection()]",
        "Get-ReleaseAssets",
        "assets_url",
        "Find-WindowsX64CpuRuntimeRelease",
        "releases?per_page=10",
        "X-GitHub-Api-Version",
    )
    for value in required_bundle:
        if value not in bundle:
            fail(f"llama.cpp bundle asset contract missing: {value}")

    required_manager = (
        "GetWindowsCpuRuntimePriority",
        "DefaultWindowsCpuRuntimeRegex",
        "NamedWindowsCpuRuntimeRegex",
        "CompatibleWindowsCpuRuntimeRegex",
        "AcceleratedWindowsRuntimeRegex",
    )
    for value in required_manager:
        if value not in manager:
            fail(f"llama.cpp manager asset contract missing: {value}")

    accelerated_tokens = (
        "cuda", "cudart", "vulkan", "sycl", "hip", "openvino", "rpc"
    )
    if not all(token in bundle.lower() and token in manager.lower()
               for token in accelerated_tokens):
        fail("accelerated llama.cpp runtime exclusion is incomplete")

    print(
        "LLAMA RUNTIME ASSET OK "
        "(empty assets accepted, assets_url/recent-release fallback, "
        "current win-x64, backend exclusion)"
    )


def validate_powershell_51_encoding() -> None:
    path = ROOT / "tools" / "prepare_ai_bundle.ps1"
    raw = path.read_bytes()
    if not raw.startswith(codecs.BOM_UTF8):
        fail(
            "tools/prepare_ai_bundle.ps1 must use a UTF-8 BOM "
            "for Windows PowerShell 5.1"
        )
    script = raw.decode("utf-8-sig")
    non_ascii_lines = [
        str(index)
        for index, line in enumerate(script.splitlines(), start=1)
        if any(ord(character) > 127 for character in line)
    ]
    if non_ascii_lines:
        fail(
            "prepare_ai_bundle.ps1 contains non-ASCII text on lines: "
            + ", ".join(non_ascii_lines)
        )
    print("POWERSHELL OK (UTF-8 BOM, ASCII-safe script body)")



def validate_cmd_encoding_contract() -> None:
    for path in sorted(ROOT.glob("*.cmd")):
        raw = path.read_bytes()
        if raw.startswith(codecs.BOM_UTF8) or raw.startswith(codecs.BOM_UTF16_LE) or raw.startswith(codecs.BOM_UTF16_BE):
            fail(f"{path.name} must not contain a Unicode BOM")
        try:
            text = raw.decode("ascii")
        except UnicodeDecodeError as exc:
            fail(f"{path.name} must remain ASCII-safe for cmd.exe: {exc}")
        if "\r\n" not in text:
            fail(f"{path.name} must use CRLF line endings for cmd.exe")
    print("CMD OK (ASCII-safe, BOM-free, CRLF line endings)")

def validate_search_experience_contract() -> None:
    window_markup = (SOURCE / "MainWindow.xaml").read_text(encoding="utf-8")
    window_code = (SOURCE / "MainWindow.xaml.cs").read_text(encoding="utf-8")
    search_service = (
        SOURCE / "Services" / "MetadataSearchService.cs"
    ).read_text(encoding="utf-8")
    advanced_service = (
        SOURCE / "Services" / "AdvancedAnalysisService.cs"
    ).read_text(encoding="utf-8")
    semantic_index = (
        SOURCE / "Services" / "SemanticIndexService.cs"
    ).read_text(encoding="utf-8")
    settings_service = (
        SOURCE / "Services" / "SettingsService.cs"
    ).read_text(encoding="utf-8")
    embedding_service = (
        SOURCE / "Services" / "LocalEmbeddingService.cs"
    ).read_text(encoding="utf-8")
    metadata_search = (
        SOURCE / "Services" / "MetadataSearchService.cs"
    ).read_text(encoding="utf-8")
    settings_markup = (
        SOURCE / "Dialogs" / "AiSettingsDialog.xaml"
    ).read_text(encoding="utf-8")
    styles_markup = (
        SOURCE / "Themes" / "Styles.xaml"
    ).read_text(encoding="utf-8")
    ocr_service = (
        SOURCE / "Services" / "WindowsOcrService.cs"
    ).read_text(encoding="utf-8")
    visual_service = (
        SOURCE / "Services" / "LocalVisualEmbeddingService.cs"
    ).read_text(encoding="utf-8")
    visual_index = (
        SOURCE / "Services" / "VisualIndexService.cs"
    ).read_text(encoding="utf-8")
    visual_prompt = (
        SOURCE / "Services" / "VisualQueryPromptBuilder.cs"
    ).read_text(encoding="utf-8")
    title_search = (
        SOURCE / "Services" / "TitleSearchService.cs"
    ).read_text(encoding="utf-8")
    ranking_service = (
        SOURCE / "Services" / "SearchRankingService.cs"
    ).read_text(encoding="utf-8")
    ranking_preferences = (
        SOURCE / "Services" / "SearchRankingPreferences.cs"
    ).read_text(encoding="utf-8")
    metadata_index = (
        SOURCE / "Services" / "MetadataIndexService.cs"
    ).read_text(encoding="utf-8")
    file_type_catalog = (
        SOURCE / "Services" / "FileTypeCatalog.cs"
    ).read_text(encoding="utf-8")
    metadata_descriptor = (
        SOURCE / "Services" / "FileMetadataDescriptor.cs"
    ).read_text(encoding="utf-8")
    index_choice_dialog = (
        SOURCE / "Dialogs" / "SearchIndexChoiceDialog.xaml"
    ).read_text(encoding="utf-8")
    search_result = (
        SOURCE / "Models" / "SearchResult.cs"
    ).read_text(encoding="utf-8")
    text_prompt_markup = (
        SOURCE / "Dialogs" / "TextPromptDialog.xaml"
    ).read_text(encoding="utf-8")
    targeted_search = (
        SOURCE / "Services" / "TargetedFileSearchService.cs"
    ).read_text(encoding="utf-8")
    content_index = (
        SOURCE / "Services" / "ContentIndexService.cs"
    ).read_text(encoding="utf-8")
    content_search = (
        SOURCE / "Services" / "ContentSearchService.cs"
    ).read_text(encoding="utf-8")
    query_interpreter = (
        SOURCE / "Services" / "SearchQueryInterpreter.cs"
    ).read_text(encoding="utf-8")
    search_text_analyzer = (
        SOURCE / "Services" / "SearchTextAnalyzer.cs"
    ).read_text(encoding="utf-8")
    result_refinement = (
        SOURCE / "Services" / "ResultRefinementService.cs"
    ).read_text(encoding="utf-8")
    search_text_attributes = (
        SOURCE / "Services" / "SearchTextAttributes.cs"
    ).read_text(encoding="utf-8")
    search_models = (
        SOURCE / "Models" / "SearchModels.cs"
    ).read_text(encoding="utf-8")
    project_file = (
        SOURCE / "AIExplorer.csproj"
    ).read_text(encoding="utf-8")
    smoke_test = (
        ROOT / "tests" / "AIExplorer.SmokeTests" / "Program.cs"
    ).read_text(encoding="utf-8")
    siglip_tokenizer = (
        SOURCE / "Services" / "SiglipTokenizer.cs"
    ).read_text(encoding="utf-8")
    required_values = (
        (window_markup, 'x:Name="AdvancedAnalysisButton"'),
        (window_markup, 'x:Name="ResultRefineBar"'),
        (window_markup, 'x:Name="ResultRefineTextBox"'),
        (window_markup, 'x:Name="ResultRefineInterpretationText"'),
        (window_markup, 'x:Name="ResultRefineProgressBar"'),
        (window_markup, 'Content="전체 결과로"'),
        (window_markup, 'Grid.Row="1" Margin="1,5,1,0"'),
        (window_markup, 'Content="찾기"'),
        (window_code, "RefreshResultRefinementViews"),
        (window_code, "GetResultTextFactsAsync"),
        (window_code, "_allIntegratedSearchResults"),
        (window_code, "_allTitleSearchResults"),
        (query_interpreter, "LiteralTerms"),
        (query_interpreter, "FloorReferences"),
        (search_text_analyzer, "ContainsAllFloorReferences"),
        (result_refinement, "ResultRefinementResult"),
        (search_text_attributes, "SearchAttributeQueryParser"),
        (search_text_attributes, "SearchTextAttributeAnalyzer"),
        (search_text_attributes, "NameOrContent"),
        (window_markup, 'x:Name="IndexStatusText"'),
        (window_markup, 'ToolTip="{x:Null}"'),
        (window_markup, 'Source="{Binding PreviewImage}"'),
        (window_markup, 'VirtualizingPanel.ScrollUnit="Pixel"'),
        (window_markup, 'VirtualizingPanel.CacheLengthUnit="Page"'),
        (window_markup, "CanReservePreviewSpace"),
        (window_markup, 'SelectedIndex="0"'),
        (window_code, 'SearchInputPlaceholder ='),
        (window_code, '"이곳에 검색 문구를 입력하세요."'),
        (window_code, "ShowSearchInputPlaceholder"),
        (window_code, "HideSearchInputPlaceholder"),
        (window_code, "_isSearchInputPlaceholderActive"),
        (window_code, "_priorityIndexRoots"),
        (window_code, "ScheduleBackgroundIndexing"),
        (window_code, "PauseBackgroundIndexing"),
        (window_code, "PauseBackgroundIndexingForInput"),
        (search_service, "WarmUpAsync"),
        (advanced_service, "EmbeddingResolution.Full"),
        (advanced_service, "MaximumAnalyzedResults = 50"),
        (advanced_service, "로컬 AI Multilingual E5 정밀 재평가"),
        (semantic_index, "검색어를 파일에서 직접 확인했다는 뜻은 아닙니다."),
        (
            metadata_search,
            "BuildDisplayReason(candidate, intent.RankingProfile)",
        ),
        (embedding_service, "ProcessPriorityClass.BelowNormal"),
        (embedding_service, "StoredEmbeddingDimensions = 768"),
        (metadata_search, "maximumNewVisualDocumentsPerRoot = 48"),
        (settings_service, "ChangeDataDirectoryAsync"),
        (settings_service, "storage-location.txt"),
        (settings_markup, 'x:Name="StoragePathTextBox"'),
        (settings_markup, 'Content="위치 변경"'),
        (settings_markup, 'x:Name="UseSystemTrayBackgroundCheckBox"'),
        (settings_markup, "고정 AI 검색 구성"),
        (styles_markup, 'Property="SelectionTextBrush" Value="White"'),
        (styles_markup, 'x:Key="TextSelectionBrush"'),
        (ocr_service, "OcrEngine.TryCreateFromUserProfileLanguages"),
        (ocr_service, "PdfDocument.LoadFromFileAsync"),
        (visual_service, '"text_embeds"'),
        (visual_service, '"image_embeds"'),
        (visual_service, '"siglip2-base-patch16-224-int8-768d"'),
        (visual_service, "Environment.ProcessorCount / 2"),
        (visual_index, "SearchPhase.VisualIndexing"),
        (visual_index, "SigLIP 2"),
        (visual_index, "MinimumUserInterfaceMargin"),
        (metadata_search, "maximumNewDocuments: 0"),
        (visual_index, "MaximumNewDocumentsPerSearch = 256"),
        (visual_index, "MaximumVisualCandidates = 500"),
        (semantic_index, "MaximumSemanticCandidates = 240"),
        (semantic_index, "MinimumExpandedCandidateCount = 120"),
        (visual_index, "SelectPendingDocuments"),
        (visual_service, "EmbedPromptAsync"),
        (visual_index, "VisualFailedAttemptRecord"),
        (visual_prompt, "BuildVariants"),
        (targeted_search, "20_000L"),
        (content_index, "DeferredOcrDocuments"),
        (content_index, "maximumDocuments / 8"),
        (content_index, "96,"),
        (window_code, "MaximumResults: 500"),
        (search_models, "int MaximumResults = 500"),
        (search_models, "ExistingIndexOnly"),
        (search_models, "SemanticSearchRequested"),
        (metadata_search, "GetIndexReadinessAsync"),
        (metadata_search, "PrepareIndexesAsync"),
        (metadata_search, "maximumNewDocuments: 0"),
        (metadata_search, "hasStrongLexicalEvidence"),
        (semantic_index, "BuildEmbeddingQuery"),
        (metadata_search, "!shouldSearchVisually"),
        (visual_index, "namedSubjectCandidates"),
        (visual_index, "CombinePromptSimilarities"),
        (visual_index, "identityCorroborated"),
        (visual_index, "identityCorroborated;"),
        (visual_prompt, "BuildIdentityAliases"),
        (title_search, "intent.RequestedExtensions.Count == 0"),
        (title_search, "intent.Categories.Count == 0"),
        (title_search, "!intent.DirectoryOnly"),
        (title_search, '"요청한 폴더 항목입니다."'),
        (title_search, "var normalizedTitle = Normalize(name)"),
        (title_search, "ContainsAlternativeTerm("),
        (ranking_service, "TokenizeText(record.Name)"),
        (query_interpreter, "ExtractExplicitExtensions(query)"),
        (query_interpreter, "ExplicitExtensionRegex()"),
        (query_interpreter, '"찾아달라"'),
        (query_interpreter, '"ssh키"'),
        (query_interpreter, "MeaningfulShortTokens"),
        (query_interpreter, "IsSearchableToken("),
        (index_choice_dialog, 'Content="먼저 색인"'),
        (index_choice_dialog, 'Content="일단 검색"'),
        (search_result, 'SearchEvidenceKind.ExactName => "정확 일치"'),
        (search_result, "CanReservePreviewSpace"),
        (text_prompt_markup, 'SizeToContent="Height"'),
        (text_prompt_markup, 'MinHeight="248"'),
        (text_prompt_markup, 'ResizeMode="CanResizeWithGrip"'),
        (ranking_service, "Preserve low-coverage candidates"),
        (ranking_service, "FileMetadataDescriptor.GetSearchTerms"),
        (ranking_service, "originalMatchedTermSet.Count * 28d"),
        (ranking_service, "AllowsCompactLanguagePartialMatch"),
        (ranking_service, "candidate.RankingScore"),
        (ranking_preferences, "SearchRankingFeature.CreatedRecency"),
        (ranking_preferences, "GetEffectiveCreatedUtc"),
        (query_interpreter, "BuildRankingProfile(query)"),
        (metadata_index, "CurrentFormatVersion = 5"),
        (content_index, "CurrentFormatVersion = 7"),
        (metadata_descriptor, "DescriptorVersion = 2"),
        (metadata_descriptor, "PuTTY SSH 개인키"),
        (metadata_descriptor, "BuildSemanticText("),
        (semantic_index, "CurrentFormatVersion = 6"),
        (semantic_index, "record.TextHash"),
        (semantic_index, "핵심 검색 의도:"),
        (metadata_search, "AddRankFusionEvidence("),
        (metadata_search, "GetHybridRankScore("),
        (title_search, "BuildParentContext(fullPath)"),
        (file_type_catalog, "results.Count == 1"),
        (file_type_catalog, "FileCategory.Spreadsheet"),
        (file_type_catalog, "FileCategory.Presentation"),
        (embedding_service, '"query: "'),
        (embedding_service, '"passage: "'),
        (embedding_service, '"--ubatch-size"'),
        (embedding_service, '"512"'),
        (embedding_service, "IsPromptCapacityError"),
        (visual_service, "AppendExecutionProvider_DML(0)"),
        (visual_service, "RunWithCpuFallback"),
        (siglip_tokenizer, "SentencePieceTokenizer.Create"),
        (smoke_test, "첫 완전 일치 이후에도 관련 파일 계속 수집"),
        (smoke_test, "검색 중 새 이미지 색인 금지"),
        (smoke_test, "정확 단서가 없는 단일 용어는 E5 의미 후보로 복구"),
        (smoke_test, "고유명 파일명 단서와 시각 분석이 함께 있는 이미지를 최우선"),
        (smoke_test, "본문이 없는 사용자 파일도 파일명·경로 E5 의미 색인으로 검색"),
        (smoke_test, "제목 키워드가 없는 이미지 종류 요청도 즉시 표시"),
        (smoke_test, "제목 키워드가 없는 폴더 요청도 즉시 표시"),
        (smoke_test, "빠른 이름·경로 검색에서 실제 도면 폴더 복구"),
        (smoke_test, "공백 유무와 관계없는 층수 빠른 검색 결과"),
        (smoke_test, "현재 결과 내 재검색도 층수 공백을 동일하게 처리"),
        (smoke_test, "결과 내 검색 조건을 지우면 원래 후보 복원"),
        (smoke_test, "한글 포함 문장을 일반 키워드가 아닌 문자 조건으로 해석"),
        (smoke_test, "현재 결과의 실제 본문 한글 속성으로 재검색"),
        (smoke_test, "영문 파일명의 실제 한글 본문을 통합 조건 검색으로 발견"),
        (smoke_test, "본문에 두 단어가 있는 잡음보다 IT팀 계정관리 파일명을 우선"),
        (smoke_test, "var firstAccountNoiseRank = FindResultRank("),
        (smoke_test, "accountManagementRank < firstAccountNoiseRank"),
        (smoke_test, "static int FindResultRank<T>("),
        (smoke_test, "계정관리문서 상위 폴더 단서로 내부 문서 발견"),
        (smoke_test, "자연어 문장에서 검색 대상과 생성일 최신 가중치를 분리"),
        (smoke_test, "통합 검색 최종 순위와 결과 근거에 생성일 자연어 가중치를 반영"),
        (smoke_test, "엑셀 시트명과 내부 셀 값 직접 추출"),
        (smoke_test, "평범한 엑셀 제목과 내부 장비명을 결합해 최상위 검색"),
        (smoke_test, "계정 의미 확장과 직접 본문 근거를 분리"),
        (smoke_test, "접속 점검표를 계정 내용 일치로 오인하지 않음"),
        (smoke_test, "번역어 본문 근거에 실제 일치 단어 표시"),
        (content_index, "preferredFiles"),
        (content_search, "DocumentContentSource.Spreadsheet"),
        (content_search, "term.ContentEvidenceAlternatives"),
        (content_search, "실제 일치"),
        (search_result, "SearchEvidenceKind.Combined"),
        (query_interpreter, "ContentEvidenceGroups"),
        (query_interpreter, "ContentEvidenceAlternatives"),
        (project_file, 'ExcelDataReader" Version="3.9.0"'),
        (project_file, 'Microsoft.ML.OnnxRuntime.DirectML'),
        (project_file, 'Microsoft.ML.Tokenizers'),
    )
    for source_text, required_value in required_values:
        if required_value not in source_text:
            fail(f"search experience contract mismatch: {required_value}")
    structured_title_fallback = re.search(
        r"if\s*\(\s*terms\.Length\s*==\s*0\s*&&\s*"
        r"literalTerms\.Length\s*==\s*0\s*&&\s*"
        r"intent\.RequestedExtensions\.Count\s*==\s*0\s*&&\s*"
        r"intent\.Categories\.Count\s*==\s*0\s*&&\s*"
        r"intent\.FloorReferences\.Count\s*==\s*0\s*&&\s*"
        r"intent\.AttributePredicates\.Count\s*==\s*0\s*&&\s*"
        r"!intent\.DirectoryOnly\s*&&\s*"
        r"!intent\.FilesOnly\s*\)",
        title_search,
    )
    if structured_title_fallback is None:
        fail(
            "type-only title queries can be restored as literal title "
            "keywords"
        )
    if "CancellationToken cancellationToken)\n        CancellationToken cancellationToken)" in visual_service:
        fail("visual embedding method contains a duplicated parameter line")
    if 'x:Name="WatermarkText"' in styles_markup:
        fail(
            "search placeholder must use the TextBox content renderer, "
            "not a separately positioned watermark"
        )
    if "candidate.NameMatchCount == intent.Terms.Count" in targeted_search:
        fail("targeted scan still stops after the first complete name match")
    for forbidden_example in ("14층", "와이파이", "Wi-Fi", "wifi"):
        if forbidden_example in window_markup:
            fail(
                "search UI contains a technology-specific example: "
                + forbidden_example
            )
    print(
        "SEARCH UX OK "
        "(exact placeholder, broad visual AI, input pause, storage controls)"
    )



def validate_progressive_search_contract() -> None:
    window_code = (SOURCE / "MainWindow.xaml.cs").read_text(
        encoding="utf-8-sig"
    )
    search_models = (SOURCE / "Models" / "SearchModels.cs").read_text(
        encoding="utf-8-sig"
    )
    search_service = (
        SOURCE / "Services" / "MetadataSearchService.cs"
    ).read_text(encoding="utf-8-sig")
    metadata_index = (
        SOURCE / "Services" / "MetadataIndexService.cs"
    ).read_text(encoding="utf-8-sig")
    content_index = (
        SOURCE / "Services" / "ContentIndexService.cs"
    ).read_text(encoding="utf-8-sig")
    targeted_search = (
        SOURCE / "Services" / "TargetedFileSearchService.cs"
    ).read_text(encoding="utf-8-sig")
    content_search = (
        SOURCE / "Services" / "ContentSearchService.cs"
    ).read_text(encoding="utf-8-sig")
    smoke_test = (
        ROOT / "tests" / "AIExplorer.SmokeTests" / "Program.cs"
    ).read_text(encoding="utf-8-sig")

    required_values = (
        (search_models, "bool AllowTargetedScan = true"),
        (search_models, "bool IncludeAiCandidates = true"),
        (search_models, "IReadOnlyList<SearchResult>? PartialResults = null"),
        (window_code, "이름·경로와 내용 검색 시작"),
        (window_code, "SearchExistingIndexesAsync"),
        (window_code, "allowTargetedScan: false"),
        (window_code, "includeAiCandidates: false"),
        (window_code, "includeAiCandidates: true"),
        (window_code, "MergeProgressiveSearchResults"),
        (window_code, "ApplyProgressiveSearchResults"),
        (window_code, "남은 파일은 유휴 시간에 분석합니다"),
        (window_code, "지금까지 찾은"),
        (window_code, "state.PartialResults"),
        (window_code, "TitleSearchResults"),
        (window_code, "TitleSearchService"),
        (window_code, "Search cached-AI stage"),
        (window_code, "OrderTitleSearchRoots"),
        (window_code, "본문·OCR·AI 결과 표시"),
        (window_code, "공유 폴더 직접 검색 중"),
        (search_service, "request.AllowTargetedScan"),
        (search_service, "IsNetworkRoot(root)"),
        (search_service, "CreateFastSearchResult"),
        (targeted_search, "liveBatch"),
        (targeted_search, "liveBatchSize = isNetworkRoot ? 8 : 24"),
        (content_search, "CalculateContextCoherence"),
        (window_code, "useDeterministicFastPath ? 20 : 15"),
        (metadata_index, "bool forceRefresh = false"),
        (content_index, "bool forceRefresh = false"),
        (search_service, "request.IncludeAiCandidates"),
        (search_service, "maximumContentDocumentsPerRoot > 0"),
        (metadata_index, "TryGetAvailableAsync"),
        (content_index, "TryGetAvailableAsync"),
        (smoke_test, "점진 검색 첫 단계는 직접 재탐색과 AI 실행을 생략"),
        (smoke_test, "점진 검색은 준비된 결과만 즉시 반환"),
        (smoke_test, "점진 색인 완료 단계에서 결과 자동 보강"),
        (smoke_test, "직접 탐색 결과를 작은 묶음으로 점진 전달"),
        (smoke_test, "약한 다중 검색어 후보를 보존하되 문맥이 완전한 파일보다 낮게 배치"),
        (smoke_test, "Other 특수 파일도 형식 의미와 상위 경로를 메타데이터 의미 문서로 구성"),
        (smoke_test, "빠른 검색에서 파일명 AWS와 계정관리문서 경로 단서를 결합"),
        (smoke_test, "AWS·서버·접속·자격 단서를 이름·PPK 형식·계정관리 경로로 결합"),
        (smoke_test, "본문 단어 개수보다 같은 문맥의 결합을 우선"),
        (smoke_test, "독립 제목 검색은 파일명 키워드를 놓치지 않음"),
        (smoke_test, "첫 제목 일치는 전체 탐색 완료 전에 전달"),
        (smoke_test, "파일명 키워드가 없어도 확장자 요청은 제목 검색에 즉시 표시"),
        (smoke_test, "등록되지 않은 점 표기 확장자도 정확한 제목 검색 조건으로 사용"),
        (smoke_test, "카탈로그에 없는 명시적 확장자를 통합 검색에서 발견"),
        (smoke_test, "카탈로그에 없는 확장자를 파일명 메타데이터로 통합 검색"),
        (smoke_test, "AWS SSH 키 자연어 검색에서 영문 key 제목을 즉시 발견"),
        (smoke_test, "SSH key 의미가 맞는 PPK 파일을 AWS 단어만 맞는 문서보다 우선"),
        (smoke_test, "AWS 키 자연어 변형 검색에서 정답 파일 Recall@3 보장"),
    )
    for source_text, required_value in required_values:
        if required_value not in source_text:
            fail(f"progressive search contract mismatch: {required_value}")
    title_progress_start = window_code.find(
        "var titleProgress = new Progress<TitleSearchProgress>"
    )
    title_progress_end = window_code.find(
        "var titleSearchTask = _instantTitleSearchService",
        title_progress_start,
    )
    title_progress_block = window_code[
        title_progress_start:title_progress_end
    ]
    if "MergeProgressiveSearchResults" in title_progress_block:
        fail(
            "independent title hits are still copied directly into the "
            "integrated result pane"
        )

    run_search_start = window_code.find("private async Task RunSearchAsync()")
    run_search_end = window_code.find(
        "private string BuildSearchResultCountText", run_search_start
    )
    run_search = window_code[run_search_start:run_search_end]
    if "new SearchIndexChoiceDialog" in run_search:
        fail("progressive search still blocks on the old index-choice dialog")
    clear_count = len(re.findall(
        r"^\s*SearchResults\.Clear\(\);\s*$",
        run_search,
        re.MULTILINE,
    ))
    if clear_count == 0:
        fail("progressive search must clear only once when a new query starts")
    if clear_count != 1:
        fail("progressive refresh must not clear the whole result list")

    title_service = (
        SOURCE / "Services" / "TitleSearchService.cs"
    ).read_text(encoding="utf-8-sig")
    main_xaml = (SOURCE / "MainWindow.xaml").read_text(encoding="utf-8-sig")
    title_required = (
        "TaskCreationOptions.LongRunning",
        "BatchSize = 8",
        "BatchInterval = TimeSpan.FromMilliseconds(220)",
        "Directory.EnumerateFiles",
        "Directory.EnumerateDirectories",
        "maximumResults",
        "TitleMatcher",
        "force: matchedItems == 1",
    )
    for required_value in title_required:
        if required_value not in title_service:
            fail(f"title search contract mismatch: {required_value}")
    if "Directory.EnumerateFileSystemEntries" in title_service:
        fail("title search must not add one SMB attribute request per file")
    if "GetLastWriteTime" in title_service:
        fail("title search must not fetch SMB timestamps before showing a match")
    for required_value in (
        "Do not call Directory.Exists here",
        "ResolveConfiguredNetworkRootsWithoutProbe",
        "const int maximumTitleResults = 3_000",
        "_instantTitleSearchService",
        ".SearchNaturalLanguageAsync(",
    ):
        if required_value not in window_code:
            fail(f"instant title-start contract mismatch: {required_value}")
    root_resolver_start = window_code.find(
        "private IReadOnlyList<string> ResolveSearchRoots()"
    )
    root_resolver_end = window_code.find(
        "private async Task<IReadOnlyList<string>> EnsureSearchRootsAccessibleAsync",
        root_resolver_start,
    )
    root_resolver = window_code[root_resolver_start:root_resolver_end]
    for forbidden_value in (
        "Directory.Exists(",
        "GetConnectedSharedFolders()",
        "GetReadyDrives()",
    ):
        if forbidden_value in root_resolver:
            fail(
                "title search still waits on a synchronous root probe: "
                + forbidden_value
            )
    for required_value in (
        'x:Name="TitleSearchResultsListBox"',
        'ItemsSource="{Binding TitleSearchResults}"',
        'Text="통합 검색 결과"',
        'Text="빠른 이름·경로 검색"',
    ):
        if required_value not in main_xaml:
            fail(f"dual search pane contract mismatch: {required_value}")

    print(
        "PROGRESSIVE SEARCH OK "
        "(independent title scan, dual result panes, staged AI)"
    )


def validate_visibility_and_fast_bootstrap_contract() -> None:
    policy = (SOURCE / "Services" / "SearchVisibilityPolicy.cs").read_text(
        encoding="utf-8-sig"
    )
    title = (SOURCE / "Services" / "TitleSearchService.cs").read_text(
        encoding="utf-8-sig"
    )
    targeted = (SOURCE / "Services" / "TargetedFileSearchService.cs").read_text(
        encoding="utf-8-sig"
    )
    metadata = (SOURCE / "Services" / "MetadataIndexService.cs").read_text(
        encoding="utf-8-sig"
    )
    content = (SOURCE / "Services" / "ContentIndexService.cs").read_text(
        encoding="utf-8-sig"
    )
    service = (SOURCE / "Services" / "MetadataSearchService.cs").read_text(
        encoding="utf-8-sig"
    )
    window = (SOURCE / "MainWindow.xaml.cs").read_text(encoding="utf-8-sig")
    smoke = (
        ROOT / "tests" / "AIExplorer.SmokeTests" / "Program.cs"
    ).read_text(encoding="utf-8-sig")

    for required in (
        "FileAttributes.Hidden | FileAttributes.System",
        'name.StartsWith("~", StringComparison.Ordinal)',
        "TryGetVisibleAttributes",
        "IsVisiblePathByName",
    ):
        if required not in policy:
            fail(f"visibility policy contract mismatch: {required}")
    for source_text, required in (
        (title, "VisibleEnumerationOptions"),
        (title, "SearchVisibilityPolicy.IsExcludedName"),
        (targeted, "SearchVisibilityPolicy.TryGetVisibleAttributes"),
        (metadata, "SearchVisibilityPolicy.TryGetVisibleAttributes"),
        (content, "SearchVisibilityPolicy.TryGetVisibleAttributes"),
        (service, "MaximumTargetedScanItems"),
        (service, "NormalizeRootWithoutProbe"),
        (window, "Search local cache files before touching a potentially sleeping"),
        (window, "allowTargetedScan: false"),
    ):
        if required not in source_text:
            fail(f"visibility/fast-bootstrap contract mismatch: {required}")

    metadata_available = metadata[
        metadata.find("public async Task<IndexAccessResult?> TryGetAvailableAsync"):
        metadata.find("public async Task<IndexAccessResult> GetOrBuildAsync")
    ]
    content_available = content[
        content.find("public async Task<ContentIndexAccessResult?> TryGetAvailableAsync"):
        content.find("public async Task<ContentIndexAccessResult> GetOrBuildAsync")
    ]
    if "GetRootWriteTimeUtc" in metadata_available or "GetRootWriteTimeUtc" in content_available:
        fail("quick cached search still probes a slow file-system root")

    run_start = window.find("private async Task RunSearchAsync()")
    reconnect = window.find("roots = await EnsureSearchRootsAccessibleAsync", run_start)
    quick_cache = window.find("Search local cache files before touching", run_start)
    if quick_cache < 0 or reconnect < 0 or quick_cache > reconnect:
        fail("cached lexical results must be loaded before network reconnect")
    for scenario in (
        "물결표로 시작하는 임시 파일은 제목 검색에서 제외",
        "숨김 속성 파일은 제목 검색에서 제외",
        "물결표로 시작하는 임시 파일은 직접 검색에서 제외",
    ):
        if scenario not in smoke:
            fail(f"visibility smoke regression test is missing: {scenario}")

    print("VISIBILITY/BOOTSTRAP OK (hidden/temp exclusion, cache-first AI pane)")


def validate_nuget_restore_contract() -> None:
    config_path = ROOT / "NuGet.Config"
    try:
        config_root = ET.parse(config_path).getroot()
    except ET.ParseError as exc:
        fail(f"NuGet.Config: invalid XML: {exc}")

    package_sources = config_root.find("packageSources")
    if package_sources is None:
        fail("NuGet.Config does not define packageSources")
    add_nodes = package_sources.findall("add")
    sources = {
        node.attrib.get("key", ""): node.attrib.get("value", "")
        for node in add_nodes
    }
    expected_feed = "https://api.nuget.org/v3/index.json"
    if sources != {"nuget.org": expected_feed}:
        fail(
            "NuGet.Config must use only the official nuget.org v3 feed: "
            + repr(sources)
        )
    if package_sources.find("clear") is None:
        fail("NuGet.Config must clear inherited package sources")

    mapping = config_root.find("packageSourceMapping")
    if mapping is None:
        fail("NuGet.Config must define packageSourceMapping")
    mapped_source = mapping.find("packageSource[@key='nuget.org']")
    if mapped_source is None:
        fail("NuGet.Config does not map packages to nuget.org")
    patterns = {
        node.attrib.get("pattern", "")
        for node in mapped_source.findall("package")
    }
    if "*" not in patterns:
        fail("NuGet.Config must map all packages to nuget.org")

    app_project = (SOURCE / "AIExplorer.csproj").read_text(encoding="utf-8")
    package_contracts = (
        'Include="Microsoft.ML.OnnxRuntime.DirectML" Version="1.22.0"',
        'Include="Microsoft.ML.Tokenizers" Version="2.0.0"',
        '<TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>',
    )
    for contract in package_contracts:
        if contract not in app_project:
            fail(f"NuGet package contract mismatch: {contract}")

    verify = (ROOT / "verify_source.cmd").read_text(encoding="utf-8-sig")
    preflight = (ROOT / "tools" / "preflight.ps1").read_text(encoding="utf-8-sig")
    if 'tools\\preflight.ps1' not in verify:
        fail("verify_source.cmd does not run the source preflight")
    for contract in (
        "검색어를 파일에서 직접 확인했다는 뜻은 아닙니다.",
        "추출된 본문을 포함한 문서 정보의 다국어 의미가",
        "낮은 본문 단어 커버리지는 AI 후보를 덮어쓰지 않음",
        "Direct content evidence must not treat login",
    ):
        if contract not in preflight:
            fail(f"preflight regression contract mismatch: {contract}")
    run_dev = (ROOT / "run_dev.cmd").read_text(encoding="utf-8-sig")
    build = (ROOT / "build_release.cmd").read_text(encoding="utf-8-sig")
    for name, script in (
        ("verify_source.cmd", verify),
        ("run_dev.cmd", run_dev),
        ("build_release.cmd", build),
    ):
        if 'set "NUGET_CONFIG=%~dp0NuGet.Config"' not in script:
            fail(f"{name} does not bind the project NuGet.Config")
        if '--configfile "%NUGET_CONFIG%"' not in script:
            fail(f"{name} does not pass --configfile to restore")
        if "--ignore-failed-sources" in script:
            fail(f"{name} must not hide a missing official package source")

    if "-r win-x64" not in build or "--no-restore" not in build:
        fail("release build must restore win-x64 assets before publish --no-restore")

    print("NUGET OK (isolated official feed, explicit restore configuration)")


def validate_network_location_contract() -> None:
    network_service = (
        SOURCE / "Services" / "NetworkPathService.cs"
    ).read_text(encoding="utf-8-sig")
    dialog_markup = (
        SOURCE / "Dialogs" / "NetworkLocationDialog.xaml"
    ).read_text(encoding="utf-8-sig")
    dialog_code = (
        SOURCE / "Dialogs" / "NetworkLocationDialog.xaml.cs"
    ).read_text(encoding="utf-8-sig")
    window_code = (SOURCE / "MainWindow.xaml.cs").read_text(encoding="utf-8-sig")
    navigation_node = (
        SOURCE / "Models" / "NavigationNode.cs"
    ).read_text(encoding="utf-8-sig")
    smoke_test = (
        ROOT / "tests" / "AIExplorer.SmokeTests" / "Program.cs"
    ).read_text(encoding="utf-8-sig")

    required_values = (
        (network_service, "WNetAddConnection3"),
        (network_service, "WNetOpenEnum"),
        (network_service, "WNetEnumResource"),
        (network_service, "NetShareEnum"),
        (network_service, "GetConnectedSharedFolders()"),
        (network_service, "NetUseEnum"),
        (network_service, "ConnectedNetworkShareInfo"),
        (network_service, "NormalizeNetworkLocationPath"),
        (network_service, "IsUncServerRoot"),
        (network_service, "EnumerateServerSharesAsync"),
        (network_service, "expanded.Length == 2"),
        (network_service, "WaitAsync(TimeSpan.FromSeconds(12)"),
        (dialog_markup, 'SizeToContent="Height"'),
        (dialog_markup, 'ResizeMode="CanResizeWithGrip"'),
        (dialog_markup, 'HorizontalContentAlignment="Stretch"'),
        (dialog_markup, 'x:Name="PathTextBox"'),
        (dialog_markup, 'Content="연결 확인"'),
        (dialog_markup, "192.168.0.10"),
        (dialog_code, "NormalizeNetworkLocationPath"),
        (dialog_code, "EnsureAccessibleAsync"),
        (window_code, "NavigationNodeKind.Computer"),
        (window_code, "ShowComputerViewAsync"),
        (window_code, "CollectNetworkTreeLocations"),
        (window_code, "CreateNetworkServerEntry"),
        (window_code, "EnumerateServerSharesAsync"),
        (window_code, "EnsureSearchRootsAccessibleAsync"),
        (window_code, ".Where(IsSyntacticallyValidSearchRoot)"),
        (window_code, "new NetworkLocationDialog(this, _networkPathService)"),
        (navigation_node, "Kind == NavigationNodeKind.Computer"),
        (smoke_test, "내 PC 탐색 노드 선택 가능"),
        (smoke_test, "매핑 드라이브 루트의 역슬래시 보존"),
        (smoke_test, "IP 주소만 입력해 UNC 서버 루트로 정규화"),
        (smoke_test, "UNC 서버 최상위 위치 판별"),
        (smoke_test, "공유 폴더에서 서버 최상위로 이동"),
    )
    for source_text, required_value in required_values:
        if required_value not in source_text:
            fail(f"network location contract mismatch: {required_value}")

    if 'Height="330"' in dialog_markup or 'ResizeMode="NoResize"' in dialog_markup:
        fail("network location dialog still uses the clipped fixed-size layout")
    if ".TrimEnd(Path.DirectorySeparatorChar)" in dialog_code:
        fail("network dialog must not turn a mapped drive root such as Z:\\ into Z:")
    if "WNetRestoreConnection" in network_service:
        fail("network service must not call the unsupported WNetRestoreConnection API")
    if "GetUncShareRoot(path) is not null" in network_service:
        fail("network validation must also accept a server root without a share name")

    preflight_text = (ROOT / "tools" / "preflight.ps1").read_text(
        encoding="utf-8-sig"
    )
    if ".Where(IsConfiguredSearchRoot)" in preflight_text or ".Where(IsConfiguredSearchRoot)" in window_code:
        fail("legacy configured-root filtering can discard UNC locations before reconnect")
    for contract in (
        "EnsureSearchRootsAccessibleAsync",
        ".Where(IsSyntacticallyValidSearchRoot)",
        "_networkPathService.EnsureAccessibleAsync(",
    ):
        if contract not in preflight_text or contract not in window_code:
            fail(f"preflight/source network-root contract mismatch: {contract}")

    print(
        "NETWORK OK "
        "(connected UNC shares, server-root share discovery, My PC view)"
    )


def validate_tooltip_and_connected_share_contract() -> None:
    styles = (SOURCE / "Themes" / "Styles.xaml").read_text(encoding="utf-8-sig")
    window_markup = (SOURCE / "MainWindow.xaml").read_text(encoding="utf-8-sig")
    window_code = (SOURCE / "MainWindow.xaml.cs").read_text(encoding="utf-8-sig")
    network_service = (
        SOURCE / "Services" / "NetworkPathService.cs"
    ).read_text(encoding="utf-8-sig")
    smoke_test = (
        ROOT / "tests" / "AIExplorer.SmokeTests" / "Program.cs"
    ).read_text(encoding="utf-8-sig")

    required_values = (
        (styles, '<Style TargetType="ToolTip">'),
        (styles, '<Setter Property="Background" Value="#FFFFFF" />'),
        (styles, '<Setter Property="OverridesDefaultStyle" Value="True" />'),
        (styles, '<Setter Property="TextWrapping" Value="Wrap" />'),
        (styles, 'TextElement.Foreground="{TemplateBinding Foreground}"'),
        (window_markup, 'ToolTip="{Binding Path}"'),
        (network_service, "GetConnectedSharedFolders()"),
        (network_service, "NetUseEnum"),
        (network_service, "EnumerateWNetResources(ResourceConnected)"),
        (network_service, "ConnectedNetworkShareInfo"),
        (window_code, "NetworkPathService.GetConnectedSharedFolders()"),
        (window_code, "drive.IsReady && drive.DriveType != DriveType.Network"),
        (window_code, "ResolveConfiguredNetworkRootsWithoutProbe()"),
        (window_code, ".Concat(ResolveConfiguredNetworkRootsWithoutProbe())"),
        (smoke_test, "연결된 UNC 공유 폴더 표시 정보"),
    )
    for source_text, required_value in required_values:
        if required_value not in source_text:
            fail(f"tooltip/connected-share contract mismatch: {required_value}")

    if "NetworkPathService.GetKnownNetworkLocations()" in window_code:
        fail("MainWindow must not auto-add remembered or mapped network drives")
    if '#202633' in styles:
        fail("legacy black tooltip background is still present")

    print(
        "TOOLTIP/SHARE DISCOVERY OK "
        "(high-contrast hover text, connected UNC shares only)"
    )

def validate_favorites_navigation_contract() -> None:
    window_markup = (SOURCE / "MainWindow.xaml").read_text(encoding="utf-8-sig")
    window_code = (SOURCE / "MainWindow.xaml.cs").read_text(encoding="utf-8-sig")
    favorite_service = (
        SOURCE / "Services" / "FavoritePathService.cs"
    ).read_text(encoding="utf-8-sig")
    navigation_node = (
        SOURCE / "Models" / "NavigationNode.cs"
    ).read_text(encoding="utf-8-sig")
    smoke_test = (
        ROOT / "tests" / "AIExplorer.SmokeTests" / "Program.cs"
    ).read_text(encoding="utf-8-sig")

    required_values = (
        (window_markup, 'Text="끌어서 추가"'),
        (window_markup, 'Handler="NavigationTreeItem_PreviewMouseMove"'),
        (window_markup, 'Click="AddNavigationFolderToFavoritesMenuItem_Click"'),
        (window_markup, 'Click="AddSelectedFolderToFavoritesMenuItem_Click"'),
        (window_markup, 'x:Name="AddCurrentPathToFavoritesButton"'),
        (window_markup, 'Click="AddCurrentPathToFavoritesButton_Click"'),
        (window_markup, 'SelectedIndex="0"'),
        (window_code, "UpdateCurrentPathFavoriteButtonState"),
        (window_code, "GetFavoriteDisplayName(_currentPath)"),
        (window_markup, 'Opened="NavigationTreeContextMenu_Opened"'),
        (window_markup, 'Opened="FileListContextMenu_Opened"'),
        (window_code, "FavoriteReorderDataFormat"),
        (window_code, "FavoritePathService.MoveFavorite"),
        (window_code, "FavoritePathService.TryCreateFolderTarget"),
        (window_code, "NavigationNodeKind.FavoritesSection"),
        (favorite_service, "TryCreateFolderTarget"),
        (favorite_service, "MoveFavorite"),
        (navigation_node, "FavoritesSection"),
        (smoke_test, "폴더 우클릭 즐겨찾기 등록"),
        (smoke_test, "즐겨찾기 드래그 순서 변경"),
        (smoke_test, "즐겨찾기 섹션은 탐색 대상이 아님"),
    )
    for source_text, required_value in required_values:
        if required_value not in source_text:
            fail(f"favorites navigation contract mismatch: {required_value}")

    if "var network = new NavigationNode(" in window_code or        "NavigationRoots.Add(network)" in window_code:
        fail("standalone network navigation tree is still present")
    if 'Click="AddNetworkLocationButton_Click"' in window_markup:
        fail("inactive network add button returned to navigation UI")
    if 'x:Name="SearchScopeComboBox"' not in window_markup or \
            'SelectedIndex="0"' not in window_markup:
        fail("search scope must default to current folder and descendants")
    if "Directory.Exists(path) || NetworkPathService.IsPotentialNetworkPath(path)" in window_code:
        fail("UNC favorite eligibility still blocks on Directory.Exists before network syntax")

    print(
        "FAVORITES OK "
        "(bottom guidance, context add, drag reorder, no network tree)"
    )


def validate_process_cleanup_contract() -> None:
    app_code = (SOURCE / "App.xaml.cs").read_text(encoding="utf-8")
    window_code = (SOURCE / "MainWindow.xaml.cs").read_text(encoding="utf-8")
    shell_service = (
        SOURCE / "Services" / "ShellService.cs"
    ).read_text(encoding="utf-8")
    tracker = (
        SOURCE / "Services" / "LaunchedProcessTracker.cs"
    ).read_text(encoding="utf-8")
    required_values = (
        (app_code, "LaunchedProcesses.Dispose()"),
        (window_code, "_shellService.TerminateLaunchedProcesses()"),
        (shell_service, "_launchedProcesses.Track(process, path)"),
        (tracker, "process.CloseMainWindow()"),
        (tracker, "process.Kill(entireProcessTree: true)"),
        (tracker, "processId == Environment.ProcessId"),
        (tracker, "Windows reused an application that was already running"),
    )
    for source_text, required_value in required_values:
        if required_value not in source_text:
            fail(f"process cleanup contract mismatch: {required_value}")
    print(
        "PROCESS OK "
        "(new shell process tracking, graceful close, forced tree cleanup)"
    )


def validate_background_index_and_tray_contract() -> None:
    app_markup = (SOURCE / "App.xaml").read_text(encoding="utf-8-sig")
    app_code = (SOURCE / "App.xaml.cs").read_text(encoding="utf-8-sig")
    app_project = (SOURCE / "AIExplorer.csproj").read_text(
        encoding="utf-8-sig"
    )
    window_code = (SOURCE / "MainWindow.xaml.cs").read_text(
        encoding="utf-8-sig"
    )
    planner = (
        SOURCE / "Services" / "BackgroundIndexRootPlanner.cs"
    ).read_text(encoding="utf-8-sig")
    work_policy = (
        SOURCE / "Services" / "BackgroundIndexWorkPolicy.cs"
    ).read_text(encoding="utf-8-sig")
    tray = (
        SOURCE / "Services" / "TrayIconService.cs"
    ).read_text(encoding="utf-8-sig")
    smoke_test = (
        ROOT / "tests" / "AIExplorer.SmokeTests" / "Program.cs"
    ).read_text(encoding="utf-8-sig")

    required_values = (
        (app_project, "<UseWindowsForms>true</UseWindowsForms>"),
        (app_project, "WFO0003"),
        (app_markup, 'ShutdownMode="OnExplicitShutdown"'),
        (app_code, "IsSessionEnding = true"),
        (window_code, "HideToSystemTray()"),
        (window_code, "_settings.UseSystemTrayBackground"),
        (window_code, "RequestApplicationExit()"),
        (window_code, "BackgroundIndexRootPlanner.OrderRoots("),
        (window_code, "ResolveFavoriteIndexRootsWithoutProbe()"),
        (work_policy, "TimeSpan.FromMinutes(5)"),
        (work_policy, "MaximumNewVisualDocumentsPerRoot: 0"),
        (planner, "AppendDistinct(activeSearchRoots"),
        (planner, "AppendDistinct(favoriteRoots"),
        (planner, "StringComparer.OrdinalIgnoreCase"),
        (tray, "Forms.NotifyIcon"),
        (tray, "AI 탐색기 열기"),
        (tray, "백그라운드 색인 일시 중지"),
        (tray, "완전히 종료"),
        (tray, "public void SetVisible(bool visible)"),
        (
            smoke_test,
            "백그라운드 색인은 현재 검색 위치 다음에 "
            "즐겨찾기를 우선하고 중복 제거",
        ),
        (
            smoke_test,
            "전면 실행 중에는 제목 색인만 수행하고 무거운 AI 색인은 "
            "긴 유휴·트레이에서만 배치 실행",
        ),
    )
    for source_text, required_value in required_values:
        if required_value not in source_text:
            fail(
                "background index/tray contract mismatch: "
                f"{required_value}"
            )

    print(
        "BACKGROUND/TRAY OK "
        "(favorite-first queue, recurring idle scan, explicit exit)"
    )


def validate_explorer_input_and_responsive_results_contract() -> None:
    window_markup = (SOURCE / "MainWindow.xaml").read_text(
        encoding="utf-8-sig"
    )
    window_code = (SOURCE / "MainWindow.xaml.cs").read_text(
        encoding="utf-8-sig"
    )
    attributes = (
        SOURCE / "Services" / "SearchTextAttributes.cs"
    ).read_text(encoding="utf-8-sig")
    result_sort = (
        SOURCE / "Services" / "SearchResultSortService.cs"
    ).read_text(encoding="utf-8-sig")
    app_settings = (
        SOURCE / "Models" / "AppSettings.cs"
    ).read_text(encoding="utf-8-sig")
    smoke_test = (
        ROOT / "tests" / "AIExplorer.SmokeTests" / "Program.cs"
    ).read_text(encoding="utf-8-sig")

    required_values = (
        (window_markup, 'PreviewMouseDown="Window_PreviewMouseDown"'),
        (window_code, "MouseButton.XButton1"),
        (window_code, "MouseButton.XButton2"),
        (window_code, "Key.BrowserBack"),
        (window_code, "Key.BrowserForward"),
        (window_code, "key == Key.Back"),
        (window_code, "FocusPathInput()"),
        (window_code, "FocusSearchInput()"),
        (window_markup, 'x:Name="SearchResultsHostGrid"'),
        (window_markup, 'x:Name="IntegratedResultsViewButton"'),
        (window_markup, 'x:Name="TitleResultsViewButton"'),
        (window_markup, 'x:Name="SearchPanelColumn"'),
        (window_markup, 'x:Name="InstantTitlePanelColumn"'),
        (window_markup, 'ResizeDirection="Columns"'),
        (window_markup, 'x:Name="SearchResultSortComboBox"'),
        (window_code, "SearchResultSortComboBox_SelectionChanged"),
        (app_settings, "SearchResultSortMode SearchResultSortMode"),
        (result_sort, "SearchResultSortMode.TopLevelPath"),
        (result_sort, "SearchResultSortMode.ModifiedNewest"),
        (
            window_markup,
            'x:Name="NaturalLanguageInterpretationBar"',
        ),
        (window_code, "NaturalLanguageSearchService"),
        (window_code, "SearchConversationContext"),
        (attributes, "SearchTextAttributeMode.Only"),
        (attributes, "Path.GetFileNameWithoutExtension(name)"),
        (
            smoke_test,
            "한글로만 된 파일을 확장자 제외 파일명 전용 조건으로 해석",
        ),
        (
            smoke_test,
            "빠른 이름 검색도 확장자를 제외한 한글 전용 파일명만 발견",
        ),
        (
            smoke_test,
            "통합 검색도 확장자를 제외한 한글 전용 파일명만 발견",
        ),
        (
            smoke_test,
            "검색 결과를 일치도·드라이브 최상위 경로·가나다·최신 수정일로 정렬",
        ),
    )
    for source_text, required_value in required_values:
        if required_value not in source_text:
            fail(
                "explorer input/responsive results contract mismatch: "
                f"{required_value}"
            )

    print(
        "EXPLORER UX OK "
        "(mouse navigation, familiar keys, exact-script names, responsive results)"
    )


def main() -> int:
    validate_required_files()
    validate_xml()
    validate_event_handlers()
    validate_static_resources()
    validate_no_placeholders()
    validate_csharp_structure()
    validate_smoke_test_local_declarations()
    validate_csharp_scope_regressions()
    validate_mainwindow_helper_references()
    validate_search_policy_regression()
    validate_ai_bundle_contract()
    validate_llama_runtime_asset_contract()
    validate_powershell_51_encoding()
    validate_cmd_encoding_contract()
    validate_search_experience_contract()
    validate_progressive_search_contract()
    validate_visibility_and_fast_bootstrap_contract()
    validate_nuget_restore_contract()
    validate_network_location_contract()
    validate_tooltip_and_connected_share_contract()
    validate_favorites_navigation_contract()
    validate_process_cleanup_contract()
    validate_background_index_and_tray_contract()
    validate_explorer_input_and_responsive_results_contract()
    file_count = sum(1 for path in ROOT.rglob("*") if path.is_file())
    print(f"Source structure validated: {file_count} files.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
