import {
  AvatarModifierArea,
  AvatarModifierType,
  Entity,
  MainCamera,
  Transform,
  VirtualCamera,
  engine,
} from '@dcl/sdk/ecs'
import { Vector3 } from '@dcl/sdk/math'

/**
 * Visual-regression scenes need two things real Decentraland scenes don't:
 *
 *   1. The local avatar hidden. Test snapshots are pixel-diffed against a
 *      committed baseline; a wandering avatar in frame produces a different
 *      diff every run. AvatarModifierArea + AMT_HIDE_AVATARS removes it from
 *      the area covering the parcel.
 *
 *   2. A fixed camera pose. The default third-person camera follows the
 *      player, so any spawn jitter or animation shifts the framing. A
 *      VirtualCamera locked to a transform pins the shot.
 *
 * Call this at the END of `main()` after the scene's content entities have
 * been created (the camera needs an entity to look at).
 *
 * Override or skip this helper when the scene specifically tests avatar
 * rendering, camera behavior, or any feature this setup would suppress.
 *
 * If you change `cameraPos` for a scene, re-record its baselines with:
 *   metaforge explorer test --filter "..." --record-baselines
 */
export function setupVisualTest(opts: { lookAt: Entity; cameraPos?: Vector3 }) {
  const { lookAt, cameraPos = Vector3.create(8, 3, 0.5) } = opts

  const hideArea = engine.addEntity()
  Transform.create(hideArea, { position: Vector3.create(8, 1, 8) })
  AvatarModifierArea.create(hideArea, {
    area: Vector3.create(16, 16, 16),
    modifiers: [AvatarModifierType.AMT_HIDE_AVATARS, AvatarModifierType.AMT_DISABLE_PASSPORTS],
    excludeIds: [],
  })

  const cam = engine.addEntity()
  Transform.create(cam, { position: cameraPos })
  VirtualCamera.create(cam, { lookAtEntity: lookAt })
  MainCamera.createOrReplace(engine.CameraEntity, { virtualCameraEntity: cam })
}
