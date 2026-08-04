DOCTOR WHO VR — PREFAB ERROR FIX

Fixes:
InvalidOperationException: Destroying a GameObject inside a Prefab instance is not allowed.

INSTALL
1. Exit Play Mode in Unity.
2. Extract these files.
3. Put both fix files directly inside:
   C:\Users\aiden\My project\
4. Double-click:
   Run-Prefab-Error-Fix.bat
5. Return to Unity and wait for compilation.
6. In Unity's top menu run:
   Doctor Who VR > Rebuild Clean Portal Prototype

The patch changes the XR vignette removal from destroying a nested prefab
object to safely disabling it. The dark movement border remains removed.
