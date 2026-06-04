import { test, expect } from "bun:test";
import { readFileSync, existsSync } from "node:fs";
import { join } from "node:path";

const BRAND = import.meta.dir; // brand/ dir

function read(rel: string): string {
  return readFileSync(join(BRAND, rel), "utf8");
}

test("dark mark uses accent #2DD4A7 on three diagonal cells", () => {
  const svg = read("afk4-mark.svg");
  expect(svg).toContain("<svg");
  expect(svg).toContain('viewBox="0 0 52 52"');
  const accents = svg.match(/#2DD4A7/gi) ?? [];
  expect(accents.length).toBe(3); // exactly three active cells
  expect(svg).toContain("#173028"); // inactive cells present
});

test("light mark uses deep accent #0B9E74 and light inactive cells", () => {
  const svg = read("afk4-mark-light.svg");
  expect((svg.match(/#0B9E74/gi) ?? []).length).toBe(3);
  expect(svg).toContain("#D9E6E1");
});

test("no lime placeholder remains in brand sources", () => {
  expect(read("afk4-mark.svg").toLowerCase()).not.toContain("#c8ff00");
});

test("app icon has dark rounded background and three accent cells", () => {
  const svg = read("afk4-icon.svg");
  expect(svg).toContain('viewBox="0 0 64 64"');
  expect(svg).toContain("#0E2019");                       // bg
  expect((svg.match(/#2DD4A7/gi) ?? []).length).toBe(3);  // active cells
});

test("maskable icon keeps content inside the safe zone via scale", () => {
  const svg = read("afk4-icon-maskable.svg");
  expect(svg).toContain('viewBox="0 0 64 64"');
  expect(svg).toContain("scale(");
});

test("horizontal lockup has wordmark with accent .NET", () => {
  const svg = read("afk4-logo-horizontal.svg");
  expect(svg).toContain(">AFK4<");
  expect(svg).toContain('fill="#2DD4A7">.NET<');
  expect(svg).toContain("#E2F1EC"); // wordmark text color (dark surface)
});

test("vertical lockup exists and stacks mark over wordmark", () => {
  const svg = read("afk4-logo-vertical.svg");
  expect(svg).toContain(">AFK4<");
  expect(svg).toContain("translate");
});
