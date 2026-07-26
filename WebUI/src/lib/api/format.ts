/**
 * Presentation helpers for raw game identifiers.
 */

const SIDE_TOKENS: Record<string, string> = {
  R: 'Right',
  L: 'Left'
}

/**
 * Prettifies raw definition strings like PART_R_LEG → "Right Leg",
 * AILMENT_CLAWFLINCH → "Clawflinch". Already-friendly names
 * ("Head", "Poison") pass through unchanged.
 */
export function prettifyName(raw: string): string {
  if (!raw) return raw

  const match = raw.match(/^(PART|AILMENT)_(.+)$/i)
  const body = match ? match[2] : raw

  // Friendly already (has lowercase letters, no underscores)
  if (!body.includes('_') && /[a-z]/.test(body)) return body

  return body
    .split('_')
    .filter((token) => token.length > 0)
    .map((token) => {
      if (SIDE_TOKENS[token.toUpperCase()]) return SIDE_TOKENS[token.toUpperCase()]

      return token.charAt(0).toUpperCase() + token.slice(1).toLowerCase()
    })
    .join(' ')
}

/** "ChargeBlade" → "Charge Blade" */
export function splitCamelCase(raw: string): string {
  return raw.replace(/([a-z])([A-Z])/g, '$1 $2')
}
