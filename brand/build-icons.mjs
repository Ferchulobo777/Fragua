// Genera el set de iconos de Fragua a partir de los SVG de marca.
// Usa el Chromium de Playwright que ya esta instalado en el portfolio.
// Resuelto por ruta absoluta: el script vive fuera de ese proyecto.
const { chromium } = await import(
  "file:///C:/Users/Notebook/Downloads/portfolio 2026/node_modules/playwright/index.mjs"
);
import { readFileSync, mkdirSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const out = join(here, "out");
mkdirSync(out, { recursive: true });

const read = (f) => readFileSync(join(here, f), "utf8");
const iconBig = read("fragua-icon.svg");
const iconSmall = read("fragua-icon-small.svg");
const markBig = read("fragua-mark.svg");
const markSmall = read("fragua-mark-small.svg");

// Por debajo de 32 px manda la version simplificada: el trazo fino y los
// escalones de la marca completa no sobreviven a esa resolucion.
const iconFor = (size) => (size < 32 ? iconSmall : iconBig);
const markFor = (size) => (size < 32 ? markSmall : markBig);

const browser = await chromium.launch();

async function render(svg, size, file, { transparent = false } = {}) {
  const page = await browser.newPage({
    viewport: { width: size, height: size },
    deviceScaleFactor: 1,
  });
  await page.setContent(
    `<!doctype html><style>
       html,body{margin:0;padding:0;background:${transparent ? "transparent" : "#181818"}}
       svg{display:block;width:${size}px;height:${size}px}
     </style>${svg}`
  );
  await page.screenshot({ path: join(out, file), omitBackground: transparent });
  await page.close();
}

for (const size of [16, 20, 24, 32, 48, 64, 128, 256, 512, 1024]) {
  await render(iconFor(size), size, `icon_${size}.png`);
}

for (const [name, color] of [
  ["light", "#F2F2F2"],
  ["ember", "#F27636"],
  ["dark", "#181818"],
]) {
  for (const size of [64, 256, 512]) {
    const tinted = markFor(size).replace("<svg ", `<svg style="color:${color}" `);
    await render(tinted, size, `mark_${name}_${size}.png`, { transparent: true });
  }
}

// Hoja de contacto para revisar el comportamiento real a cada tamano.
const at = (svg, s, extra = "") =>
  svg.replace("<svg ", `<svg width="${s}" height="${s}" ${extra} `);

const sheet = await browser.newPage({ viewport: { width: 1000, height: 560 } });
await sheet.setContent(`<!doctype html>
<style>
  body{margin:0;background:#0D0D0D;color:#A4A4A4;font:12px/1.4 "Segoe UI",sans-serif;padding:28px}
  .row{display:flex;align-items:flex-end;gap:26px;margin-bottom:32px}
  .cell{text-align:center}
  .cell svg{display:block;margin:0 auto 8px}
  .light{background:#F2F2F2;padding:22px;border-radius:8px}
  .light span{color:#717171}
  h3{color:#F2F2F2;font-size:13px;font-weight:600;margin:0 0 12px}
</style>
<h3>Icono sobre fondo oscuro</h3>
<div class="row">
  ${[16, 20, 24, 32, 48, 64, 128]
    .map((s) => `<div class="cell">${at(iconFor(s), s)}<span>${s}</span></div>`)
    .join("")}
</div>
<h3>Icono sobre fondo claro</h3>
<div class="row light">
  ${[16, 20, 24, 32, 48, 64, 128]
    .map((s) => `<div class="cell">${at(iconFor(s), s)}<span>${s}</span></div>`)
    .join("")}
</div>
<h3>Marca sola, brasa y tinta</h3>
<div class="row">
  ${[16, 24, 32, 48, 96]
    .map((s) => `<div class="cell">${at(markFor(s), s, 'style="color:#F27636"')}<span>${s}</span></div>`)
    .join("")}
  ${[16, 24, 32, 48, 96]
    .map((s) => `<div class="cell">${at(markFor(s), s, 'style="color:#F2F2F2"')}<span>${s}</span></div>`)
    .join("")}
</div>`);
await sheet.screenshot({ path: join(out, "_contact-sheet.png"), fullPage: true });
await sheet.close();

await browser.close();
console.log("Listo:", out);
