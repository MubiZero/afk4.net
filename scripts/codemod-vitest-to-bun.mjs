#!/usr/bin/env bun
// scripts/codemod-vitest-to-bun.mjs
// One-shot: rewrites the mechanical vitest surface in *.test.ts(x) to bun:test.
// Special files are passed via --skip and left untouched for hand-rewriting.
import { readdirSync, readFileSync, writeFileSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';

const [, , srcDir, ...rest] = process.argv;
if (!srcDir) {
  console.error('usage: bun scripts/codemod-vitest-to-bun.mjs <srcDir> [--skip rel/path ...]');
  process.exit(1);
}
const skip = new Set(rest.filter((a) => a !== '--skip').map((p) => p.split('/').join(sep)));

function walk(dir) {
  const out = [];
  for (const name of readdirSync(dir)) {
    if (name === 'node_modules') continue;
    const full = join(dir, name);
    if (statSync(full).isDirectory()) out.push(...walk(full));
    else if (/\.test\.tsx?$/.test(name)) out.push(full);
  }
  return out;
}

function transform(content) {
  let s = content;
  s = s.replace(/\bvi\.fn\b/g, 'mock');
  s = s.replace(/\bvi\.spyOn\b/g, 'spyOn');
  s = s.replace(/\bvi\.restoreAllMocks\b/g, 'mock.restore');
  s = s.replace(/\bvi\.clearAllMocks\b/g, 'jest.clearAllMocks');
  s = s.replace(
    /import\s*\{([^}]*)\}\s*from\s*['"]vitest['"];?/,
    (_full, names) => {
      const kept = names
        .split(',')
        .map((n) => n.trim())
        .filter(Boolean)
        .filter((n) => n !== 'vi');
      for (const helper of ['mock', 'spyOn', 'jest']) {
        if (new RegExp(`\\b${helper}\\b`).test(s) && !kept.includes(helper)) kept.push(helper);
      }
      return `import { ${kept.join(', ')} } from 'bun:test';`;
    }
  );
  return s;
}

let changed = 0;
for (const file of walk(srcDir)) {
  const rel = relative(srcDir, file);
  if (skip.has(rel)) { console.log('skip   ', rel); continue; }
  const before = readFileSync(file, 'utf8');
  if (!before.includes("'vitest'") && !before.includes('"vitest"')) continue;
  const after = transform(before);
  if (after !== before) { writeFileSync(file, after); changed++; console.log('mod    ', rel); }
}
console.log(`\n${changed} files modified.`);
