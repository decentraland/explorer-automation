import { readFileSync } from 'node:fs'

/**
 * Inflates a valid GLB to `targetBytes` by padding its final (BIN) chunk and
 * fixing the header/chunk length fields, so the result still parses as a
 * spec-valid model (the padding is unreferenced BIN data). Needed for the
 * oversize-upload negative: the builder's ImportStep runs the Babylon parse
 * BEFORE the MAX_WEARABLE_FILE_SIZE check (ImportStep.tsx handleModelFile),
 * so a junk buffer would fail with a parse error instead of the size error.
 */
export function inflateGlb(filePath: string, targetBytes: number): Buffer {
  const source = readFileSync(filePath)
  // keep GLB 4-byte chunk alignment
  const paddingLength = Math.ceil((targetBytes - source.length) / 4) * 4
  const inflated = Buffer.concat([source, Buffer.alloc(paddingLength)])

  // header: magic(0..3) version(4..7) totalLength(8..11)
  inflated.writeUInt32LE(inflated.length, 8)

  // walk the chunk list to the last chunk and extend its declared length
  let offset = 12
  for (;;) {
    const chunkLength = inflated.readUInt32LE(offset)
    const next = offset + 8 + chunkLength
    if (next >= source.length) {
      inflated.writeUInt32LE(chunkLength + paddingLength, offset)
      break
    }
    offset = next
  }
  return inflated
}
