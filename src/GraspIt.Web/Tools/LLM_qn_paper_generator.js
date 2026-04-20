// required npm packages: docx
// npm install docx

const {
  Document, Packer, Paragraph, TextRun, Table, TableRow, TableCell,
  AlignmentType, BorderStyle, WidthType, ShadingType, VerticalAlign,
  PageBreak, ImageRun
} = require('docx');
const fs = require('fs');
const path = require('path');

// ── Read Command Line Arguments ──────────────────────────────────────────────
const inputFile = process.argv[2];
let outputFile = process.argv[3] || 'generated_question_paper.docx';

if (!inputFile) {
  console.error("\n❌ Error: Please provide an input JSON file.");
  console.error("Usage: node llm_paper_generator.js <input_data.json> [output_file.docx]\n");
  process.exit(1);
}

if (!fs.existsSync(inputFile)) {
  console.error(`\n❌ Error: Input file '${inputFile}' not found.\n`);
  process.exit(1);
}

// Parse the JSON data provided by the LLM
let papers;
try {
  const rawData = fs.readFileSync(inputFile, 'utf-8');
  papers = JSON.parse(rawData);
  // Ensure it's an array (even if the LLM only outputs one paper)
  if (!Array.isArray(papers)) {
    papers = [papers];
  }
} catch (err) {
  console.error(`\n❌ Error parsing JSON from '${inputFile}':`, err.message, "\n");
  process.exit(1);
}


// ── Page / margin constants ──────────────────────────────────────────────────
const MARGIN   = 720;          // 0.5 inch margins (DXA)
const PAGE_W   = 11906;        // A4 width
const CW_TOTAL = PAGE_W - 2 * MARGIN; // 10466 DXA content width

// ── Logo ─────────────────────────────────────────────────────────────────────
// Resolve logo relative to the script's own directory (not CWD)
const logoPath = path.join(__dirname, "NITTE_Deemed_Univerity_logo.png");
let logoData;
try {
  logoData = fs.readFileSync(logoPath);
} catch (err) {
  console.error(`\n❌ Error: Logo file '${logoPath}' not found. Please ensure it is in the same directory as the script.\n`);
  process.exit(1);
}

// original: 1920×622 px → ratio 3.087
const LOGO_W = 140;
const LOGO_H  = Math.round(LOGO_W / (1920 / 622)); // ~45

// ── Header column widths (Grid layout for perfect alignment) ─────────────────
// We use a 4-column base grid to support perfectly aligned left/center/right text 
// in the bottom row without using nested tables (which cause huge margins).
const COL_1 = 2200; // Logo column
const COL_2 = 1288; // Spacer to divide remaining width into 3rds
const COL_3 = 3489; // Middle 3rd
const COL_4 = 3489; // Right 3rd
// COL_1 + COL_2 + COL_3 + COL_4 = 10466 (CW_TOTAL)
const COL_RIGHT = COL_2 + COL_3 + COL_4; // 8266

// ── Question-table column widths (sum = CW_TOTAL = 10466) ────────────────────
const CW = { qno:540, sub:400, q:7366, marks:780, bt:700, co:680 };

// ── Border helpers ───────────────────────────────────────────────────────────
const THIN = { style: BorderStyle.SINGLE, size: 4, color: "000000" };
const ALLB = { top: THIN, bottom: THIN, left: THIN, right: THIN };

const FNT = "Times New Roman";
const SZ  = 18;  // 9pt in half-pts

// ── Text / Paragraph helpers ─────────────────────────────────────────────────
function tx(text, opts = {}) {
  return new TextRun({
    text:    String(text ?? ''),
    bold:    !!opts.bold,
    italics: !!opts.italic,
    size:    opts.size || SZ,
    font:    opts.font || FNT,
  });
}

function pr(runs, align = AlignmentType.LEFT, spacing = {}) {
  if (!Array.isArray(runs)) runs = [runs];
  return new Paragraph({
    alignment: align,
    spacing,
    children: runs.map(r => typeof r === 'string' ? tx(r) : r),
  });
}

// ── Generic cell builder ─────────────────────────────────────────────────────
function makeCell(children, width, opts = {}) {
  return new TableCell({
    width:         { size: width, type: WidthType.DXA },
    borders:       opts.borders  || ALLB,
    margins:       opts.margins  || { top: 80, bottom: 80, left: 130, right: 130 },
    verticalAlign: opts.va       || VerticalAlign.CENTER,
    rowSpan:       opts.rs,
    columnSpan:    opts.cs,
    shading:  opts.bg ? { fill: opts.bg, type: ShadingType.CLEAR } : undefined,
    children,
  });
}

function tc(text, width, opts = {}) {
  return makeCell(
    [pr([tx(text, { bold: opts.bold, size: opts.size || SZ })],
        opts.center ? AlignmentType.CENTER : AlignmentType.LEFT)],
    width, opts
  );
}

// ── USN row: "USN" label + 10 boxes inline, right-aligned ────────────────────
function makeUSNTable() {
  const N      = 10;
  const labelW = 620;
  const boxW   = 230;
  const brd    = { style: BorderStyle.SINGLE, size: 6, color: "000000" };
  const noBrd  = { style: BorderStyle.NONE, size: 0, color: "FFFFFF" };
  const noAll  = { top: noBrd, bottom: noBrd, left: noBrd, right: noBrd };
  return new Table({
    alignment: AlignmentType.RIGHT,
    width:        { size: labelW + boxW * N, type: WidthType.DXA },
    columnWidths: [labelW, ...Array(N).fill(boxW)],
    rows: [
      new TableRow({
        height: { value: 380, rule: "exact" },
        children: [
          new TableCell({
            width: { size: labelW, type: WidthType.DXA },
            borders: noAll,
            verticalAlign: VerticalAlign.CENTER,
            margins: { top: 0, bottom: 0, left: 0, right: 60 },
            children: [pr([tx("USN", { size: 17 })], AlignmentType.RIGHT)],
          }),
          ...Array(N).fill(null).map(() =>
            new TableCell({
              width:   { size: boxW, type: WidthType.DXA },
              borders: { top: brd, bottom: brd, left: brd, right: brd },
              margins: { top: 0, bottom: 0, left: 0, right: 0 },
              children: [new Paragraph({ children: [] })],
            })
          ),
        ],
      }),
    ],
  });
}

// ══════════════════════════════════════════════════════════════════════════════
//  HEADER TABLE
// ══════════════════════════════════════════════════════════════════════════════
function buildHeaderTable(paper) {

  // ── Row 1: Logo (rowspan=4) | USN row (colspan=3) ───────────────────────────
  const row1 = new TableRow({
    children: [
      new TableCell({
        width:         { size: COL_1, type: WidthType.DXA },
        borders:       ALLB,
        verticalAlign: VerticalAlign.CENTER,
        margins:       { top: 100, bottom: 100, left: 140, right: 140 },
        rowSpan:       4,
        children: [
          new Paragraph({
            alignment: AlignmentType.CENTER,
            spacing: { before: 0, after: 0 },
            children: [
              new ImageRun({
                type: "png",
                data: logoData,
                transformation: { width: LOGO_W, height: LOGO_H },
                altText: { title: "NITTE", description: "NITTE Logo", name: "NITTELogo" },
              }),
            ],
          }),
        ],
      }),
      new TableCell({
        width:         { size: COL_RIGHT, type: WidthType.DXA },
        columnSpan:    3,
        borders:       ALLB,
        verticalAlign: VerticalAlign.CENTER,
        margins:       { top: 70, bottom: 70, left: 130, right: 130 },
        children: [ makeUSNTable() ],
      }),
    ],
  });

  // ── Row 2: Institute name (colspan=3) ───────────────────────────────────────
  const row2 = new TableRow({
    children: [
      new TableCell({
        width:         { size: COL_RIGHT, type: WidthType.DXA },
        columnSpan:    3,
        borders:       ALLB,
        verticalAlign: VerticalAlign.CENTER,
        margins:       { top: 80, bottom: 80, left: 130, right: 130 },
        children: [
          pr([tx("Nitte Institute of Professional Education", { bold: true, size: 22 })],
             AlignmentType.CENTER),
        ],
      }),
    ],
  });

  // ── Row 3: Exam info (colspan=3) ────────────────────────────────────────────
  const row3 = new TableRow({
    children: [
      new TableCell({
        width:         { size: COL_RIGHT, type: WidthType.DXA },
        columnSpan:    3,
        borders:       ALLB,
        verticalAlign: VerticalAlign.CENTER,
        margins:       { top: 70, bottom: 70, left: 130, right: 130 },
        children: [
          pr([tx(paper.semester + " Mid Semester Examination " + paper.examNo, { bold: true, size: 19 })],
             AlignmentType.CENTER),
        ],
      }),
    ],
  });

  // ── Row 4: Subject code + name (colspan=3) ──────────────────────────────────
  const row4 = new TableRow({
    children: [
      new TableCell({
        width:         { size: COL_RIGHT, type: WidthType.DXA },
        columnSpan:    3,
        borders:       ALLB,
        verticalAlign: VerticalAlign.CENTER,
        margins:       { top: 70, bottom: 70, left: 130, right: 130 },
        children: [
          pr([tx(paper.code + paper.title, { bold: true, size: 19 })],
             AlignmentType.CENTER),
        ],
      }),
    ],
  });

  // ── Row 5: Single line layout using precise columns (Invisible inner borders) ─
  const NO_B = { style: BorderStyle.NONE, size: 0, color: "FFFFFF" };

  const row5 = new TableRow({
    children: [
      new TableCell({
        width:         { size: COL_1 + COL_2, type: WidthType.DXA },
        columnSpan:    2,
        borders:       { top: THIN, bottom: THIN, left: THIN, right: NO_B },
        verticalAlign: VerticalAlign.CENTER,
        margins:       { top: 60, bottom: 60, left: 130, right: 130 },
        children: [
          pr([
            tx("Duration: ", { bold: true, size: 17 }),
            tx(paper.duration, { size: 17 })
          ], AlignmentType.LEFT, { before: 0, after: 0 })
        ],
      }),
      new TableCell({
        width:         { size: COL_3, type: WidthType.DXA },
        borders:       { top: THIN, bottom: THIN, left: NO_B, right: NO_B },
        verticalAlign: VerticalAlign.CENTER,
        margins:       { top: 60, bottom: 60, left: 130, right: 130 },
        children: [
          pr([
            tx("Date: ", { bold: true, size: 17 }),
            tx(paper.date + (paper.time ? "  Time: " + paper.time : ""), { size: 17 })
          ], AlignmentType.CENTER, { before: 0, after: 0 })
        ],
      }),
      new TableCell({
        width:         { size: COL_4, type: WidthType.DXA },
        borders:       { top: THIN, bottom: THIN, left: NO_B, right: THIN },
        verticalAlign: VerticalAlign.CENTER,
        margins:       { top: 60, bottom: 60, left: 130, right: 130 },
        children: [
          pr([
            tx("Max Marks: ", { bold: true, size: 17 }),
            tx(paper.maxMarks, { size: 17 })
          ], AlignmentType.RIGHT, { before: 0, after: 0 })
        ],
      }),
    ],
  });

  return new Table({
    width:        { size: CW_TOTAL, type: WidthType.DXA },
    columnWidths: [COL_1, COL_2, COL_3, COL_4],   
    rows:         [row1, row2, row3, row4, row5],
  });
}

// ── Question table ────────────────────────────────────────────────────────────
function buildQTable(paper) {
  const colWidths = [CW.qno, CW.sub, CW.q, CW.marks, CW.bt, CW.co];

  // Note row (italic)
  const noteRow = new TableRow({
    children: [makeCell([
      pr([
        tx("Note:  ", { bold: true, italic: true, size: 18 }),
        tx(paper.note, { italic: true, size: 18 }),
      ], AlignmentType.CENTER),
    ], CW_TOTAL, { cs: 6, margins: { top: 70, bottom: 70, left: 130, right: 130 } })],
  });

  // Header row
  const hdrRow = new TableRow({
    children: [
      tc("Q No",     CW.qno,   { bold: true, center: true, bg: "D4D4D4", margins: { top:70, bottom:70, left:60, right:60 } }),
      tc("",         CW.sub,   { bold: true, bg: "D4D4D4"  }),
      tc("Question", CW.q,     { bold: true, center: true, bg: "D4D4D4" }),
      tc("Marks",    CW.marks, { bold: true, center: true, bg: "D4D4D4" }),
      tc("BT*",      CW.bt,    { bold: true, center: true, bg: "D4D4D4" }),
      tc("CO*",      CW.co,    { bold: true, center: true, bg: "D4D4D4" }),
    ],
  });

  const rows = [noteRow, hdrRow];

  for (const unit of paper.units) {
    if (unit === 'OR') {
      rows.push(new TableRow({
        children: [makeCell(
          [pr([tx("OR", { bold: true, size: 18 })], AlignmentType.CENTER)],
          CW_TOTAL, { cs: 6 }
        )],
      }));
      continue;
    }

    // Unit header
    rows.push(new TableRow({
      children: [makeCell(
        [pr([tx(unit.name, { bold: true, size: 19 })], AlignmentType.CENTER)],
        CW_TOTAL, { cs: 6, bg: "E8E8E8" }
      )],
    }));

    for (const q of unit.questions) {
      if (q === 'OR') {
        rows.push(new TableRow({
          children: [makeCell(
            [pr([tx("OR", { bold: true, size: 18 })], AlignmentType.CENTER)],
            CW_TOTAL, { cs: 6 }
          )],
        }));
        continue;
      }

      q.parts.forEach((p, i) => {
        const cells = [];
        if (i === 0) {
          cells.push(tc(q.no, CW.qno, {
            rs: q.parts.length, center: true, bold: true,
            margins: { top: 60, bottom: 60, left: 60, right: 60 },
          }));
        }
        cells.push(tc(p.sub, CW.sub, { center: true }));

        const qContent = Array.isArray(p.text)
          ? p.text
          : [pr([tx(p.text, { size: 18 })], AlignmentType.LEFT, { before: 0, after: 0 })];

        cells.push(makeCell(qContent, CW.q, {
          va: VerticalAlign.TOP,
          margins: { top: 70, bottom: 70, left: 110, right: 110 },
        }));
        cells.push(tc(String(p.marks), CW.marks, { center: true }));
        cells.push(tc(p.bt,            CW.bt,    { center: true }));
        cells.push(tc(p.co,            CW.co,    { center: true }));
        rows.push(new TableRow({ children: cells }));
      });
    }
  }

  // Footer cell spanning all 6 columns cleanly
  rows.push(new TableRow({
    children: [
      makeCell(
        [pr([tx(paper.footer, { size: 16 })], AlignmentType.LEFT, { before: 0, after: 0 })], 
        CW_TOTAL, 
        { cs: 6, margins: { top: 60, bottom: 60, left: 60, right: 60 } }
      )
    ]
  }));

  return new Table({
    width:        { size: CW_TOTAL, type: WidthType.DXA },
    columnWidths: colWidths,
    rows,
  });
}

// ── Full paper content ────────────────────────────────────────────────────────
function buildPaperContent(paper, addPageBreak) {
  const children = [];
  if (addPageBreak) children.push(new Paragraph({ children: [new PageBreak()] }));
  children.push(buildHeaderTable(paper));
  children.push(new Paragraph({ spacing: { after: 100 }, children: [] }));
  children.push(buildQTable(paper));
  return children;
}

// ════════════════════════════════════════════════════════════════════════════
// ASSEMBLE & WRITE DOCUMENT
// ════════════════════════════════════════════════════════════════════════════

const allChildren = [];
papers.forEach((paper, idx) => {
  buildPaperContent(paper, idx > 0).forEach(c => allChildren.push(c));
});

const doc = new Document({
  sections: [{
    properties: {
      page: {
        size:   { width: 11906, height: 16838 }, // A4
        margin: { top: MARGIN, bottom: MARGIN, left: MARGIN, right: MARGIN },
      },
    },
    children: allChildren,
  }],
});

Packer.toBuffer(doc).then(buf => {
  if (!outputFile.endsWith(".docx")) outputFile += ".docx";
  fs.writeFileSync(outputFile, buf);
  console.log(`✅ Successfully generated: ${outputFile}`);
}).catch(err => {
  console.error("❌ Error generating document:", err);
});