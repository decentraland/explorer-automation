/**
 * Scaffolds a new visual-regression scene + matching C# fixture stub.
 *
 *   npm run new-scene -- <scene-id>
 *
 * <scene-id> must be kebab-case (a-z, 0-9, hyphens). It becomes:
 *   - the npm package name suffix (@dcl-vrt/<scene-id>)
 *   - the directory name in packages/
 *   - the C# fixture class name (PascalCase + "Fixture")
 *   - the snapshot baseline filename root
 */
import { cpSync, existsSync, mkdirSync, readdirSync, readFileSync, statSync, writeFileSync } from 'node:fs'
import { join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = fileURLToPath(new URL('..', import.meta.url))
const REPO_ROOT = fileURLToPath(new URL('../..', import.meta.url))
const PACKAGES = join(ROOT, 'packages')
const TEMPLATE = join(ROOT, 'templates', 'scene')
const FIXTURES_DIR = join(REPO_ROOT, 'Tests', 'Tests', 'Visual')

const sceneId = process.argv[2]?.trim()
if (!sceneId) die(`Usage: npm run new-scene -- <scene-id>`)
if (!/^[a-z][a-z0-9-]*$/.test(sceneId)) die(`Invalid scene-id "${sceneId}". Use kebab-case: a-z, 0-9, hyphens.`)

const dest = join(PACKAGES, sceneId)
if (existsSync(dest)) die(`Package already exists: ${relative(REPO_ROOT, dest)}`)

cpSync(TEMPLATE, dest, { recursive: true })
substituteInTree(dest, '__SCENE_ID__', sceneId)

const className = toPascalCase(sceneId) + 'Fixture'
const fixturePath = join(FIXTURES_DIR, `${className}.cs`)
if (!existsSync(FIXTURES_DIR)) mkdirSync(FIXTURES_DIR, { recursive: true })
if (existsSync(fixturePath)) die(`Fixture already exists: ${relative(REPO_ROOT, fixturePath)}`)
writeFileSync(fixturePath, fixtureSource(sceneId, className))

console.log(`Created scene  ${relative(REPO_ROOT, dest)}`)
console.log(`Created fixture ${relative(REPO_ROOT, fixturePath)}`)
console.log(``)
console.log(`Next steps:`)
console.log(`  1. Edit packages/${sceneId}/src/index.ts — what the scene renders.`)
console.log(`  2. Edit ../Tests/Tests/Visual/${className}.cs — snapshot assertions.`)
console.log(`  3. metaforge explorer test dev --filter "FullyQualifiedName~${className}" --record-baselines`)

function substituteInTree(dir: string, from: string, to: string): void {
  for (const name of readdirSync(dir)) {
    const path = join(dir, name)
    if (statSync(path).isDirectory()) {
      substituteInTree(path, from, to)
      continue
    }
    const original = readFileSync(path, 'utf8')
    if (!original.includes(from)) continue
    writeFileSync(path, original.split(from).join(to))
  }
}

function toPascalCase(id: string): string {
  return id
    .split('-')
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join('')
}

function fixtureSource(sceneId: string, className: string): string {
  return `namespace ExplorerAutomation.Tests.Tests.Visual;

[TestFixture]
[AllureNUnit]
[AllureSuite("Visual")]
[AllureSubSuite("${sceneId}")]
[Category("Visual")]
public class ${className}
{
    [OneTimeSetUp]
    public void LoadScene()
    {
        VisualHost.Load("${sceneId}");
    }

    [Test]
    public void Default()
    {
        Snapshot.AssertMatchesBaseline("default", tolerance: 0.5);
    }
}
`
}

function die(msg: string): never {
  process.stderr.write(`${msg}\n`)
  process.exit(1)
}
