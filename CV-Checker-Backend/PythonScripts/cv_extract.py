import json
import re
import sys
from pathlib import Path
from typing import Dict, List, Tuple

from pypdf import PdfReader
from docx import Document
from sentence_transformers import SentenceTransformer, util


def read_txt(file_path: Path) -> str:
    return file_path.read_text(encoding="utf-8", errors="ignore")


def read_pdf(file_path: Path) -> str:
    reader = PdfReader(str(file_path))
    pages = []
    for page in reader.pages:
        text = page.extract_text() or ""
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
    text = re.sub(r"\n{2,}", "\n\n", text)
    return text.strip()


def split_into_chunks(text: str, max_words: int = 120, overlap: int = 25) -> List[str]:
    paragraphs = [p.strip() for p in text.split("\n") if p.strip()]
    chunks: List[str] = []
    current_words: List[str] = []

    for para in paragraphs:
        para_words = para.split()

        if len(current_words) + len(para_words) <= max_words:
            current_words.extend(para_words)
        else:
            if current_words:
                chunks.append(" ".join(current_words))
                current_words = current_words[-overlap:] if overlap < len(current_words) else current_words

            current_words.extend(para_words)

            while len(current_words) > max_words:
                chunks.append(" ".join(current_words[:max_words]))
                current_words = current_words[max_words - overlap:]

    if current_words:
        chunks.append(" ".join(current_words))

    seen = set()
    final_chunks = []

    for chunk in chunks:
        normalized = chunk.strip().lower()
        if len(normalized) > 25 and normalized not in seen:
            seen.add(normalized)
            final_chunks.append(chunk)

    return final_chunks


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


def semantic_extract(
    model: SentenceTransformer,
    chunks: List[str],
    keyword_queries: Dict[str, List[str]],
    score_threshold: float = 0.35,
    top_k: int = 3,
) -> Dict[str, str]:
    results: Dict[str, str] = {}

    if not chunks:
        return {key: "Not found" for key in keyword_queries}

    chunk_embeddings = model.encode(chunks, convert_to_tensor=True, normalize_embeddings=True)

    for keyword, queries in keyword_queries.items():
        best_matches: List[Tuple[float, str]] = []

        for query in queries:
            query_embedding = model.encode(query, convert_to_tensor=True, normalize_embeddings=True)
            scores = util.cos_sim(query_embedding, chunk_embeddings)[0]

            top_results = scores.topk(k=min(top_k, len(chunks)))
            for score, idx in zip(top_results.values.tolist(), top_results.indices.tolist()):
                if score >= score_threshold:
                    best_matches.append((float(score), chunks[idx]))

        if not best_matches:
            results[keyword] = "Not found"
            continue

        best_matches.sort(key=lambda x: x[0], reverse=True)
        selected_texts = []
        seen = set()

        for _, text in best_matches:
            norm = text.strip().lower()
            if norm not in seen:
                seen.add(norm)
                selected_texts.append(text)
            if len(selected_texts) == 2:
                break

        results[keyword] = " | ".join(selected_texts)

    return results


def get_keyword_queries() -> Dict[str, List[str]]:
    return {
        "full_name": [
            "candidate full name",
            "name of the applicant",
            "person name at top of cv"
        ],
        "job_title": [
            "current job title",
            "target role or professional title",
            "headline under the name"
        ],
        "location": [
            "candidate location",
            "city country address",
            "where the applicant is based"
        ],
        "professional_summary": [
            "professional summary profile",
            "about me summary",
            "career overview"
        ],
        "skills": [
            "technical skills and tools",
            "core competencies",
            "skills section"
        ],
        "work_experience": [
            "work experience employment history",
            "professional experience",
            "past roles and responsibilities"
        ],
        "education": [
            "education academic background",
            "degree university college",
            "studies qualifications"
        ],
        "certifications": [
            "certifications licenses training",
            "professional certificates",
            "accreditations"
        ],
        "projects": [
            "projects portfolio achievements",
            "relevant projects",
            "case studies or implementations"
        ],
        "languages": [
            "languages spoken",
            "language proficiency",
            "multilingual skills"
        ],
        "achievements": [
            "awards accomplishments achievements",
            "notable results",
            "business impact and outcomes"
        ],
        "work_authorization": [
            "work permit visa authorization",
            "right to work",
            "employment eligibility"
        ],
        "availability": [
            "notice period availability start date",
            "when can candidate start",
            "available from"
        ],
    }


def guess_name_from_top(text: str) -> str:
    lines = [line.strip() for line in text.splitlines() if line.strip()]
    if not lines:
        return "Not found"

    first = lines[0]
    if 2 <= len(first.split()) <= 4 and re.fullmatch(r"[A-Za-z?-?' -]+", first):
        return first

    return "Not found"


def postprocess_results(raw_text: str, results: Dict[str, str]) -> Dict[str, str]:
    regex_fields = {
        "email": extract_email(raw_text),
        "phone": extract_phone(raw_text),
        "linkedin": extract_linkedin(raw_text),
        "github": extract_github(raw_text),
    }

    final_results = dict(results)
    final_results.update(regex_fields)

    if final_results.get("full_name", "Not found") == "Not found":
        guessed = guess_name_from_top(raw_text)
        if guessed != "Not found":
            final_results["full_name"] = guessed

    return final_results


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

        chunks = split_into_chunks(raw_text, max_words=120, overlap=25)

        model = SentenceTransformer("sentence-transformers/all-MiniLM-L6-v2")
        keyword_queries = get_keyword_queries()

        semantic_results = semantic_extract(
            model=model,
            chunks=chunks,
            keyword_queries=keyword_queries,
            score_threshold=0.35,
            top_k=3,
        )

        final_results = postprocess_results(raw_text, semantic_results)
        final_results = to_pascal_case_dict(final_results)

        print(json.dumps(final_results, ensure_ascii=False))
    except Exception as ex:
        print(json.dumps({"Error": str(ex)}))
        sys.exit(1)


if __name__ == "__main__":
    main()