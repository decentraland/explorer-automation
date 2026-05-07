import { engine, GltfContainer, Material, MeshRenderer, Transform } from '@dcl/sdk/ecs'
import { Color4, Quaternion, Vector3 } from '@dcl/sdk/math'
import { setupVisualTest } from './visual-test-setup'


export function main() {
  // Render whatever this test scene is about.
  const cube = engine.addEntity()
  Transform.create(cube, { position: Vector3.create(8, 1, 8) })
  MeshRenderer.setBox(cube)
  Material.setPbrMaterial(cube, { albedoColor: Color4.create(1, 0, 0, 1) })

  // Determinism boilerplate — see ./visual-test-setup.ts for rationale.
  setupVisualTest({ lookAt: cube })


  // ── hero: SampleScene_*.glb panels at distinct positions ─────────────────
  // 01 left, 02 right at the same height; 03 lifted above and centered so
  // the three showcases don't visually overlap. y-lift is screenshot-only —
  // ground contact doesn't matter for visual diff.
  const sampleScene = engine.addEntity()
  Transform.create(sampleScene, {
    scale: Vector3.create(0.8, 0.8, 0.8),
    rotation: {
      "x": 0.03378266096115112,
      "y": 0.9576621651649475,
      "z": -0.12607862055301666,
      "w": 0.2566048204898834
    },
    position: {
      "x": 9.770000457763672,
      "y": 2.25,
      "z": -14.989999771118164
    }
  })



  GltfContainer.create(sampleScene, { src: 'assets/sdk7-models/SampleScene.glb' })

  // Camera on the +Z side of the panels so the showcase fronts face it.
  // Pulled back to z=30 to fit the full 32m-wide layout (tree at x=3 →
  // treehouse at x=29) inside the HFOV. Aim raised to y=5 so the lifted
  // SampleScene_03 (y=8+) and the y=2 foreground accents both fit vertically.
  setupVisualTest({
    lookAtPos: Vector3.create(16, 4, 6),
    cameraPos: Vector3.create(16, 12, -9),
  })


}