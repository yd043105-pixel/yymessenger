"""Render the four curated Korean user guides. Uses the installed ReportLab runtime."""
from pathlib import Path
import html
import re
from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, PageBreak, KeepTogether

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / 'output/pdf/public'
OUT.mkdir(parents=True, exist_ok=True)
pdfmetrics.registerFont(TTFont('Korean', 'C:/Windows/Fonts/malgun.ttf'))
pdfmetrics.registerFont(TTFont('KoreanBold', 'C:/Windows/Fonts/malgunbd.ttf'))
pdfmetrics.registerFontFamily('Korean', normal='Korean', bold='KoreanBold')
INK, BLUE, MUTED, LINE, PALE = map(colors.HexColor, ['#172A43', '#2857CB', '#52647B', '#DCE4EF', '#EFF4FF'])
WIDTH, HEIGHT = A4
styles = {
    'body': ParagraphStyle('body', fontName='Korean', fontSize=10.2, leading=17, wordWrap='CJK', textColor=INK, spaceAfter=9),
    'section': ParagraphStyle('section', fontName='KoreanBold', fontSize=12, leading=18, wordWrap='CJK', textColor=INK, spaceBefore=13, spaceAfter=7, keepWithNext=True),
    'page': ParagraphStyle('page', fontName='KoreanBold', fontSize=23, leading=32, wordWrap='CJK', textColor=INK, spaceAfter=18),
    'cell': ParagraphStyle('cell', fontName='Korean', fontSize=9, leading=14, wordWrap='CJK', textColor=INK),
    'head': ParagraphStyle('head', fontName='KoreanBold', fontSize=9, leading=14, wordWrap='CJK', textColor=BLUE),
    'note': ParagraphStyle('note', fontName='Korean', fontSize=10, leading=17, wordWrap='CJK', textColor=BLUE),
}

def p(value, style='body'):
    return Paragraph(html.escape(value), styles[style])

def footer(canvas, doc):
    canvas.saveState()
    canvas.setStrokeColor(LINE)
    canvas.line(44, HEIGHT - 45, WIDTH - 44, HEIGHT - 45)
    canvas.setFont('KoreanBold', 10)
    canvas.setFillColor(BLUE)
    canvas.drawString(44, HEIGHT - 32, '교무톡')
    canvas.setFont('Korean', 8)
    canvas.setFillColor(MUTED)
    canvas.drawRightString(WIDTH - 44, HEIGHT - 32, doc.title)
    canvas.drawString(44, 27, '1.0.0 시험용 안내  |  2026.10.29')
    canvas.drawRightString(WIDTH - 44, 27, str(doc.page))
    canvas.restoreState()

for source in sorted((ROOT / 'docs/public').glob('*.md')):
    lines = source.read_text(encoding='utf-8').splitlines()
    title = lines[0].removeprefix('# ')
    story = []
    first = True
    i = 1
    while i < len(lines):
        line = lines[i].strip()
        i += 1
        if not line:
            continue
        if line.startswith('## '):
            if not first:
                story.append(PageBreak())
            first = False
            story.append(p(line[3:], 'page'))
        elif line.startswith('### '):
            story.append(p(line[4:], 'section'))
        elif line.startswith('> '):
            note = Table([[p(line[2:], 'note')]], colWidths=[WIDTH - 88])
            note.setStyle(TableStyle([('BACKGROUND', (0, 0), (-1, -1), PALE), ('BOX', (0, 0), (-1, -1), 0.5, LINE), ('LEFTPADDING', (0, 0), (-1, -1), 12), ('RIGHTPADDING', (0, 0), (-1, -1), 12), ('TOPPADDING', (0, 0), (-1, -1), 10), ('BOTTOMPADDING', (0, 0), (-1, -1), 10)]))
            story.extend([note, Spacer(1, 10)])
        elif line.startswith('|'):
            rows = [line]
            while i < len(lines) and lines[i].strip().startswith('|'):
                rows.append(lines[i].strip())
                i += 1
            cells = [[x.strip() for x in row.strip('|').split('|')] for row in rows]
            cols = len(cells[0])
            ratios = ([0.26, 0.44, 0.30] if source.name.startswith('00') else [0.16, 0.44, 0.40]) if cols == 3 else [0.35, 0.65]
            table = Table([[p(x, 'head' if n == 0 else 'cell') for x in row] for n, row in enumerate(cells)], colWidths=[(WIDTH - 88)*r for r in ratios], repeatRows=1, hAlign='LEFT')
            table.setStyle(TableStyle([('BACKGROUND', (0, 0), (-1, 0), PALE), ('VALIGN', (0, 0), (-1, -1), 'TOP'), ('LINEBELOW', (0, 0), (-1, -1), .5, LINE), ('LEFTPADDING', (0, 0), (-1, -1), 8), ('RIGHTPADDING', (0, 0), (-1, -1), 8), ('TOPPADDING', (0, 0), (-1, -1), 9), ('BOTTOMPADDING', (0, 0), (-1, -1), 9)]))
            story.extend([table, Spacer(1, 9)])
        else:
            story.append(p(line))
    doc = SimpleDocTemplate(str(OUT / (source.stem + '.pdf')), pagesize=A4, rightMargin=44, leftMargin=44, topMargin=68, bottomMargin=48, title=title, author='교무톡 프로젝트', subject='학교별 공통 사용자 안내', pageCompression=1)
    doc.build(story, onFirstPage=footer, onLaterPages=footer)
    print(source.stem + '.pdf')
