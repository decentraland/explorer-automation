import { engine, Transform, MeshRenderer, Material } from '@dcl/sdk/ecs'
import { Color4, Vector3 } from '@dcl/sdk/math'

export function main() {
  const cube = engine.addEntity()
  Transform.create(cube, { position: Vector3.create(8, 1, 8) })
  MeshRenderer.setBox(cube)
  Material.setPbrMaterial(cube, { albedoColor: Color4.create(1, 0, 0, 1) })
}
