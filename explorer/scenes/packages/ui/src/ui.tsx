import { Material, MeshRenderer, Transform, engine } from '@dcl/sdk/ecs'
import { Color4, Vector3 } from '@dcl/sdk/math'
import ReactEcs, { Button, Input, Label, ReactEcsRenderer, UiEntity } from '@dcl/sdk/react-ecs'
import { setupVisualTest } from './visual-test-setup'

/**
 * ui — react-ecs screen-space UI surface.
 *
 * Visual contract: regressions in yoga layout, font hinting, UI text
 * rasterization, UiBackground color/texture, button/input chrome, or
 * alpha blending on the UI canvas show up here. The world is intentionally
 * kept to a uniform backdrop so UI changes pop in the diff.
 */
export function main() {
  // Uniform world backdrop so the UI canvas is the only thing that varies.
  const backdrop = engine.addEntity()
  Transform.create(backdrop, {
    position: Vector3.create(8, 0, 8),
    scale: Vector3.create(16, 0.05, 16),
  })
  MeshRenderer.setBox(backdrop)
  Material.setPbrMaterial(backdrop, { albedoColor: Color4.create(0.1, 0.1, 0.12, 1) })

  ReactEcsRenderer.setUiRenderer(uiRoot)

  // No camera lock — UI is screen-space, world camera position barely matters
  // for the diff. We still hide the avatar so it doesn't intrude into world view.
  setupVisualTest()
}

const uiRoot = () => (
  <UiEntity
    uiTransform={{
      width: '100%',
      height: '100%',
      flexDirection: 'column',
      alignItems: 'center',
      justifyContent: 'center',
      padding: 32,
    }}
    uiBackground={{ color: Color4.create(0, 0, 0, 0.55) }}
  >
    <Label
      value="Visual Regression — UI Surface"
      fontSize={36}
      color={Color4.create(1, 1, 1, 1)}
      uiTransform={{ margin: { bottom: 24 } }}
    />

    <UiEntity
      uiTransform={{ flexDirection: 'row', width: 720, height: 200 }}
    >
      <UiEntity
        uiTransform={{ flexGrow: 1, margin: { right: 12 }, padding: 16 }}
        uiBackground={{ color: Color4.create(0.85, 0.2, 0.3, 1) }}
      >
        <Label
          value={'Solid panel\nLine two'}
          fontSize={20}
          color={Color4.create(1, 1, 1, 1)}
        />
      </UiEntity>
      <UiEntity
        uiTransform={{ flexGrow: 1, margin: { left: 12 }, padding: 16 }}
        uiBackground={{ color: Color4.create(0.2, 0.5, 0.85, 1) }}
      >
        <Label
          value={'Translucent panel'}
          fontSize={20}
          color={Color4.create(1, 1, 1, 1)}
          uiTransform={{ margin: { bottom: 8 } }}
        />
        <Label
          value={'Tests stacked alpha blending\nover the world background.'}
          fontSize={14}
          color={Color4.create(0.92, 0.92, 0.92, 1)}
        />
      </UiEntity>
    </UiEntity>

    <UiEntity
      uiTransform={{
        flexDirection: 'row',
        width: 720,
        height: 64,
        margin: { top: 24 },
        alignItems: 'center',
      }}
    >
      <Button
        value="Primary action"
        fontSize={18}
        variant="primary"
        uiTransform={{ width: 200, height: 48, margin: { right: 16 } }}
      />
      <Button
        value="Secondary"
        fontSize={18}
        variant="secondary"
        uiTransform={{ width: 160, height: 48, margin: { right: 16 } }}
      />
      <Input
        placeholder="Type something…"
        fontSize={16}
        uiTransform={{ flexGrow: 1, height: 48 }}
      />
    </UiEntity>

    <Label
      value={
        'Multi-line label exercises kerning and line-break behavior.\n' +
        'Three lines of varying widths catch font-metrics drift.\n' +
        'The quick brown fox jumps over the lazy dog. 0123456789'
      }
      fontSize={14}
      color={Color4.create(0.85, 0.85, 0.85, 1)}
      textAlign="middle-center"
      uiTransform={{ margin: { top: 24 }, width: 720 }}
    />
  </UiEntity>
)
