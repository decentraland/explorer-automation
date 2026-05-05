import {
  GltfContainer,
  LightSource,
  Material,
  MeshRenderer,
  Transform,
  engine,
} from '@dcl/sdk/ecs'
import { Color3, Color4, Quaternion, Vector3 } from '@dcl/sdk/math'
import { setupVisualTest } from './visual-test-setup'

/**
 * gltf — GLTF asset import + multi-mesh assemblies.
 *
 * Visual contract: any regression in the GLTF importer (vertex layouts,
 * embedded materials, asset bundle conversion, mesh hierarchy) shows up
 * here. Three subjects of varying scale and complexity:
 *
 *   tree         — vegetation, large scale, foliage transparency
 *   treehouse    — multi-mesh structure, baked textures
 *   gecko-statue — small detailed mesh
 *
 */
export function main() {
  // ── ground ───────────────────────────────────────────────────────────────
  const ground = engine.addEntity()
  Transform.create(ground, {
    position: Vector3.create(8, 0, 8),
    scale: Vector3.create(16, 0.05, 16),
  })
  MeshRenderer.setBox(ground)
  Material.setPbrMaterial(ground, {
    albedoColor: Color4.create(0.42, 0.45, 0.4, 1),
    roughness: 0.95,
  })

  // ── three GLTF subjects in a row along x at z=8 ──────────────────────────
  const tree = engine.addEntity()
  Transform.create(tree, {
    position: Vector3.create(3, 0, 8),
    scale: Vector3.create(0.8, 0.8, 0.8),
  })
  GltfContainer.create(tree, { src: 'assets/genesis/tree.glb' })

  const gecko = engine.addEntity()
  Transform.create(gecko, {
    position: Vector3.create(8, 0, 8),
    rotation: Quaternion.fromEulerDegrees(0, 180, 0),
    scale: Vector3.create(2, 2, 2),
  })
  GltfContainer.create(gecko, { src: 'assets/genesis/gecko-statue.glb' })

  const treehouse = engine.addEntity()
  Transform.create(treehouse, {
    position: Vector3.create(13, 0, 8),
    scale: Vector3.create(0.8, 0.8, 0.8),
  })
  GltfContainer.create(treehouse, { src: 'assets/genesis/treehouse.glb' })

  // ── lighting ─────────────────────────────────────────────────────────────
  const keyLight = engine.addEntity()
  Transform.create(keyLight, { position: Vector3.create(8, 6, 4) })
  LightSource.create(keyLight, {
    color: Color3.create(1, 0.97, 0.9),
    intensity: 60000,
    range: 18,
    active: true,
    shadow: false,
    type: LightSource.Type.Point({}),
  })

  setupVisualTest({
    lookAtPos: Vector3.create(8, 1.5, 9.5),
    cameraPos: Vector3.create(8, 4.5, 0),
  })
}
