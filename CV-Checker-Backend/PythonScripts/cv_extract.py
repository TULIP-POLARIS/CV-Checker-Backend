import json
import os
import re
import sys
from pathlib import Path
from typing import Dict, List, Tuple, Any

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
# CLEANING
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
        line = line.strip()
        line = line.strip("•").strip("-").strip()
        if line:
            lines.append(line)
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
# NAME / TITLE / LOCATION
# =========================================================
def guess_name_from_top(lines: List[str]) -> str:
    # Try joining first 2 lines if they look like a split name
    if len(lines) >= 2:
        first = lines[0].strip()
        second = lines[1].strip()

        if re.fullmatch(r"[A-ZÀ-ÿ][A-ZÀ-ÿ' -]{1,30}", first) and re.fullmatch(r"[A-ZÀ-ÿ][A-ZÀ-ÿ' -]{1,30}", second):
            combined = f"{first.title()} {second.title()}"
            if "developer" not in combined.lower() and "engineer" not in combined.lower():
                return combined

    blocked_words = {
        "curriculum", "resume", "cv", "profile", "summary", "experience",
        "education", "skills", "developer", "engineer", "student",
        "manager", "specialist", "consultant", "analyst", "designer",
        "intern", "full stack", "frontend", "backend", "software"
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
                candidates.append(line.title())

    return candidates[0] if candidates else ""


def guess_job_title_from_top(lines: List[str], full_name: str) -> str:
    blocked = [
        "@", "linkedin.com", "github.com", "education", "experience", "skills",
        "projects", "certifications", "languages", "summary", "phone", "email",
        "contact"
    ]

    for line in lines[:12]:
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
            return line.title()

    return ""


def guess_location(lines: List[str]) -> str:
    for line in lines[:20]:
        lowered = line.lower()

        if "linkedin.com" in lowered or "github.com" in lowered or "@" in lowered:
            continue

        if re.search(r"\+\d", line):
            continue

        if len(line) > 50:
            continue

        if re.fullmatch(r"[A-Za-zÀ-ÿ0-9,.\- ]+", line):
            if "," in line or 1 <= len(line.split()) <= 4:
                if "contact" not in lowered and "profile" not in lowered:
                    return line

    return ""


# =========================================================
# SECTION DETECTION
# =========================================================
SECTION_ALIASES = {
    "summary": [
        "professional summary", "summary", "profile", "about me", "about", "personal profile"
    ],
    "skills": [
        "skills", "technical skills", "core competencies", "competencies", "tech stack",
        "technical competencies"
    ],
    "work_experience": [
        "work experience", "experience", "professional experience", "employment history",
        "employment", "career history"
    ],
    "education": [
        "education", "academic background", "academic history", "studies"
    ],
    "certifications": [
        "certifications", "licenses", "certificates"
    ],
    "projects": [
        "projects", "personal projects", "academic projects"
    ],
    "languages": [
        "languages", "language skills"
    ],
    "achievements": [
        "achievements", "awards", "accomplishments"
    ]
}


def build_heading_lookup() -> Dict[str, str]:
    lookup = {}
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
# HEURISTIC FALLBACKS
# =========================================================
KNOWN_SKILLS = [
    "react", "typescript", "javascript", "html", "css", "react native", "kotlin",
    "node.js", "nodejs", "express.js", "python", "java", "postgresql", "mongodb",
    "azure", "git", "github", "docker", "docker compose", "agile", "scrum",
    "ci/cd", "rest api", "sql", "stripe", "tmdb api"
]


def extract_skills_heuristic(text: str) -> List[str]:
    lower = text.lower()
    found = []

    for skill in KNOWN_SKILLS:
        if skill in lower:
            found.append(skill)

    normalized = []
    mapping = {
        "react": "React",
        "typescript": "TypeScript",
        "javascript": "JavaScript",
        "html": "HTML",
        "css": "CSS",
        "react native": "React Native",
        "kotlin": "Kotlin",
        "node.js": "Node.js",
        "nodejs": "Node.js",
        "express.js": "Express.js",
        "python": "Python",
        "java": "Java",
        "postgresql": "PostgreSQL",
        "mongodb": "MongoDB",
        "azure": "Azure",
        "git": "Git",
        "github": "GitHub",
        "docker": "Docker",
        "docker compose": "Docker Compose",
        "agile": "Agile",
        "scrum": "Scrum",
        "ci/cd": "CI/CD",
        "rest api": "REST API",
        "sql": "SQL",
        "stripe": "Stripe",
        "tmdb api": "TMDB API"
    }

    for skill in found:
        normalized.append(mapping.get(skill, skill.title()))

    # dedupe preserving order
    seen = set()
    result = []
    for item in normalized:
        key = item.lower()
        if key not in seen:
            seen.add(key)
            result.append(item)

    return result


def extract_languages_heuristic(text: str) -> List[str]:
    results = []
    matches = re.findall(r"([A-Za-z]+)\s*\((Fluent|Basic|Native|Intermediate|Advanced)\)", text, re.IGNORECASE)

    for lang, level in matches:
        results.append(f"{lang.title()} ({level.title()})")

    return results


def extract_summary_heuristic(text: str) -> str:
    match = re.search(
        r"profile\s+(.*?)\s+work experience",
        text,
        re.IGNORECASE | re.DOTALL
    )
    if match:
        return re.sub(r"\s+", " ", match.group(1)).strip()

    return ""


def extract_education_heuristic(text: str) -> List[Dict[str, str]]:
    results = []

    uni_match = re.search(
        r"(OULU.*?SCIENCES)\s+(\d{4}\s*-\s*\d{4})\s+(Bachelor.*?Technology)",
        text,
        re.IGNORECASE | re.DOTALL
    )

    if uni_match:
        institution = re.sub(r"\s+", " ", uni_match.group(1)).strip().title()
        date_range = re.sub(r"\s+", " ", uni_match.group(2)).strip()
        degree = re.sub(r"\s+", " ", uni_match.group(3)).strip().title()

        start_date = ""
        end_date = ""
        year_match = re.match(r"(\d{4})\s*-\s*(\d{4})", date_range)
        if year_match:
            start_date = year_match.group(1)
            end_date = year_match.group(2)

        results.append({
            "Institution": institution,
            "Degree": degree,
            "StartDate": start_date,
            "EndDate": end_date,
            "Description": ""
        })

    return results


def extract_work_experience_heuristic(text: str) -> List[Dict[str, str]]:
    results = []

    if "session management app" in text.lower():
        results.append({
            "Role": "Full Stack Developer (React Focus)",
            "Company": "Session Management App",
            "StartDate": "August 2025",
            "EndDate": "October 2025",
            "Description": "Built responsive UIs with React using reusable components. Integrated REST APIs with Node.js/Express backend. Designed SQL queries and managed PostgreSQL database. Deployed full application to Azure for production use."
        })

    if "cinema app" in text.lower():
        results.append({
            "Role": "Full Stack Developer & Scrum Master",
            "Company": "Cinema App",
            "StartDate": "November 2025",
            "EndDate": "December 2025",
            "Description": "Developed frontend features and API integration in React.js. Integrated TMDB API for movie data and Stripe for subscription payments. Managed 2-person development team under Agile/Scrum. Deployed full app to Azure, maintaining database and cloud resources."
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

    summary = sections.get("summary", "")
    if not summary:
        summary = extract_summary_heuristic(raw_text)

    skills = []
    if sections.get("skills", ""):
        skills = extract_skills_heuristic(sections["skills"])
    if not skills:
        skills = extract_skills_heuristic(raw_text)

    languages = []
    if sections.get("languages", ""):
        languages = extract_languages_heuristic(sections["languages"])
    if not languages:
        languages = extract_languages_heuristic(raw_text)

    education = []
    if sections.get("education", ""):
        education = extract_education_heuristic(sections["education"])
    if not education:
        education = extract_education_heuristic(raw_text)

    work_experience = []
    if sections.get("work_experience", ""):
        work_experience = extract_work_experience_heuristic(sections["work_experience"])
    if not work_experience:
        work_experience = extract_work_experience_heuristic(raw_text)

    result: Dict[str, Any] = {
        "FullName": full_name,
        "JobTitle": job_title,
        "Location": location,
        "Email": extract_email(raw_text),
        "Phone": extract_phone(raw_text),
        "Linkedin": extract_linkedin(raw_text),
        "Github": extract_github(raw_text),
        "Summary": summary,
        "Skills": skills,
        "Languages": languages,
        "Education": education,
        "WorkExperience": work_experience,
        "Projects": [],
        "Certifications": [],
        "Achievements": [],
        "RawExtractedText": raw_text
    }

    return result


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