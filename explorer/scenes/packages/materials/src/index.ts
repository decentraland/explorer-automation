import {
  Entity,
  LightSource,
  Material,
  MaterialTransparencyMode,
  MeshRenderer,
  Transform,
  engine,
} from '@dcl/sdk/ecs'
import { Color3, Color4, Quaternion, Vector3 } from '@dcl/sdk/math'
import { setupVisualTest } from './visual-test-setup'

/**
 * materials — PBR property matrix.
 *
 * Visual contract: a grid of identical spheres lit identically, varying ONE
 * material property per row. Any shader regression — BRDF rewrite, lighting
 * model tweak, alpha pipeline change — shows up as per-cell drift in the diff.
 *
 * Layout (looking from +Z toward -Z):
 *   row 0  metallic=0   roughness ramp 0 → 1
 *   row 1  metallic=1   roughness ramp 0 → 1
 *   row 2  emissive intensity ramp 0 → 4
 *   row 3  unlit | alpha-blend (50%) | alpha-test (cutout)
 */
export function main() {
  const sphereY = 1.5
  const cols = 5
  const colSpacing = 1.6
  const rowSpacing = 1.8

  // Origin (8, sphereY, 8) is the grid center.
  const gridX = (col: number) => 8 + (col - (cols - 1) / 2) * colSpacing
  const gridZ = (row: number) => 8 + (row - 1.5) * rowSpacing

  // ── ground ───────────────────────────────────────────────────────────────
  const ground = engine.addEntity()
  Transform.create(ground, {
    position: Vector3.create(8, 0, 8),
    scale: Vector3.create(16, 0.05, 16),
  })
  MeshRenderer.setBox(ground)
  Material.setPbrMaterial(ground, {
    albedoColor: Color4.create(0.4, 0.4, 0.42, 1),
    roughness: 0.95,
    metallic: 0,
  })

  const spheres: Entity[] = []

  // ── row 0: dielectric (metallic=0), roughness ramp ───────────────────────
  for (let col = 0; col < cols; col++) {
    const e = mkSphere(gridX(col), sphereY, gridZ(0))
    Material.setPbrMaterial(e, {
      albedoColor: Color4.create(0.85, 0.3, 0.3, 1),
      metallic: 0,
      roughness: col / (cols - 1),
    })
    spheres.push(e)
  }

  // ── row 1: metal (metallic=1), roughness ramp ────────────────────────────
  for (let col = 0; col < cols; col++) {
    const e = mkSphere(gridX(col), sphereY, gridZ(1))
    Material.setPbrMaterial(e, {
      albedoColor: Color4.create(0.85, 0.85, 0.9, 1),
      metallic: 1,
      roughness: col / (cols - 1),
    })
  }

  // ── row 2: emissive intensity ramp ───────────────────────────────────────
  const intensities = [0, 0.5, 1, 2, 4]
  for (let col = 0; col < cols; col++) {
    const e = mkSphere(gridX(col), sphereY, gridZ(2))
    Material.setPbrMaterial(e, {
      albedoColor: Color4.create(0.05, 0.05, 0.05, 1),
      emissiveColor: Color3.create(0.95, 0.4, 0.1),
      emissiveIntensity: intensities[col],
      metallic: 0,
      roughness: 0.5,
    })
  }

  // ── row 3: unlit, alpha-blend, alpha-test ────────────────────────────────
  const unlit = mkSphere(gridX(1), sphereY, gridZ(3))
  Material.setBasicMaterial(unlit, {
    diffuseColor: Color4.create(0.95, 0.85, 0.2, 1),
  })

  const alphaBlend = mkSphere(gridX(2), sphereY, gridZ(3))
  Material.setPbrMaterial(alphaBlend, {
    albedoColor: Color4.create(0.2, 0.7, 0.95, 0.5),
    transparencyMode: MaterialTransparencyMode.MTM_ALPHA_BLEND,
    metallic: 0,
    roughness: 0.3,
  })

  const alphaTest = mkSphere(gridX(3), sphereY, gridZ(3))
  Material.setPbrMaterial(alphaTest, {
    albedoColor: Color4.create(0.95, 0.4, 0.85, 1),
    transparencyMode: MaterialTransparencyMode.MTM_ALPHA_TEST,
    alphaTest: 0.5,
    metallic: 0,
    roughness: 0.5,
  })

  // ── lighting ─────────────────────────────────────────────────────────────
  const keyLight = engine.addEntity()
  Transform.create(keyLight, { position: Vector3.create(8, 6, 4) })
  LightSource.create(keyLight, {
    color: Color3.create(1, 0.97, 0.9),
    intensity: 70000,
    range: 18,
    active: true,
    shadow: false,
    type: LightSource.Type.Point({}),
  })

  const fillLight = engine.addEntity()
  Transform.create(fillLight, {
    position: Vector3.create(8, 5, 12),
    rotation: Quaternion.fromEulerDegrees(45, 0, 0),
  })
  LightSource.create(fillLight, {
    color: Color3.create(0.6, 0.75, 1),
    intensity: 50000,
    range: 16,
    active: true,
    shadow: false,
    type: LightSource.Type.Spot({ innerAngle: 20, outerAngle: 50 }),
  })

  setupVisualTest({
    lookAt: spheres[Math.floor(spheres.length / 2)],
    cameraPos: Vector3.create(8, 6, 0.5),
  })
}

function mkSphere(x: number, y: number, z: number): Entity {
  const e = engine.addEntity()
  Transform.create(e, {
    position: Vector3.create(x, y, z),
    scale: Vector3.create(0.7, 0.7, 0.7),
  })
  MeshRenderer.setSphere(e)
  return e
}
