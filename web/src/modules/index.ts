/**
 * Explicit list of the frontend module manifests, the mirror image of `IvaoHub.Web/Modules.cs`
 * (design M0 section 6.5). Adding a module means adding one line here and one there; no scanning,
 * no dynamic import. The `ModuleManifest` type and the loader that registers blocks, widgets and
 * routes arrive in F6; the first module (`atc`) in F8.
 */
export const moduleManifests = [];
