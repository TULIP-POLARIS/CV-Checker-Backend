import json
import re
import sys
from pathlib import Path
from typing import Dict, List, Optional

# Add local packaged dependencies path first
extra_paths = [
    r"C:\home\site\wwwroot\PythonPackages"
]

for p in extra_paths:
    if p and os.path.isdir(p) and p not in sys.path:
        sys.path.insert(0, p)

from PyPDF2 import PdfReader
from docx import Document


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


def clean_text(text: str) -> str:
    text = text.replace("\xa0", " ")
    text = re.sub(r"[ \t]+", " ", text)
    text = re.sub(r"\r\n", "\n", text)
    text = re.sub(r"\n{3,}", "\n\n", text)
    return text.strip()


def get_lines(text: str) -> List[str]:
    return [line.strip() for line in text.splitlines() if line.strip()]


def extract_email(text: str) -> str:
    match = re.search(r"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", text)
    return match.group(0) if match else "Not found"


def extract_phone(text: str) -> str:
    patterns = [
        r"(\+\d{1,3}[\s\-]?\(?\d+\)?(?:[\s\-]?\d+){5,})",
        r"(\(?\d{2,4}\)?[\s\-]?\d{3,4}[\s\-]?\d{3,4})",
    ]

    for pattern in patterns:
        match = re.search(pattern, text)
        if match:
            return match.group(0).strip()

    return "Not found"


def extract_linkedin(text: str) -> str:
    match = re.search(r"(https?://)?(www\.)?linkedin\.com/in/[A-Za-z0-9\-_/]+", text, re.IGNORECASE)
    return match.group(0) if match else "Not found"


def extract_github(text: str) -> str:
    match = re.search(r"(https?://)?(www\.)?github\.com/[A-Za-z0-9\-_/]+", text, re.IGNORECASE)
    return match.group(0) if match else "Not found"


def guess_name_from_top(text: str) -> str:
    lines = get_lines(text)[:6]

    for line in lines:
        words = line.split()
        if 2 <= len(words) <= 4 and re.fullmatch(r"[A-Za-zÀ-ÿ' -]+", line):
            lowered = line.lower()
            blocked = [
                "curriculum",
                "resume",
                "cv",
                "profile",
                "about",
                "summary",
                "experience",
                "education",
                "skills",
            ]
            if not any(b in lowered for b in blocked):
                return line

    return "Not found"


def guess_job_title_from_top(text: str, full_name: str) -> str:
    lines = get_lines(text)[:8]

    for line in lines:
        if line == full_name:
            continue

        lowered = line.lower()
        if len(line) > 60:
            continue

        blocked = [
            "@", "linkedin.com", "github.com", "education", "experience", "skills",
            "projects", "certifications", "languages", "summary"
        ]
        if any(b in lowered for b in blocked):
            continue

        if re.search(r"\d", line):
            continue

        if 2 <= len(line.split()) <= 8:
            return line

    return "Not found"


def guess_location(text: str) -> str:
    lines = get_lines(text)[:10]

    for line in lines:
        lowered = line.lower()

        if "linkedin.com" in lowered or "github.com" in lowered or "@" in line:
            continue

        if re.search(r"\+\d", line):
            continue

        # simple "City, Country" or short location line
        if len(line) <= 50 and ("," in line or 1 <= len(line.split()) <= 4):
            if re.fullmatch(r"[A-Za-zÀ-ÿ0-9,.\- ]+", line):
                return line

    return "Not found"


def find_section(lines: List[str], possible_titles: List[str]) -> str:
    normalized_titles = [t.lower() for t in possible_titles]

    start_index = -1
    for i, line in enumerate(lines):
        line_norm = line.strip().lower().rstrip(":")
        if line_norm in normalized_titles:
            start_index = i + 1
            break

    if start_index == -1:
        return "Not found"

    collected = []
    for j in range(start_index, len(lines)):
        current = lines[j].strip()
        current_norm = current.lower().rstrip(":")

        if j > start_index:
            if is_section_heading(current_norm):
                break

        collected.append(current)

    result = " ".join(collected).strip()
    return result if result else "Not found"


def is_section_heading(line: str) -> bool:
    known_headings = [
        "profile",
        "professional summary",
        "summary",
        "about me",
        "about",
        "skills",
        "technical skills",
        "core competencies",
        "work experience",
        "experience",
        "professional experience",
        "employment history",
        "education",
        "academic background",
        "certifications",
        "licenses",
        "projects",
        "languages",
        "achievements",
        "awards",
        "work authorization",
        "availability",
        "contact",
    ]

    return line in known_headings


def extract_sections(text: str) -> Dict[str, str]:
    lines = get_lines(text)

    return {
        "professional_summary": find_section(lines, ["professional summary", "summary", "about me", "profile"]),
        "skills": find_section(lines, ["skills", "technical skills", "core competencies"]),
        "work_experience": find_section(lines, ["work experience", "experience", "professional experience", "employment history"]),
        "education": find_section(lines, ["education", "academic background"]),
        "certifications": find_section(lines, ["certifications", "licenses"]),
        "projects": find_section(lines, ["projects"]),
        "languages": find_section(lines, ["languages"]),
        "achievements": find_section(lines, ["achievements", "awards"]),
        "work_authorization": find_section(lines, ["work authorization"]),
        "availability": find_section(lines, ["availability"]),
    }


def to_pascal_case_dict(data: Dict[str, str]) -> Dict[str, str]:
    mapping = {
        "full_name": "FullName",
        "job_title": "JobTitle",
        "location": "Location",
        "professional_summary": "ProfessionalSummary",
        "skills": "Skills",
        "work_experience": "WorkExperience",
        "education": "Education",
        "certifications": "Certifications",
        "projects": "Projects",
        "languages": "Languages",
        "achievements": "Achievements",
        "work_authorization": "WorkAuthorization",
        "availability": "Availability",
        "email": "Email",
        "phone": "Phone",
        "linkedin": "Linkedin",
        "github": "Github",
    }

    return {mapping.get(k, k): v for k, v in data.items()}


def extract_cv_data(raw_text: str) -> Dict[str, str]:
    full_name = guess_name_from_top(raw_text)
    sections = extract_sections(raw_text)

    result = {
        "full_name": full_name,
        "job_title": guess_job_title_from_top(raw_text, full_name),
        "location": guess_location(raw_text),
        "professional_summary": sections.get("professional_summary", "Not found"),
        "skills": sections.get("skills", "Not found"),
        "work_experience": sections.get("work_experience", "Not found"),
        "education": sections.get("education", "Not found"),
        "certifications": sections.get("certifications", "Not found"),
        "projects": sections.get("projects", "Not found"),
        "languages": sections.get("languages", "Not found"),
        "achievements": sections.get("achievements", "Not found"),
        "work_authorization": sections.get("work_authorization", "Not found"),
        "availability": sections.get("availability", "Not found"),
        "email": extract_email(raw_text),
        "phone": extract_phone(raw_text),
        "linkedin": extract_linkedin(raw_text),
        "github": extract_github(raw_text),
    }

    return to_pascal_case_dict(result)


def main() -> None:
    if len(sys.argv) < 2:
        print(json.dumps({"Error": "Usage: python cv_extract.py <path_to_cv>"}))
        sys.exit(1)

    file_path = Path(sys.argv[1])

    if not file_path.exists():
        print(json.dumps({"Error": "File not found: {0}".format(file_path)}))
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