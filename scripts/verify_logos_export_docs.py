"""Probe public Logos documentation; publish metadata and short excerpts only."""
import concurrent.futures
import datetime
import html
from html.parser import HTMLParser
import json
import os
from pathlib import Path
import re
import sys
import urllib.parse
import urllib.request


class ArticleText(HTMLParser):
    def __init__(self):
        super().__init__()
        self.parts = []

    def handle_starttag(self, tag, attrs):
        if tag in {"p", "br", "li", "h1", "h2", "h3", "h4", "tr"}:
            self.parts.append("\n")

    def handle_endtag(self, tag):
        if tag in {"p", "li", "h1", "h2", "h3", "h4", "tr"}:
            self.parts.append("\n")

    def handle_data(self, data):
        self.parts.append(data)


def fetch(url):
    request = urllib.request.Request(url, headers={"User-Agent": "Bible2PPT-doc-verification/1.0"})
    with urllib.request.urlopen(request, timeout=20) as response:
        return response.geturl(), response.status, response.read(6_000_000).decode("utf-8", errors="replace")


def excerpts(body):
    parser = ArticleText()
    parser.feed(body)
    lines = [re.sub(r"\s+", " ", line).strip() for line in "".join(parser.parts).splitlines()]
    patterns = [r"limit|maximum|100.{0,15}page|entire|whole", r"text document|plain text|save as|export", r"copy bible verses|style|range"]
    selected, remaining = [], 80
    seen = set()
    for pattern in patterns:
        for line in lines:
            if line in seen or not re.search(pattern, line, re.I):
                continue
            seen.add(line)
            words = line.split()
            take = min(len(words), remaining, 30)
            if take:
                selected.append(" ".join(words[:take]) + (" …" if take < len(words) else ""))
                remaining -= take
            if remaining == 0:
                return selected
    return selected


def search(query):
    url = "https://support.logos.com/api/v2/help_center/en-us/articles/search.json?" + urllib.parse.urlencode({"query": query, "per_page": 20})
    try:
        final, status, payload = fetch(url)
        data = json.loads(payload)
        articles = []
        for article in data.get("results", data.get("articles", [])):
            title = article.get("title", "")
            if re.search(r"print|export|copy bible|clipboard", title, re.I):
                articles.append({"title": title, "url": article.get("html_url", article.get("url")), "excerpt": excerpts(article.get("body", ""))})
        return {"query": query, "request_url": url, "final_url": final, "status": status, "articles": articles}
    except Exception as error:
        return {"query": query, "request_url": url, "error": f"{type(error).__name__}: {error}"}


def wiki(url):
    try:
        final, status, page = fetch(url)
        match = re.search(r"<title[^>]*>(.*?)</title>", page, re.I | re.S)
        # Extract article content rather than quoting navigation or JavaScript.
        article = re.search(r"<article\b[^>]*>(.*?)</article>", page, re.I | re.S)
        if article is None:
            article = re.search(r'<div[^>]*class="[^"]*mw-parser-output[^>]*>(.*?)(?:<div[^>]*class="printfooter|<!--)', page, re.I | re.S)
        return {"request_url": url, "final_url": final, "status": status,
                "title": html.unescape(match.group(1)) if match else "",
                "excerpt": excerpts(article.group(1)) if article else [],
                "article_extracted": article is not None}
    except Exception as error:
        return {"request_url": url, "error": f"{type(error).__name__}: {error}"}


def main():
    output = Path(sys.argv[1])
    output.mkdir(parents=True, exist_ok=True)
    (output / "README.md").write_text("# Logos export documentation verification\n\nProbe not completed. No verified conclusion.\n")
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        searches = list(pool.map(search, ["Print Export", "Copy Bible Verses", "export limit", "copy limit"]))
        pages = list(pool.map(wiki, ["https://wiki.logos.com/Print$2fExport", "https://wiki.logos.com/Copy_Bible_Verses"]))
    report = {"checked_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
              "source_commit": os.environ.get("GITHUB_SHA", "local"),
              "runner_os": os.environ.get("RUNNER_OS", "local"),
              "run_url": "https://github.com/" + os.environ.get("GITHUB_REPOSITORY", "") + "/actions/runs/" + os.environ.get("GITHUB_RUN_ID", ""),
              "mac_application_execution": "NOT TESTED", "searches": searches, "pages": pages}
    (output / "report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    lines = ["# Logos export documentation verification", "", "Checked UTC: " + report["checked_utc"],
             "", "Run: " + report["run_url"], "", "Mac application execution: **NOT TESTED**", "",
             "This report records public documentation responses; it does not execute Logos or access an account.", ""]
    seen = set()
    for result in searches:
        lines += ["## Search: " + result["query"], "", result.get("error", "HTTP " + str(result.get("status"))), ""]
        for item in result.get("articles", []):
            if item["url"] in seen:
                continue
            seen.add(item["url"])
            lines += ["### " + item["title"], "", str(item["url"]), ""]
            lines += ["> " + excerpt for excerpt in item["excerpt"]] + [""]
    for page in pages:
        lines += ["## Wiki probe", "", page["request_url"], "", page.get("error", "HTTP " + str(page.get("status")) + "; final URL: " + page.get("final_url", "")), ""]
        if page.get("final_url") not in seen:
            lines += ["> " + excerpt for excerpt in page.get("excerpt", [])] + [""]
    (output / "README.md").write_text("\n".join(lines), encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
