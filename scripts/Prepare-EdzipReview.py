"""Build a local, explicitly unapproved review pack. No upload, signature or school approval."""
import argparse
import hashlib
import html
import json
import re
import shutil
from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle

ROOT = Path(__file__).resolve().parent.parent
parser = argparse.ArgumentParser()
parser.add_argument("--name", required=True)
parser.add_argument("--email", required=True)
parser.add_argument("--organization", required=True)
parser.add_argument("--position", required=True)
args = parser.parse_args()
pack = ROOT / "03_에듀집_등록준비"
drafts = pack / "02_작성자료_학교확정전"
evidence = pack / "03_검증요약"
pdfout = ROOT / "output/pdf"
for directory in (drafts, evidence, pdfout):
    directory.mkdir(parents=True, exist_ok=True)
pdfmetrics.registerFont(TTFont("Korean", "C:/Windows/Fonts/malgun.ttf"))
pdfmetrics.registerFont(TTFont("KoreanBold", "C:/Windows/Fonts/malgunbd.ttf"))
styles = {
    "title": ParagraphStyle("title", fontName="KoreanBold", fontSize=18, leading=25, spaceAfter=14, wordWrap="CJK"),
    "h2": ParagraphStyle("h2", fontName="KoreanBold", fontSize=11.5, leading=18, spaceBefore=12, spaceAfter=6, keepWithNext=True, wordWrap="CJK"),
    "body": ParagraphStyle("body", fontName="Korean", fontSize=9, leading=15, spaceAfter=8, wordWrap="CJK", alignment=TA_LEFT),
    "small": ParagraphStyle("small", fontName="Korean", fontSize=7.3, leading=11, spaceAfter=5, wordWrap="CJK"),
}


def para(text, style="body"):
    value = html.escape(text).replace("`", "")
    value = re.sub(r"\*\*(.*?)\*\*", r"<b>\1</b>", value)
    value = re.sub(r"\[([^\]]+)\]\((https?://[^)]+)\)", r'<link href="\2" color="#2456a6">\1</link>', value)
    return Paragraph(value, styles[style])


def footer(canvas, doc):
    canvas.setFont("Korean", 8)
    canvas.setFillColor(colors.HexColor("#5b6474"))
    canvas.drawString(17 * mm, 12 * mm, "검토용 초안 | 1.0.0 출시 전 | 학교 확정·서명·등록 전")
    canvas.drawRightString(193 * mm, 12 * mm, str(doc.page))


def build(name, flow):
    path = pdfout / name
    SimpleDocTemplate(str(path), pagesize=(210 * mm, 297 * mm), leftMargin=17 * mm, rightMargin=17 * mm,
                      topMargin=17 * mm, bottomMargin=20 * mm, title=name.removesuffix(".pdf"),
                      author=args.name).build(flow, onFirstPage=footer, onLaterPages=footer)
    shutil.copy2(path, drafts / name)


def markdown_pdf(source, output):
    text = (ROOT / "docs" / source).read_text(encoding="utf-8-sig")
    flow = []
    table_header = None
    for line in text.splitlines():
        if not line.strip() or line.startswith("```"):
            continue
        if line.startswith("# "):
            flow.append(para(line[2:], "title"))
        elif line.startswith("## "):
            flow.append(para(line[3:], "h2"))
        elif line.startswith("|"):
            cells = [c.strip() for c in line.strip("|").split("|")]
            if all(re.fullmatch(r"[- :]+", c or "-") for c in cells):
                continue
            if table_header is None:
                table_header = cells
            else:
                flow.append(para(" / ".join(f"{k}: {v}" for k, v in zip(table_header, cells)), "small"))
        else:
            table_header = None
            flow.append(para(line))
    build(output, flow)


checks = [
    ("1-1", "최소 수집", "학교별 항목·필수/선택·처리 근거 확정 필요"),
    ("1-2", "목적 기재", "기능별 목적 초안 준비 / 학교 확정 필요"),
    ("1-3", "항목·보유 기간", "재학·졸업 파기 / 첨부 30일 / 업무·로그 등 기간 확정 필요"),
    ("2-1", "안전조치", "개발 회귀 시험 / 운영 환경·장치·백업 실제 시험 필요"),
    ("3-1", "권리 요구 절차", "앱/PC 접수·학교 회신 / 학교 담당·후속 처리 확정 필요"),
    ("4-1", "만 14세 미만", "실제 대상 확인 / 해당 시 법정대리인 동의·확인 필요"),
    ("5-1", "보호책임자", "학교별 설정 초안 / 실제 성명·직위·연락처 확정 필요"),
    ("5-2", "제3자 제공", "실제 제공 여부·항목·근거 등 운영 학교 확인 필요"),
    ("5-3", "위·수탁", "Cloudflare·메일·유지보수 등 실제 계약·처리 위치 확인 필요"),
]
flow = [para("학습지원 소프트웨어 필수기준 검토표", "title"),
        para("공식 서식 작성 전 대응표 · 충족 여부 체크 및 서명 전", "h2"),
        para(f"여양메신저 학교 통합 소통 시스템 (YY Messenger) / 공급자: {args.name}", "small"),
        para(f"개발·관리: {args.organization} / {args.position} / {args.name} / {args.email}", "small"),
        para("설치형 Windows 메신저 + 교사용·학생 및 학부모용 앱. 운영 학교별 서버와 데이터를 분리. 공개 운영 주소 미설정. 버전 1.0.0 출시 전.", "small")]
rows = [[para(c, "small") for c in row] for row in [("항목", "기준", "준비 상태 / 확정할 근거"), *checks]]
table = Table(rows, colWidths=[14 * mm, 32 * mm, 130 * mm])
table.setStyle(TableStyle([("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#e9eef7")),
                           ("GRID", (0, 0), (-1, -1), .4, colors.HexColor("#c8d1df")),
                           ("VALIGN", (0, 0), (-1, -1), "TOP"),
                           ("LEFTPADDING", (0, 0), (-1, -1), 6), ("RIGHTPADDING", (0, 0), (-1, -1), 6),
                           ("TOPPADDING", (0, 0), (-1, -1), 5), ("BOTTOMPADDING", (0, 0), (-1, -1), 4)]))
flow += [Spacer(1, 6), table, Spacer(1, 12),
         para("이 표의 준비 상태는 인증이나 법적 충족 판정이 아닙니다. 미확정 사항을 해결한 뒤 공식 HWP의 충족/미충족/해당 없음 중 하나와 증빙 쪽수를 학교·등록자가 확인하여 작성하세요. 에듀집 등록 양식은 미충족 항목이 있는 경우 등록 불가라고 안내합니다.", "small"),
         para("첨부 근거: 개인정보처리방침 검토안, 학교별 운영 설정·권리 요청 안내, 보안 점검 및 가상 데이터 시험 결과. 학운위 승인·CSAP·ISMS-P·도름스 마크를 보유한 것으로 표시하지 않습니다.", "small"),
         para("공식 안내: https://edzip.kr/main-notice/6a17f7ebb8641362b8b5cabc", "small")]
build("01_필수기준_작성전_검토표.pdf", flow)
markdown_pdf("privacy-policy.draft.md", "02_개인정보처리방침_검토안.pdf")
markdown_pdf("edzip-registration-guide.md", "03_등록방법과_학교별설정.pdf")

for file in ("edzip-registration-guide.md", "edzip-checklist.draft.md", "privacy-policy.draft.md", "privacy-rights-operations.md"):
    shutil.copy2(ROOT / "docs" / file, drafts / file)
for file in ("committee-security-evidence-2026-10-24.md", "security-checklist-2026-10-24.md", "security-incident-response.md"):
    shutil.copy2(ROOT / "docs" / file, evidence / file)
for file in ("feedback-checks.json", "registration-policy-checks.json", "server-operations-checks.json", "security-logging-checks.json", "backend-hardening-checks.json", "feedback-mail-checks.json"):
    source = ROOT / "artifacts" / file
    if source.exists():
        shutil.copy2(source, evidence / file)

intro = f"""에듀집 입력 준비안 - 실제 제출 전 확인

등록 경로: 교사 개인의 필수기준 자료 등록
공식 양식: https://forms.gle/aCa4mjvgtmovEf1eA
소속 / 직책 / 성명: {args.organization} / {args.position} / {args.name}
공급자명 제안: {args.name}
국문 제품명 제안: 여양메신저 학교 통합 소통 시스템
영문 제품명 제안: YY Messenger
개발·관리 이메일: {args.email}
등록자 실제 연락처: [본인 확인 후 입력]
공개 업무 문의처: 여양고등학교 교무실 061-689-5870 [공개 범위 학교 확인]

게시글 본문안
교직원의 Windows 메신저와 교사용 앱, 학생 및 학부모용 학교 앱을 학교별 서버로 연결하는 설치형 소통 프로그램입니다. 교직원 업무 메시지·파일 제출·할 일 확인, 대상별 공지와 가정통신문, 학교가 등록한 시간표·일정, 상담·특별실 예약·선택과목 신청 기능을 제공합니다. 역할·소속·확인된 자녀 관계와 학교가 부여한 업무 권한에 따라 열람·처리 범위를 제한합니다.
각 학교는 전용 서버·데이터·계정·키·약관을 별도로 운영하고, 관리자 프로그램에서 해당 학교의 문서 초안을 준비합니다. 운영 학교의 개인정보 책임자와 확정 방침은 학교가 설정해야 합니다. 개발자가 각 학교의 개인정보 보호책임자로 자동 지정되는 제품이 아닙니다.
현재 버전은 1.0.0 출시 전입니다. Windows 설치 마법사와 역할별 Android 시험 APK, Mac에서 제작할 iOS 소스를 준비했습니다. iOS 정식 배포·운영형 백그라운드 푸시·NEIS 자동 연동·학생 대량 등록·보안 인증·학교 심의 승인을 완료한 것으로 소개하지 않습니다. 일반 공개 웹앱이 아니며, 학교가 마련한 HTTPS 서버 주소와 계정·초대 절차를 통해 설치 앱에서 이용합니다. 프로그램의 기본 이용은 무료이며 학교의 서버·도메인·외부 서비스·스토어 및 서명 비용은 별도 검토 대상입니다.

다음 파일은 완성 제출본이 아닙니다.
02_작성자료_학교확정전: 제품·학교별 설정·처리방침 검토용 PDF/MD.
01_공식서식: 공식 HWP 원본. 등록자 동의서는 본인이 확인·작성·서명합니다.
03_검증요약: 가상 데이터 개발 점검. 실제 학교망·백신·iOS 기기 시험을 대체하지 않습니다.

최종 제출 전
1. 처리 주체와 학교별 책임자/위탁/연령별 절차를 실제 근거로 확정.
2. 개인정보처리방침·항목별 보유 기간·시행일을 확정하고 앱 문서와 대조.
3. 공식 체크리스트 9항목의 충족/해당 없음 근거·문서 쪽수 확인. 미충족인 채 제출하지 않기.
4. 실제 운영 환경과 서명·실기기 시험, 미완료 기능의 사용 범위 제한.
5. 본인 연락처·공개 연락처·제품명 확인, 등록자 개인정보 동의서 본인 작성·서명.
6. 학교별 설정이 달라지는 제품의 등록 증빙 방식은 KERIS 053-714-0357/0308/0341에 확인.

양식 입력·업로드·제출·법적 동의·서명은 자동 수행하지 않았습니다.
"""
(pack / "00_먼저읽기_입력준비안.txt").write_text(intro, encoding="utf-8-sig")
manifest = []
for file in sorted(pack.rglob("*")):
    if file.is_file() and file.name != "파일목록과해시.json":
        manifest.append({"file": file.relative_to(pack).as_posix(), "bytes": file.stat().st_size,
                         "sha256": hashlib.sha256(file.read_bytes()).hexdigest()})
(pack / "파일목록과해시.json").write_text(json.dumps({"version": "1.0.0", "submitted": False, "schoolApproved": False, "files": manifest}, ensure_ascii=False, indent=2), encoding="utf-8")
print(f"Review pack: {len(manifest)} files, no upload or approval; PDFs: {pdfout}")
