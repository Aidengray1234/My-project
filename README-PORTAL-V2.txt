DOCTOR WHO VR — PORTAL V2

This fixes the distorted / splitting portal surface.

Main change:
The old shader stretched the full portal-camera image over the doorway UVs.
Portal V2 samples the destination view using screen-space coordinates, so
moving your head changes perspective like looking through a real doorway.

Also included:
- Separate left/right eye render textures remain supported.
- One-sided rendering prevents the back of a portal from rendering.
- More stable destination clipping near the doorway.
- Dynamic texture aspect ratio.
- Portal surfaces remain excluded from their own camera.
- Two-way crossing and high-speed crossing detection remain enabled.

INSTALL
1. Exit Play Mode.
2. Extract the ZIP.
3. Put the extracted files and _PortalV2Payload folder directly into:
   C:\Users\aiden\My project\
4. Double-click Install-Portal-V2.bat.
5. Return to Unity and wait for compilation.
6. Clear Console and enter Play Mode.

Do not select Rebuild Clean Portal Prototype unless Unity reports missing
objects or references. Rebuilding is not normally required for this patch.
