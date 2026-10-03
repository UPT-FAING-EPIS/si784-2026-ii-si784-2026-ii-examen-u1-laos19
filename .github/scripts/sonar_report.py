#!/usr/bin/env python3
"""Genera el reporte del análisis de SonarQube (JSON, Markdown y HTML) y aplica la política:
0 bugs, 0 vulnerabilidades y 0 security hotspots pendientes.

Variables de entorno: SONAR_HOST_URL, SONAR_PROJECT_KEY y SONAR_ADMIN_PASSWORD (o SONAR_TOKEN).
Uso: python3 sonar_report.py <carpeta-salida>
"""
import base64
import html
import http.client
import json
import os
import sys
import urllib.parse

HOST = os.environ.get("SONAR_HOST_URL", "http://localhost:9000").rstrip("/")
PROJECT = os.environ.get("SONAR_PROJECT_KEY", "musicstream")
if os.environ.get("SONAR_ADMIN_PASSWORD"):
    CREDENTIALS = "admin:" + os.environ["SONAR_ADMIN_PASSWORD"]
else:
    CREDENTIALS = os.environ.get("SONAR_TOKEN", "") + ":"
AUTH = "Basic " + base64.b64encode(CREDENTIALS.encode()).decode()

METRICS = [
    ("bugs", "Bugs"),
    ("vulnerabilities", "Vulnerabilidades"),
    ("security_hotspots", "Security hotspots"),
    ("code_smells", "Code smells"),
    ("coverage", "Cobertura (%)"),
    ("duplicated_lines_density", "Duplicación (%)"),
    ("ncloc", "Líneas de código"),
    ("reliability_rating", "Rating de confiabilidad"),
    ("security_rating", "Rating de seguridad"),
    ("sqale_rating", "Rating de mantenibilidad"),
]
RATINGS = {"1.0": "A", "2.0": "B", "3.0": "C", "4.0": "D", "5.0": "E"}


def get(path, **params):
    target = urllib.parse.urlsplit(HOST)
    connection_class = http.client.HTTPSConnection if target.scheme == "https" else http.client.HTTPConnection
    connection = connection_class(target.hostname, target.port, timeout=60)
    try:
        connection.request("GET", f"{path}?{urllib.parse.urlencode(params)}", headers={"Authorization": AUTH})
        response = connection.getresponse()
        if response.status != 200:
            raise RuntimeError(f"{path}: HTTP {response.status}")
        return json.load(response)
    finally:
        connection.close()


def main(output_dir):
    os.makedirs(output_dir, exist_ok=True)
    measures = {
        m["metric"]: m.get("value", "0")
        for m in get("/api/measures/component", component=PROJECT, metricKeys=",".join(k for k, _ in METRICS))["component"]["measures"]
    }
    gate = get("/api/qualitygates/project_status", projectKey=PROJECT)["projectStatus"]["status"]
    issues = get("/api/issues/search", componentKeys=PROJECT, types="BUG,VULNERABILITY", resolved="false", ps=500)["issues"]
    hotspots = get("/api/hotspots/search", project=PROJECT, status="TO_REVIEW", ps=500)["hotspots"]
    smells = get("/api/issues/search", componentKeys=PROJECT, types="CODE_SMELL", resolved="false", ps=500)["issues"]

    report = {
        "project": PROJECT,
        "qualityGate": gate,
        "measures": measures,
        "issues": issues,
        "hotspots": hotspots,
        "codeSmells": smells,
    }
    with open(os.path.join(output_dir, "sonarqube-report.json"), "w", encoding="utf-8") as fh:
        json.dump(report, fh, indent=2, ensure_ascii=False)

    def value(key):
        v = measures.get(key, "0")
        return RATINGS.get(v, v) if key.endswith("_rating") else v

    def location(item):
        return f"{item['component'].split(':', 1)[-1]}:{item.get('line', '-')}"

    md = [f"## SonarQube — {PROJECT}", "", f"**Quality Gate:** {'✅' if gate == 'OK' else '❌'} {gate}", "",
          "| Métrica | Valor |", "|---|---|"]
    md += [f"| {label} | {value(key)} |" for key, label in METRICS]
    md += ["", f"### Bugs y vulnerabilidades ({len(issues)})", ""]
    md += [f"- `{i['type']}` {location(i)} — {i['message']} (`{i['rule']}`)" for i in issues] or ["Ninguno ✅"]
    md += ["", f"### Security hotspots por revisar ({len(hotspots)})", ""]
    md += [f"- {location(h)} — {h['message']} (`{h['ruleKey']}`)" for h in hotspots] or ["Ninguno ✅"]
    md += ["", f"### Code smells ({len(smells)})", ""]
    md += [f"- `{s['severity']}` {location(s)} — {s['message']}" for s in smells[:100]] or ["Ninguno ✅"]
    markdown = "\n".join(md) + "\n"
    with open(os.path.join(output_dir, "sonarqube-report.md"), "w", encoding="utf-8") as fh:
        fh.write(markdown)

    rows = "".join(f"<tr><td>{html.escape(label)}</td><td>{html.escape(str(value(key)))}</td></tr>" for key, label in METRICS)

    def items(entries, fmt):
        return "".join(f"<li>{html.escape(fmt(e))}</li>" for e in entries) or "<li>Ninguno ✅</li>"

    page = f"""<!doctype html><html lang="es"><head><meta charset="utf-8"><title>SonarQube - {html.escape(PROJECT)}</title>
<style>body{{font-family:system-ui,sans-serif;max-width:960px;margin:2rem auto;padding:0 1rem}}table{{border-collapse:collapse}}td,th{{border:1px solid #ccc;padding:.4rem .8rem}}.ok{{color:#167c3a}}.ko{{color:#b42318}}</style></head>
<body><h1>Reporte SonarQube — {html.escape(PROJECT)}</h1>
<h2 class="{'ok' if gate == 'OK' else 'ko'}">Quality Gate: {html.escape(gate)}</h2>
<table><tr><th>Métrica</th><th>Valor</th></tr>{rows}</table>
<h2>Bugs y vulnerabilidades ({len(issues)})</h2><ul>{items(issues, lambda i: f"{i['type']} {location(i)} - {i['message']}")}</ul>
<h2>Security hotspots ({len(hotspots)})</h2><ul>{items(hotspots, lambda h: f"{location(h)} - {h['message']}")}</ul>
<h2>Code smells ({len(smells)})</h2><ul>{items(smells[:100], lambda s: f"{s['severity']} {location(s)} - {s['message']}")}</ul>
</body></html>"""
    with open(os.path.join(output_dir, "sonarqube-report.html"), "w", encoding="utf-8") as fh:
        fh.write(page)

    summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary:
        with open(summary, "a", encoding="utf-8") as fh:
            fh.write(markdown)
    print(markdown)

    failures = len(issues) + len(hotspots)
    if gate != "OK" or failures:
        print(f"Política incumplida: quality gate={gate}, bugs/vulnerabilidades={len(issues)}, hotspots={len(hotspots)}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1] if len(sys.argv) > 1 else "sonar-report"))
