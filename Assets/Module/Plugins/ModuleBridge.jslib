// C# → page for the module build (ModuleWebBridge.Post). Unity links every .jslib into WebGL builds as an
// Emscripten library; a function is only included where C# declares it with [DllImport("__Internal")].
mergeInto(LibraryManager.library, {
  ModulePostToParent: function (jsonPtr) {
    var json = UTF8ToString(jsonPtr);                    // C# string → pointer into the WASM heap → JS string
    if (window.parent === window) return;                // not embedded: nobody listens
    // targetOrigin = own origin: the message only reaches a parent page on the same server
    window.parent.postMessage(JSON.parse(json), window.location.origin);
  },
});
