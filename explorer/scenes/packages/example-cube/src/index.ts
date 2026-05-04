import {AvatarModifierArea, AvatarModifierType, engine, MainCamera, Material, MeshRenderer, Transform, VirtualCamera} from '@dcl/sdk/ecs'
import {Color4, Vector3} from '@dcl/sdk/math'

export function main() { 
  // Test case body
  const cube = engine.addEntity()
  Transform.create(cube, { position: Vector3.create(8, 1, 8) })
  MeshRenderer.setBox(cube)
  Material.setPbrMaterial(cube, { albedoColor: Color4.create(1, 0, 0, 1) })

  // Visual Test Setup - avatar hiding + virtual camera for snapshot
  const avatarHidingArea = engine.addEntity()
  Transform.create(avatarHidingArea, {
    position: Vector3.create(8, 1, 8)
  })
  AvatarModifierArea.create(avatarHidingArea, {
    area: Vector3.create(16, 16, 16),
    modifiers: [AvatarModifierType.AMT_HIDE_AVATARS, AvatarModifierType.AMT_DISABLE_PASSPORTS],
    excludeIds: []
  })

  const snapshotCamera = engine.addEntity()
  VirtualCamera.create(snapshotCamera, { lookAtEntity: cube })
  Transform.create(snapshotCamera, {
    position: Vector3.create(8, 3, 0.5)
  })
  MainCamera.createOrReplace(engine.CameraEntity, {
    virtualCameraEntity: snapshotCamera,
  })
}
