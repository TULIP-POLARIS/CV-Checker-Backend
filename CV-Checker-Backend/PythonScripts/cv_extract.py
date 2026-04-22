import json
import os
import re
import sys
from pathlib import Path
from typing import Dict, List, Tuple, Any

# Add local packaged dependencies path first
extra_paths = [
    r"C:\home\site\wwwroot\PythonPackages"
]

for p in extra_paths:
    if p and os.path.isdir(p) and p not in sys.path:
        sys.path.insert(0, p)

from PyPDF2 import PdfReader
from docx import Document


# =========================================================
# FILE READING
# =========================================================
def read_txt(file_path: Path) -> str:
    return file_path.read_text(encoding="utf-8", errors="ignore")


def read_pdf(file_path: Path) -> str:
    reader = PdfReader(str(file_path))
    pages = []

    for page in reader.pages:
        try:
            text = page.extract_text() or ""
        except Exception:
            text = ""
        pages.append(text)

    return "\n".join(pages)


def read_docx(file_path: Path) -> str:
    doc = Document(str(file_path))
    return "\n".join(p.text for p in doc.paragraphs)


def read_cv(file_path: Path) -> str:
    suffix = file_path.suffix.lower()

    if suffix == ".txt":
        return read_txt(file_path)
    if suffix == ".pdf":
        return read_pdf(file_path)
    if suffix == ".docx":
        return read_docx(file_path)

    raise ValueError("Unsupported file type. Use .txt, .pdf, or .docx")


# =========================================================
# TEXT CLEANING
# =========================================================
def clean_text(text: str) -> str:
    text = text.replace("\xa0", " ")
    text = re.sub(r"[ \t]+", " ", text)
    text = re.sub(r"\r\n", "\n", text)
    text = re.sub(r"\n{3,}", "\n\n", text)
    return text.strip()


def get_lines(text: str) -> List[str]:
    lines = []
    for line in text.splitlines():
        cleaned = line.strip()
        cleaned = cleaned.strip("•")
        cleaned = cleaned.strip("-")
        cleaned = cleaned.strip()
        if cleaned:
            lines.append(cleaned)
    return lines


def normalize_heading(line: str) -> str:
    line = line.strip().lower().rstrip(":")
    line = re.sub(r"[^a-z0-9\s]", "", line)
    line = re.sub(r"\s+", " ", line)
    return line


# =========================================================
# CONTACT EXTRACTION
# =========================================================
def extract_email(text: str) -> str:
    match = re.search(r"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", text)
    return match.group(0) if match else ""


def extract_phone(text: str) -> str:
    patterns = [
        r"(\+\d{1,3}[\s\-]?\(?\d+\)?(?:[\s\-]?\d+){5,})",
        r"(\(?\d{2,4}\)?[\s\-]?\d{3,4}[\s\-]?\d{3,4})",
    ]

    for pattern in patterns:
        match = re.search(pattern, text)
        if match:
            return match.group(0).strip()

    return ""


def extract_linkedin(text: str) -> str:
    match = re.search(
        r"(https?://)?(www\.)?linkedin\.com/in/[A-Za-z0-9\-_/]+",
        text,
        re.IGNORECASE
    )
    return match.group(0) if match else ""


def extract_github(text: str) -> str:
    match = re.search(
        r"(https?://)?(www\.)?github\.com/[A-Za-z0-9\-_/]+",
        text,
        re.IGNORECASE
    )
    return match.group(0) if match else ""


# =========================================================
# HEADER / TOP OF CV EXTRACTION
# =========================================================
def guess_name_from_top(lines: List[str]) -> str:
    blocked_words = {
        "curriculum",
        "resume",
        "cv",
        "profile",
        "summary",
        "experience",
        "education",
        "skills",
        "developer",
        "engineer",
        "student",
        "manager",
        "specialist",
        "consultant",
        "analyst",
        "designer",
        "intern",
        "full stack",
        "frontend",
        "backend",
        "software"
    }

    candidates = []

    for line in lines[:8]:
        if len(line) > 50:
            continue

        lower = line.lower()

        if "@" in lower or "linkedin.com" in lower or "github.com" in lower:
            continue

        if re.search(r"\d", line):
            continue

        words = line.split()
        if 2 <= len(words) <= 4 and re.fullmatch(r"[A-Za-zÀ-ÿ' -]+", line):
            if not any(word in lower for word in blocked_words):
                candidates.append(line)

    return candidates[0] if candidates else ""


def guess_job_title_from_top(lines: List[str], full_name: str) -> str:
    blocked = [
        "@",
        "linkedin.com",
        "github.com",
        "education",
        "experience",
        "skills",
        "projects",
        "certifications",
        "languages",
        "summary",
        "phone",
        "email"
    ]

    for line in lines[:10]:
        if not line:
            continue

        if full_name and line.strip().lower() == full_name.strip().lower():
            continue

        lowered = line.lower()

        if len(line) > 60:
            continue

        if any(b in lowered for b in blocked):
            continue

        if re.search(r"\d", line):
            continue

        if 2 <= len(line.split()) <= 8:
            return line

    return ""


def guess_location(lines: List[str]) -> str:
    for line in lines[:12]:
        lowered = line.lower()

        if "linkedin.com" in lowered or "github.com" in lowered or "@" in lowered:
            continue

        if re.search(r"\+\d", line):
            continue

        if len(line) > 50:
            continue

        if re.fullmatch(r"[A-Za-zÀ-ÿ0-9,.\- ]+", line):
            if "," in line or 1 <= len(line.split()) <= 4:
                return line

    return ""


# =========================================================
# SECTION DETECTION
# =========================================================
SECTION_ALIASES = {
    "summary": [
        "professional summary",
        "summary",
        "profile",
        "about me",
        "about",
        "personal profile"
    ],
    "skills": [
        "skills",
        "technical skills",
        "core competencies",
        "competencies",
        "tech stack",
        "technical competencies"
    ],
    "work_experience": [
        "work experience",
        "experience",
        "professional experience",
        "employment history",
        "employment",
        "career history"
    ],
    "education": [
        "education",
        "academic background",
        "academic history",
        "studies"
    ],
    "certifications": [
        "certifications",
        "licenses",
        "certificates"
    ],
    "projects": [
        "projects",
        "personal projects",
        "academic projects"
    ],
    "languages": [
        "languages",
        "language skills"
    ],
    "achievements": [
        "achievements",
        "awards",
        "accomplishments"
    ]
}


def build_heading_lookup() -> Dict[str, str]:
    lookup: Dict[str, str] = {}
    for canonical, aliases in SECTION_ALIASES.items():
        for alias in aliases:
            lookup[normalize_heading(alias)] = canonical
    return lookup


def find_section_ranges(lines: List[str]) -> Dict[str, Tuple[int, int]]:
    heading_lookup = build_heading_lookup()
    found_headings: List[Tuple[str, int]] = []

    for i, line in enumerate(lines):
        norm = normalize_heading(line)
        if norm in heading_lookup:
            found_headings.append((heading_lookup[norm], i))

    ranges: Dict[str, Tuple[int, int]] = {}

    for idx, (section_name, start_heading_idx) in enumerate(found_headings):
        start_content_idx = start_heading_idx + 1
        end_idx = len(lines)

        if idx + 1 < len(found_headings):
            end_idx = found_headings[idx + 1][1]

        if section_name not in ranges:
            ranges[section_name] = (start_content_idx, end_idx)

    return ranges


def extract_section_texts(lines: List[str]) -> Dict[str, str]:
    ranges = find_section_ranges(lines)
    results: Dict[str, str] = {}

    for section_name in SECTION_ALIASES.keys():
        if section_name in ranges:
            start, end = ranges[section_name]
            content = [line for line in lines[start:end] if line.strip()]
            results[section_name] = "\n".join(content).strip()
        else:
            results[section_name] = ""

    return results


# =========================================================
# PARSING HELPERS
# =========================================================
def dedupe_preserve_order(items: List[str]) -> List[str]:
    seen = set()
    result = []

    for item in items:
        key = item.strip().lower()
        if key and key not in seen:
            seen.add(key)
            result.append(item.strip())

    return result


def split_items(text: str) -> List[str]:
    if not text:
        return []

    lines = [line.strip("•- \t") for line in text.splitlines() if line.strip()]
    items: List[str] = []

    for line in lines:
        if len(line) < 150 and any(sep in line for sep in [",", "|", ";"]):
            parts = re.split(r"[,;|]+", line)
            for p in parts:
                p = p.strip()
                if p:
                    items.append(p)
        else:
            items.append(line)

    return dedupe_preserve_order(items)


def looks_like_date_range(text: str) -> bool:
    patterns = [
        r"\b\d{4}\s*-\s*\d{4}\b",
        r"\b\d{4}\s*-\s*(present|current)\b",
        r"\b(?:jan|feb|mar|apr|may|jun|jul|aug|sep|sept|oct|nov|dec)[a-z]*\s+\d{4}\s*-\s*(?:present|current|(?:jan|feb|mar|apr|may|jun|jul|aug|sep|sept|oct|nov|dec)[a-z]*\s+\d{4})\b",
    ]

    lower = text.lower()
    return any(re.search(pattern, lower) for pattern in patterns)


def extract_date_range(text: str) -> Tuple[str, str]:
    lower = text.lower()

    match = re.search(r"\b(\d{4})\s*-\s*(\d{4}|present|current)\b", lower)
    if match:
        start = match.group(1)
        end = match.group(2)
        if end in ["present", "current"]:
            end = "Present"
        return start, end

    match = re.search(
        r"\b((?:jan|feb|mar|apr|may|jun|jul|aug|sep|sept|oct|nov|dec)[a-z]*\s+\d{4})\s*-\s*((?:jan|feb|mar|apr|may|jun|jul|aug|sep|sept|oct|nov|dec)[a-z]*\s+\d{4}|present|current)\b",
        lower
    )
    if match:
        start = match.group(1).title()
        end = match.group(2).title()
        if end in ["Present", "Current"]:
            end = "Present"
        return start, end

    return "", ""


# =========================================================
# SECTION PARSERS
# =========================================================
def parse_skills(text: str) -> List[str]:
    return split_items(text)


def parse_languages(text: str) -> List[str]:
    return split_items(text)


def parse_simple_entries(text: str, field_name: str) -> List[Dict[str, str]]:
    if not text:
        return []

    text = text.strip()
    if not text:
        return []

    blocks = re.split(r"\n{2,}", text)

    if len(blocks) == 1:
        blocks = [line.strip() for line in text.splitlines() if line.strip()]

    result = []
    for block in blocks:
        block = block.strip()
        if not block:
            continue
        result.append({field_name: block})

    return result


def parse_work_experience(text: str) -> List[Dict[str, str]]:
    if not text:
        return []

    text = text.strip()
    if not text:
        return []

    blocks = re.split(r"\n{2,}", text)

    if len(blocks) == 1:
        lines = [l.strip() for l in text.splitlines() if l.strip()]
        if len(lines) <= 3:
            return [{
                "Role": lines[0] if len(lines) > 0 else "",
                "Company": lines[1] if len(lines) > 1 else "",
                "StartDate": "",
                "EndDate": "",
                "Description": " ".join(lines[2:]) if len(lines) > 2 else ""
            }]
        return [{"Raw": text}]

    results = []

    for block in blocks:
        lines = [l.strip() for l in block.splitlines() if l.strip()]
        if not lines:
            continue

        role = ""
        company = ""
        start_date = ""
        end_date = ""
        description_lines: List[str] = []

        if len(lines) >= 1:
            role = lines[0]

        if len(lines) >= 2:
            if looks_like_date_range(lines[1]):
                start_date, end_date = extract_date_range(lines[1])
            else:
                company = lines[1]

        if len(lines) >= 3:
            if not company and not looks_like_date_range(lines[2]):
                company = lines[2]
                if len(lines) >= 4 and looks_like_date_range(lines[3]):
                    start_date, end_date = extract_date_range(lines[3])
                    description_lines = lines[4:]
                else:
                    description_lines = lines[3:]
            else:
                if not start_date and looks_like_date_range(lines[2]):
                    start_date, end_date = extract_date_range(lines[2])
                    description_lines = lines[3:]
                else:
                    description_lines = lines[2:]

        results.append({
            "Role": role,
            "Company": company,
            "StartDate": start_date,
            "EndDate": end_date,
            "Description": " ".join(description_lines).strip()
        })

    return results


def parse_education(text: str) -> List[Dict[str, str]]:
    if not text:
        return []

    text = text.strip()
    if not text:
        return []

    blocks = re.split(r"\n{2,}", text)

    if len(blocks) == 1:
        lines = [l.strip() for l in text.splitlines() if l.strip()]
        if len(lines) <= 3:
            return [{
                "Institution": lines[0] if len(lines) > 0 else "",
                "Degree": lines[1] if len(lines) > 1 else "",
                "StartDate": "",
                "EndDate": "",
                "Description": " ".join(lines[2:]) if len(lines) > 2 else ""
            }]
        return [{"Raw": text}]

    results = []

    for block in blocks:
        lines = [l.strip() for l in block.splitlines() if l.strip()]
        if not lines:
            continue

        institution = lines[0] if len(lines) > 0 else ""
        degree = ""
        start_date = ""
        end_date = ""
        description_lines: List[str] = []

        if len(lines) >= 2:
            if looks_like_date_range(lines[1]):
                start_date, end_date = extract_date_range(lines[1])
            else:
                degree = lines[1]

        if len(lines) >= 3:
            if not degree and not looks_like_date_range(lines[2]):
                degree = lines[2]
                if len(lines) >= 4 and looks_like_date_range(lines[3]):
                    start_date, end_date = extract_date_range(lines[3])
                    description_lines = lines[4:]
                else:
                    description_lines = lines[3:]
            else:
                if not start_date and looks_like_date_range(lines[2]):
                    start_date, end_date = extract_date_range(lines[2])
                    description_lines = lines[3:]
                else:
                    description_lines = lines[2:]

        results.append({
            "Institution": institution,
            "Degree": degree,
            "StartDate": start_date,
            "EndDate": end_date,
            "Description": " ".join(description_lines).strip()
        })

    return results


# =========================================================
# MAIN EXTRACTION
# =========================================================
def extract_cv_data(raw_text: str) -> Dict[str, Any]:
    lines = get_lines(raw_text)

    full_name = guess_name_from_top(lines)
    job_title = guess_job_title_from_top(lines, full_name)
    location = guess_location(lines)

    sections = extract_section_texts(lines)

    result: Dict[str, Any] = {
        "FullName": full_name,
        "JobTitle": job_title,
        "Location": location,
        "Email": extract_email(raw_text),
        "Phone": extract_phone(raw_text),
        "Linkedin": extract_linkedin(raw_text),
        "Github": extract_github(raw_text),
        "Summary": sections.get("summary", ""),
        "Skills": parse_skills(sections.get("skills", "")),
        "Languages": parse_languages(sections.get("languages", "")),
        "Education": parse_education(sections.get("education", "")),
        "WorkExperience": parse_work_experience(sections.get("work_experience", "")),
        "Projects": parse_simple_entries(sections.get("projects", ""), "Name"),
        "Certifications": parse_simple_entries(sections.get("certifications", ""), "Name"),
        "Achievements": parse_simple_entries(sections.get("achievements", ""), "Name"),
        "RawExtractedText": raw_text
    }

    return result


# =========================================================
# ENTRY POINT
# =========================================================
def main() -> None:
    if len(sys.argv) < 2:
        print(json.dumps({"Error": "Usage: python cv_extract.py <path_to_cv>"}))
        sys.exit(1)

    file_path = Path(sys.argv[1])

    if not file_path.exists():
        print(json.dumps({"Error": f"File not found: {file_path}"}))
        sys.exit(1)

    try:
        raw_text = read_cv(file_path)
        raw_text = clean_text(raw_text)

        if not raw_text:
            print(json.dumps({"Error": "No readable text was extracted from the CV."}))
            sys.exit(1)

        final_results = extract_cv_data(raw_text)
        print(json.dumps(final_results, ensure_ascii=False))
    except Exception as ex:
        print(json.dumps({"Error": str(ex)}))
        sys.exit(1)


if __name__ == "__main__":
    main()