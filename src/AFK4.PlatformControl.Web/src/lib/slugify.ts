const CYRILLIC_TO_LATIN: Record<string, string> = {
  а: 'a', б: 'b', в: 'v', г: 'g', д: 'd', е: 'e', ё: 'e', ж: 'zh',
  з: 'z', и: 'i', й: 'y', к: 'k', л: 'l', м: 'm', н: 'n', о: 'o',
  п: 'p', р: 'r', с: 's', т: 't', у: 'u', ф: 'f', х: 'h', ц: 'ts',
  ч: 'ch', ш: 'sh', щ: 'sch', ъ: '', ы: 'y', ь: '', э: 'e', ю: 'yu',
  я: 'ya'
};

/**
 * Produces a `[a-z0-9]` slug with single hyphens between segments, matching the
 * backend SlugValidator pattern. Cyrillic is transliterated to latin; any other
 * non-alphanumeric run becomes a single hyphen. May return '' (caller validates).
 */
export function slugify(input: string): string {
  const lower = input.trim().toLowerCase();
  let out = '';
  for (const char of lower) {
    if (Object.prototype.hasOwnProperty.call(CYRILLIC_TO_LATIN, char)) {
      out += CYRILLIC_TO_LATIN[char];
    } else if (/[a-z0-9]/.test(char)) {
      out += char;
    } else {
      out += '-';
    }
  }
  return out.replace(/-+/g, '-').replace(/^-+|-+$/g, '');
}
