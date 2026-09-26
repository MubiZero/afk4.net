/** «123456» → «123 456»: так код первого входа читают вслух и переписывают без ошибок. */
export function groupCode(code: string): string {
  return code.length === 6 ? `${code.slice(0, 3)} ${code.slice(3)}` : code;
}
