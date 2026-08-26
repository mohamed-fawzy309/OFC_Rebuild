# Import Settings Conventions

## General Rules
Every imported asset must have documented import settings before gameplay integration.

## Animation Imports

### Root Motion
- Document per clip whether root motion is applied
- Default: root motion OFF for gameplay-driven movement
- Root motion ON only for cinematics or specific animations

### Avatar Configuration
- Humanoid: use when retargeting across character models
- Generic: use for animal or non-humanoid rigs
- Record decision per imported model

### Loop Time
- Explicitly set for looping animations (locomotion, idle)
- Explicitly OFF for one-shot animations (pass, shoot, tackle)

### Compression
- Keyframe Reduction for most animations
- Optimal for large clips where quality is less critical
- None for critical gameplay animations

### Scale
- Import scale: verify matches project scale (1 unit = 1 meter)
- Do not apply random scale multipliers

### Axis
- Forward: Z+
- Right: X+
- Verify before pipeline integration

### Retime
- Do not retime blindly
- Source animation speed is intentional
- Adjust only with documented reason

## Model Imports
- Read/Write enabled only when runtime mesh manipulation needed
- Material location: use External Materials if available
- Mesh compression: Medium for non-deformable, Off for characters

## Texture Imports
- Max texture size per platform
- Compression: ASTC for mobile, BC7 for desktop
- Mipmaps: ON for 3D, OFF for UI/sprites
- sRGB: ON for color, OFF for normals/masks

## Audio Imports
- Force to Mono for non-spatial sounds
- Sample rate: match source or 44100Hz
- Compression: Vorbis for music, PCM for short SFX

## Validation Checklist
- [ ] Import settings reviewed
- [ ] Asset imports without errors/warnings
- [ ] Asset appears correct in Scene view
- [ ] Scale matches project standards
- [ ] No hidden compression artifacts
- [ ] Ready for gameplay integration
