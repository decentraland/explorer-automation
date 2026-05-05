import {
  Entity,
  LightSource,
  Material,
  MeshRenderer,
  Transform,
  engine,
} from '@dcl/sdk/ecs'
import { Color3, Color4, Quaternion, Vector3 } from '@dcl/sdk/math'
import { setupVisualTest } from './visual-test-setup'

/**
 * core — primitives + transforms + lighting.
 *
 * Visual contract: any regression in mesh primitive rendering, transform-tree
 * composition, point/spot lighting, or PBR base shading shows up here first.
 * Subjects are arranged left-to-right so per-subject diffs are easy to read.
 */
export function main() {
  // ── ground plane ─────────────────────────────────────────────────────────
  const ground = engine.addEntity()
  Transform.create(ground, {
    position: Vector3.create(8, 0, 8),
    scale: Vector3.create(16, 0.05, 16),
  })
  MeshRenderer.setBox(ground)
  Material.setPbrMaterial(ground, {
    albedoColor: Color4.create(0.4, 0.4, 0.42, 1),
    roughness: 0.9,
    metallic: 0,
  })

  // ── primitives row at y=1 ────────────────────────────────────────────────
  // Each primitive at a known x; consistent y/z; distinct color so per-shape
  // regressions are easy to localize.
  const yRow = 1
  const zRow = 8

  const box = engine.addEntity()
  Transform.create(box, { position: Vector3.create(4, yRow, zRow) })
  MeshRenderer.setBox(box)
  Material.setPbrMaterial(box, {
    albedoColor: Color4.create(0.85, 0.2, 0.2, 1),
    roughness: 0.5,
  })

  const sphere = engine.addEntity()
  Transform.create(sphere, { position: Vector3.create(6.5, yRow, zRow) })
  MeshRenderer.setSphere(sphere)
  Material.setPbrMaterial(sphere, {
    albedoColor: Color4.create(0.2, 0.7, 0.3, 1),
    roughness: 0.4,
  })

  const cylinder = engine.addEntity()
  Transform.create(cylinder, { position: Vector3.create(9, yRow, zRow) })
  MeshRenderer.setCylinder(cylinder)
  Material.setPbrMaterial(cylinder, {
    albedoColor: Color4.create(0.2, 0.4, 0.85, 1),
    roughness: 0.5,
  })

  const plane = engine.addEntity()
  Transform.create(plane, {
    position: Vector3.create(11.5, yRow, zRow),
    rotation: Quaternion.fromEulerDegrees(0, 30, 0),
  })
  MeshRenderer.setPlane(plane)
  Material.setPbrMaterial(plane, {
    albedoColor: Color4.create(0.85, 0.75, 0.2, 1),
  })

  // ── hierarchical transform chain ─────────────────────────────────────────
  // Parent rotated 30° around Y; children offset along local axes. Tests
  // world-space composition (parent rotation × child translation).
  const parent = engine.addEntity()
  Transform.create(parent, {
    position: Vector3.create(8, 2.5, 11),
    rotation: Quaternion.fromEulerDegrees(0, 30, 0),
  })

  makePrimitive(parent, Vector3.create(-0.6, 0, 0), 'box', Color4.create(0.9, 0.9, 0.9, 1))
  makePrimitive(parent, Vector3.create(0.6, 0, 0), 'sphere', Color4.create(0.9, 0.5, 0.2, 1))
  makePrimitive(parent, Vector3.create(0, 0.6, 0), 'cylinder', Color4.create(0.5, 0.2, 0.8, 1))

  // ── lighting ─────────────────────────────────────────────────────────────
  // Point + spot at fixed transforms. Catches shading-model regressions and
  // any change to how dynamic lights compose with PBR.
  const pointLight = engine.addEntity()
  Transform.create(pointLight, { position: Vector3.create(8, 4, 5) })
  LightSource.create(pointLight, {
    color: Color3.create(1, 0.95, 0.85),
    intensity: 40000,
    range: 12,
    active: true,
    shadow: false,
    type: LightSource.Type.Point({}),
  })

  const spotLight = engine.addEntity()
  Transform.create(spotLight, {
    position: Vector3.create(11, 5, 5),
    rotation: Quaternion.fromEulerDegrees(45, 180, 0),
  })
  LightSource.create(spotLight, {
    color: Color3.create(0.6, 0.8, 1),
    intensity: 60000,
    range: 14,
    active: true,
    shadow: false,
    type: LightSource.Type.Spot({ innerAngle: 12, outerAngle: 30 }),
  })

  // Frame the composition center: primitives row at z=8 + parent group at z=11.
  // Aim at (8, 1.5, 9.5) so both stay in frame; camera at z=0 for breathing room.
  setupVisualTest({
    lookAtPos: Vector3.create(8, 1.5, 9.5),
    cameraPos: Vector3.create(8, 4.5, 0),
  })
}

function makePrimitive(
  parent: Entity,
  offset: Vector3,
  shape: 'box' | 'sphere' | 'cylinder',
  color: Color4,
): Entity {
  const e = engine.addEntity()
  Transform.create(e, { parent, position: offset, scale: Vector3.create(0.5, 0.5, 0.5) })
  if (shape === 'box') MeshRenderer.setBox(e)
  else if (shape === 'sphere') MeshRenderer.setSphere(e)
  else MeshRenderer.setCylinder(e)
  Material.setPbrMaterial(e, { albedoColor: color, roughness: 0.5 })
  return e
}
