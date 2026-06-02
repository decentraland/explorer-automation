import { engine, Material, MeshRenderer, Transform } from '@dcl/sdk/ecs'
import { Color4, Vector3 } from '@dcl/sdk/math'
import { setupVisualTest } from './visual-test-setup'

export function main() {
  // Render whatever this test scene is about.
  const cube = engine.addEntity()
  Transform.create(cube, { position: Vector3.create(8, 1, 8) })
  MeshRenderer.setBox(cube)
  Material.setPbrMaterial(cube, { albedoColor: Color4.create(1, 0, 0, 1) })

  // Determinism boilerplate — see ./visual-test-setup.ts for rationale.
  setupVisualTest({ lookAt: cube })
}
