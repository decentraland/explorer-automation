import { AvatarModifierArea, AvatarModifierType, engine, MainCamera, Transform, VirtualCamera } from '@dcl/sdk/ecs'
import { Vector3 } from '@dcl/sdk/math'

// Placeholder bundle. The C# VisualHost overwrites bin/index.js with the
// currently-loading test scene's compiled output before each fixture runs,
// so what actually executes inside Explorer is never this file.
export function main() {
    // Visual Test Setup: avatar hiding + virtual camera for snapshot
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
    VirtualCamera.create(snapshotCamera
        // , { lookAtEntity: targetEntity }
    )
    Transform.create(snapshotCamera, {
        position: Vector3.create(8, 3, 0.5)
    })
    MainCamera.createOrReplace(engine.CameraEntity, {
        virtualCameraEntity: snapshotCamera,
    })

    // Test case body
    // (...)
}
